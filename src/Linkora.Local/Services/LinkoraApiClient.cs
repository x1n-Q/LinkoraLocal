using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Linkora.Local.Services;

internal sealed class LinkoraApiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public LinkoraApiClient()
    {
#if DEBUG
        var configured = Environment.GetEnvironmentVariable("LINKORA_API_URL");
        var baseUrl = string.IsNullOrWhiteSpace(configured)
            ? "https://api.linkora.top"
            : configured.TrimEnd('/');
#else
        const string baseUrl = "https://api.linkora.top";
#endif
        _http = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        })
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(20)
        };
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Linkora-Local/0.1.0");
    }

    public Uri BaseAddress => _http.BaseAddress!;

    public void SetAccessToken(string? token)
    {
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    public Task<StartDeviceData> StartDeviceAsync(CancellationToken cancellationToken = default) =>
        SendAsync<StartDeviceData>(
            HttpMethod.Post,
            "/api/desktop/device/start",
            new { clientName = $"{Environment.MachineName} · Linkora Local" },
            cancellationToken);

    public Task<DeviceTokenData> PollDeviceAsync(
        string deviceCode,
        CancellationToken cancellationToken = default) =>
        SendAsync<DeviceTokenData>(
            HttpMethod.Post,
            "/api/desktop/device/token",
            new { deviceCode },
            cancellationToken);

    public Task<DesktopSession> GetSessionAsync(CancellationToken cancellationToken = default) =>
        SendAsync<DesktopSession>(HttpMethod.Get, "/api/desktop/session", null, cancellationToken);

    public Task RevokeSessionAsync(CancellationToken cancellationToken = default) =>
        SendWithoutDataAsync(
            HttpMethod.Post,
            "/api/desktop/session/revoke",
            new { },
            cancellationToken);

    public Task<List<DomainPool>> GetPoolsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<DomainPool>>(
            HttpMethod.Get,
            "/api/subdomains/pools",
            null,
            cancellationToken);

    public Task<WorkspaceData> GetWorkspaceAsync(CancellationToken cancellationToken = default) =>
        SendAsync<WorkspaceData>(
            HttpMethod.Get,
            "/api/subdomains/my",
            null,
            cancellationToken);

    public Task<ClaimData> ClaimHostnameAsync(
        string domainPoolId,
        string label,
        string localServiceUrl,
        CancellationToken cancellationToken = default) =>
        SendAsync<ClaimData>(
            HttpMethod.Post,
            "/api/subdomains",
            new
            {
                domainPoolId,
                label,
                connectionType = "MANAGED_TUNNEL",
                target = localServiceUrl
            },
            cancellationToken);

    public Task<TunnelCredential> CreateTunnelAsync(
        string subdomainId,
        string localServiceUrl,
        CancellationToken cancellationToken = default) =>
        SendAsync<TunnelCredential>(
            HttpMethod.Post,
            "/api/tunnels",
            new { subdomainId, localServiceUrl },
            cancellationToken);

    public Task<TunnelCredential> ConnectTunnelAsync(
        string managedTunnelId,
        string localServiceUrl,
        CancellationToken cancellationToken = default) =>
        SendAsync<TunnelCredential>(
            HttpMethod.Post,
            $"/api/desktop/tunnels/{Uri.EscapeDataString(managedTunnelId)}/connect",
            new { localServiceUrl },
            cancellationToken);

    private async Task SendWithoutDataAsync(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, body);
        using var response = await _http.SendAsync(request, cancellationToken);
        var envelope = await ReadEnvelopeAsync<object>(response, cancellationToken);
        if (!response.IsSuccessStatusCode || !envelope.Success)
        {
            throw new LinkoraApiException(
                envelope.Error ?? "Linkora returned an error.",
                response.StatusCode);
        }
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, body);
        using var response = await _http.SendAsync(request, cancellationToken);
        var envelope = await ReadEnvelopeAsync<T>(response, cancellationToken);
        if (!response.IsSuccessStatusCode || !envelope.Success)
        {
            throw new LinkoraApiException(
                envelope.Error ?? "Linkora returned an error.",
                response.StatusCode);
        }
        return envelope.Data
               ?? throw new LinkoraApiException(
                   "Linkora returned an incomplete response.",
                   response.StatusCode);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        return request;
    }

    private async Task<ApiEnvelope<T>> ReadEnvelopeAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<ApiEnvelope<T>>(
                   stream,
                   _json,
                   cancellationToken)
               ?? new ApiEnvelope<T>
               {
                   Success = false,
                   Error = "Linkora returned an unreadable response."
               };
    }

    public void Dispose() => _http.Dispose();
}

internal sealed class LinkoraApiException(string message, HttpStatusCode statusCode)
    : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
