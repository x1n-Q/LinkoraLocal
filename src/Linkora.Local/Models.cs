using System.Text.Json.Serialization;

namespace Linkora.Local;

public sealed record LocalService(
    int Port,
    int ProcessId,
    string ProcessName,
    string Name,
    string Protocol)
{
    public string ServiceUrl => $"{Protocol}://127.0.0.1:{Port}";
    public string PortLabel => $"localhost:{Port}";
    public string ProcessLabel => $"{ProcessName} · PID {ProcessId}";
}

public sealed class ApiEnvelope<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
    public string? Message { get; set; }
}

public sealed class StartDeviceData
{
    public string DeviceCode { get; set; } = "";
    public string UserCode { get; set; } = "";
    public string VerificationUrl { get; set; } = "";
    public int ExpiresIn { get; set; }
    public int PollInterval { get; set; }
}

public sealed class DeviceTokenData
{
    public string Status { get; set; } = "";
    public string? AccessToken { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class DesktopSession
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public int HostnameQuota { get; set; }
    public DateTimeOffset TokenExpiresAt { get; set; }
}

public sealed class DomainPool
{
    public string Id { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset ServiceEndsAt { get; set; }
    public override string ToString() => Domain;
}

public sealed class ManagedTunnelSummary
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
}

public sealed class Subdomain
{
    public string Id { get; set; } = "";
    public string Hostname { get; set; } = "";
    public int SlotNumber { get; set; }
    public string ConnectionType { get; set; } = "";
    public string Status { get; set; } = "";
    public ManagedTunnelSummary? ManagedTunnel { get; set; }
    public override string ToString() => Hostname;
}

public sealed class WorkspaceData
{
    public List<Subdomain> Subdomains { get; set; } = [];
    public int Quota { get; set; }
}

public sealed class ClaimData
{
    public string Id { get; set; } = "";
    public string Hostname { get; set; } = "";
    public string Status { get; set; } = "";
}

public sealed class TunnelCredential
{
    public string? Token { get; set; }
    public string? TunnelToken { get; set; }
    public string? Hostname { get; set; }
    public bool LocalPreview { get; set; }
}
