using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Linkora.Local.Services;

internal sealed partial class PortDiscoveryService : IDisposable
{
    private const int AfInet = 2;
    private const int TcpTableOwnerPidListener = 3;
    private const int ErrorInsufficientBuffer = 122;
    private readonly HttpClient _probeClient;

    private static readonly HashSet<int> BlockedPorts =
    [
        20, 21, 22, 23, 25, 53, 110, 135, 137, 138, 139, 143, 161, 162, 389,
        445, 465, 514, 587, 636, 873, 989, 990, 993, 995, 1433, 1521, 2049,
        2375, 2376, 3306, 3389, 5432, 5672, 5900, 5985, 5986, 6379, 6443,
        9200, 9300, 11211, 27017
    ];

    private static readonly HashSet<string> BlockedProcesses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "System", "Idle", "svchost", "lsass", "services", "wininit",
            "mysqld", "postgres", "redis-server", "mongod", "sshd",
            "cloudflared", "Code", "devenv", "Cursor", "chrome", "msedge",
            "firefox"
        };

    public PortDiscoveryService()
    {
        _probeClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        })
        {
            Timeout = TimeSpan.FromMilliseconds(900)
        };
        _probeClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("Linkora-Local-Probe", "0.1"));
    }

    public async Task<IReadOnlyList<LocalService>> DiscoverAsync(
        CancellationToken cancellationToken = default)
    {
        var listeners = GetListeners()
            .Where(listener => listener.Port >= 1024)
            .Where(listener => !BlockedPorts.Contains(listener.Port))
            .DistinctBy(listener => (listener.Port, listener.ProcessId))
            .Take(128)
            .ToList();

        using var gate = new SemaphoreSlim(12);
        var probes = listeners.Select(async listener =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                return await ProbeAsync(listener, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        });

        var results = await Task.WhenAll(probes);
        return results
            .Where(service => service is not null)
            .Cast<LocalService>()
            .OrderBy(service => service.Port)
            .ToList();
    }

    private async Task<LocalService?> ProbeAsync(
        Listener listener,
        CancellationToken cancellationToken)
    {
        string processName;
        try
        {
            processName = Process.GetProcessById(listener.ProcessId).ProcessName;
        }
        catch
        {
            return null;
        }
        if (BlockedProcesses.Contains(processName))
        {
            return null;
        }

        foreach (var protocol in new[] { "http", "https" })
        {
            try
            {
                var url = $"{protocol}://127.0.0.1:{listener.Port}/";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await _probeClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                var mediaType = response.Content.Headers.ContentType?.MediaType ?? "";
                var name = FriendlyProcessName(processName, listener.Port);
                if (mediaType.Contains("html", StringComparison.OrdinalIgnoreCase))
                {
                    var html = await ReadPrefixAsync(
                        response.Content,
                        24 * 1024,
                        cancellationToken);
                    var title = TitleRegex().Match(html).Groups["title"].Value;
                    title = WebUtility.HtmlDecode(WhitespaceRegex().Replace(title, " ")).Trim();
                    if (title.Length is > 0 and <= 80)
                    {
                        name = title;
                    }
                }
                return new LocalService(
                    listener.Port,
                    listener.ProcessId,
                    processName,
                    name,
                    protocol);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Probe timeout; try the next protocol.
            }
            catch (HttpRequestException)
            {
                // Not an HTTP service.
            }
            catch (IOException)
            {
                // The local process closed the probe connection.
            }
        }
        return null;
    }

    private static async Task<string> ReadPrefixAsync(
        HttpContent content,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var buffer = new char[maximumBytes];
        var read = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken);
        return new string(buffer, 0, read);
    }

    private static string FriendlyProcessName(string processName, int port)
    {
        var friendly = processName.ToLowerInvariant() switch
        {
            "node" => "Node.js application",
            "python" or "python3" => "Python application",
            "dotnet" => ".NET application",
            "php" => "PHP application",
            "java" or "javaw" => "Java application",
            "ruby" => "Ruby application",
            _ => processName
        };
        return $"{friendly} on port {port}";
    }

    private static IEnumerable<Listener> GetListeners()
    {
        var size = 0;
        var result = GetExtendedTcpTable(
            IntPtr.Zero,
            ref size,
            true,
            AfInet,
            TcpTableOwnerPidListener,
            0);
        if (result != ErrorInsufficientBuffer)
        {
            yield break;
        }

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            result = GetExtendedTcpTable(
                buffer,
                ref size,
                true,
                AfInet,
                TcpTableOwnerPidListener,
                0);
            if (result != 0)
            {
                yield break;
            }

            var count = Marshal.ReadInt32(buffer);
            var rowPointer = IntPtr.Add(buffer, sizeof(int));
            var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();
            for (var index = 0; index < count; index += 1)
            {
                var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPointer);
                var port = (ushort)IPAddress.NetworkToHostOrder((short)row.LocalPort);
                if (port > 0 && row.OwningPid > 0)
                {
                    yield return new Listener(port, checked((int)row.OwningPid));
                }
                rowPointer = IntPtr.Add(rowPointer, rowSize);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose() => _probeClient.Dispose();

    private sealed record Listener(int Port, int ProcessId);

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        public uint State;
        public uint LocalAddress;
        public uint LocalPort;
        public uint RemoteAddress;
        public uint RemotePort;
        public uint OwningPid;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr table,
        ref int size,
        bool order,
        int ipVersion,
        int tableClass,
        uint reserved);

    [GeneratedRegex(
        @"<title[^>]*>(?<title>.*?)</title>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
