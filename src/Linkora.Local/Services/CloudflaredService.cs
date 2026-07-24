using System.Diagnostics;
using System.IO;

namespace Linkora.Local.Services;

internal sealed class CloudflaredService : IAsyncDisposable
{
    private Process? _process;
    public event Action<string>? OutputReceived;
    public bool IsRunning => _process is { HasExited: false };

    public async Task StartAsync(string tunnelToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tunnelToken))
        {
            throw new InvalidOperationException("Linkora did not issue a tunnel credential.");
        }

        await StopAsync();
        var executable = FindCloudflared()
                         ?? throw new FileNotFoundException(
                             "cloudflared was not found. Install it or place cloudflared.exe beside Linkora Local.");

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("tunnel");
        startInfo.ArgumentList.Add("--no-autoupdate");
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--token");
        startInfo.ArgumentList.Add(tunnelToken);

        var recentOutput = new Queue<string>();
        var outputLock = new object();
        void RelayOutput(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            var sanitized = line.Replace(
                tunnelToken,
                "[redacted]",
                StringComparison.Ordinal);
            lock (outputLock)
            {
                if (recentOutput.Count == 8)
                {
                    recentOutput.Dequeue();
                }
                recentOutput.Enqueue(sanitized);
            }
            OutputReceived?.Invoke(sanitized);
        }

        _process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
        _process.OutputDataReceived += (_, eventArgs) => RelayOutput(eventArgs.Data);
        _process.ErrorDataReceived += (_, eventArgs) => RelayOutput(eventArgs.Data);
        _process.Exited += (_, _) => OutputReceived?.Invoke("Tunnel connector stopped.");

        if (!_process.Start())
        {
            throw new InvalidOperationException("cloudflared could not be started.");
        }
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        await Task.Delay(TimeSpan.FromSeconds(1.5), cancellationToken);
        if (_process.HasExited)
        {
            _process.WaitForExit();
            string? detail;
            lock (outputLock)
            {
                detail = recentOutput.FirstOrDefault(line =>
                             line.Contains("error", StringComparison.OrdinalIgnoreCase)
                             || line.Contains("incorrect usage", StringComparison.OrdinalIgnoreCase)
                             || line.Contains("failed", StringComparison.OrdinalIgnoreCase))
                         ?? recentOutput.LastOrDefault();
            }
            throw new InvalidOperationException(
                $"cloudflared stopped unexpectedly with exit code {_process.ExitCode}."
                + (string.IsNullOrWhiteSpace(detail) ? "" : $" {detail}"));
        }
    }

    public async Task StopAsync()
    {
        var process = _process;
        _process = null;
        if (process is null)
        {
            return;
        }
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception)
        {
            // The process may already have exited between the checks.
        }
        finally
        {
            process.Dispose();
        }
    }

    private static string? FindCloudflared()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "cloudflared.exe"),
            Path.Combine(AppContext.BaseDirectory, "tools", "cloudflared.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "cloudflared",
                "cloudflared.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "cloudflared",
                "cloudflared.exe")
        };
        var direct = candidates.FirstOrDefault(File.Exists);
        if (direct is not null)
        {
            return direct;
        }

        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "")
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim(), "cloudflared.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // Ignore malformed PATH entries.
            }
        }
        return null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
