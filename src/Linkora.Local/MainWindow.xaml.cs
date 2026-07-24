using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Linkora.Local.Services;

namespace Linkora.Local;

public partial class MainWindow : Window, IDisposable
{
    private const int DwmUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmUseImmersiveDarkMode = 20;

    private readonly LinkoraApiClient _api = new();
    private readonly PortDiscoveryService _discovery = new();
    private readonly CloudflaredService _cloudflared = new();
    private readonly ObservableCollection<LocalService> _localServices = [];
    private readonly ObservableCollection<Subdomain> _subdomains = [];
    private readonly ObservableCollection<DomainPool> _pools = [];
    private readonly DispatcherTimer _devicePollTimer;
    private string? _deviceCode;
    private string? _verificationUrl;
    private string? _publishedUrl;
    private DesktopSession? _session;
    private WorkspaceData? _workspace;
    private bool _busy;
    private bool _disposed;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => EnableDarkTitleBar();
        LocalServicesList.ItemsSource = _localServices;
        ExistingHostnameCombo.ItemsSource = _subdomains;
        DomainPoolCombo.ItemsSource = _pools;
        ApiEnvironmentText.Text = _api.BaseAddress.Host;
        _cloudflared.OutputReceived += HandleCloudflaredOutput;
        _devicePollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _devicePollTimer.Tick += DevicePollTimer_Tick;
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    private void EnableDarkTitleBar()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var enabled = 1;
        var size = Marshal.SizeOf(enabled);
        if (DwmSetWindowAttribute(
                handle,
                DwmUseImmersiveDarkMode,
                ref enabled,
                size) != 0)
        {
            var fallbackResult = DwmSetWindowAttribute(
                handle,
                DwmUseImmersiveDarkModeBefore20H1,
                ref enabled,
                size);
            if (fallbackResult != 0)
            {
                Debug.WriteLine(
                    $"Dark title bar is unavailable (HRESULT {fallbackResult}).");
            }
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var storedToken = CredentialStore.Read();
        if (!string.IsNullOrWhiteSpace(storedToken))
        {
            _api.SetAccessToken(storedToken);
            try
            {
                await RestoreSessionAsync();
            }
            catch
            {
                CredentialStore.Delete();
                _api.SetAccessToken(null);
                ShowSignedOutState();
            }
        }
        else
        {
            ShowSignedOutState();
        }

        await RefreshServicesAsync();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (System.Windows.Application.Current is App { IsQuitting: false })
        {
            e.Cancel = true;
            Hide();
            SetStatus(
                _cloudflared.IsRunning
                    ? "Still publishing in the notification area."
                    : "Linkora Local is running in the notification area.",
                _cloudflared.IsRunning);
        }
    }

    private async Task RestoreSessionAsync()
    {
        _session = await _api.GetSessionAsync();
        AccountStatusText.Text = _session.Email;
        ConnectAccountButton.Visibility = Visibility.Collapsed;
        SignOutButton.Visibility = Visibility.Visible;
        SignedOutPanel.Visibility = Visibility.Collapsed;
        PublishConfigurationPanel.Visibility = Visibility.Visible;
        await LoadWorkspaceAsync();
        SetStatus("Account connected securely.", true);
    }

    private void ShowSignedOutState()
    {
        _session = null;
        _workspace = null;
        AccountStatusText.Text = "Not connected";
        ConnectAccountButton.Visibility = Visibility.Visible;
        SignOutButton.Visibility = Visibility.Collapsed;
        SignedOutPanel.Visibility = Visibility.Visible;
        PublishConfigurationPanel.Visibility = Visibility.Collapsed;
        DeviceCodePanel.Visibility = Visibility.Collapsed;
        _devicePollTimer.Stop();
        UpdatePublishButton();
    }

    private async Task LoadWorkspaceAsync()
    {
        var poolsTask = _api.GetPoolsAsync();
        var workspaceTask = _api.GetWorkspaceAsync();
        await Task.WhenAll(poolsTask, workspaceTask);

        _pools.Clear();
        foreach (var pool in poolsTask.Result.Where(pool => pool.Status == "ACTIVE"))
        {
            _pools.Add(pool);
        }
        DomainPoolCombo.SelectedIndex = _pools.Count > 0 ? 0 : -1;

        _workspace = workspaceTask.Result;
        _subdomains.Clear();
        foreach (var subdomain in _workspace.Subdomains)
        {
            _subdomains.Add(subdomain);
        }
        ExistingHostnameCombo.SelectedIndex = _subdomains.Count > 0 ? 0 : -1;

        var canCreate = _subdomains.Count < _workspace.Quota;
        CreateNewHostnameCheckBox.Visibility =
            canCreate ? Visibility.Visible : Visibility.Collapsed;
        if (_subdomains.Count == 0)
        {
            CreateNewHostnameCheckBox.IsChecked = true;
            CreateNewHostnameCheckBox.IsEnabled = false;
        }
        else
        {
            CreateNewHostnameCheckBox.IsEnabled = true;
            CreateNewHostnameCheckBox.IsChecked = false;
        }
        UpdateHostnameMode();
    }

    private async Task RefreshServicesAsync()
    {
        if (_busy)
        {
            return;
        }
        _busy = true;
        RefreshButton.IsEnabled = false;
        ScanningPanel.Visibility = Visibility.Visible;
        NoServicesPanel.Visibility = Visibility.Collapsed;
        LocalServicesList.Visibility = Visibility.Collapsed;
        SetStatus("Scanning loopback services…");
        try
        {
            var services = await _discovery.DiscoverAsync();
            _localServices.Clear();
            foreach (var service in services)
            {
                _localServices.Add(service);
            }
            if (_localServices.Count > 0)
            {
                LocalServicesList.Visibility = Visibility.Visible;
                LocalServicesList.SelectedIndex = 0;
                SetStatus(
                    $"{_localServices.Count} local web service{(_localServices.Count == 1 ? "" : "s")} found.",
                    true);
            }
            else
            {
                NoServicesPanel.Visibility = Visibility.Visible;
                SetStatus("No publishable HTTP services were found.");
            }
        }
        catch (Exception exception)
        {
            NoServicesPanel.Visibility = Visibility.Visible;
            SetStatus($"Service scan failed: {exception.Message}", false, true);
        }
        finally
        {
            ScanningPanel.Visibility = Visibility.Collapsed;
            RefreshButton.IsEnabled = true;
            _busy = false;
            UpdatePublishButton();
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        await RefreshServicesAsync();

    private async void ConnectAccountButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }
        _busy = true;
        ConnectAccountButton.IsEnabled = false;
        SetStatus("Creating a secure browser authorization…");
        try
        {
            var authorization = await _api.StartDeviceAsync();
            _deviceCode = authorization.DeviceCode;
            _verificationUrl = authorization.VerificationUrl;
            DeviceCodeText.Text = authorization.UserCode;
            DeviceCodePanel.Visibility = Visibility.Visible;
            _devicePollTimer.Interval = TimeSpan.FromSeconds(
                Math.Clamp(authorization.PollInterval, 2, 10));
            _devicePollTimer.Start();
            OpenUrl(_verificationUrl);
            SetStatus("Confirm the matching code in your browser.");
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, false, true);
        }
        finally
        {
            ConnectAccountButton.IsEnabled = true;
            _busy = false;
        }
    }

    private void OpenBrowserButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_verificationUrl))
        {
            OpenUrl(_verificationUrl);
        }
    }

    private async void DevicePollTimer_Tick(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_deviceCode))
        {
            _devicePollTimer.Stop();
            return;
        }
        _devicePollTimer.Stop();
        try
        {
            var result = await _api.PollDeviceAsync(_deviceCode);
            if (result.Status == "APPROVED" && !string.IsNullOrWhiteSpace(result.AccessToken))
            {
                CredentialStore.Save(result.AccessToken);
                _api.SetAccessToken(result.AccessToken);
                _deviceCode = null;
                _verificationUrl = null;
                DeviceCodePanel.Visibility = Visibility.Collapsed;
                await RestoreSessionAsync();
                return;
            }
            _devicePollTimer.Start();
        }
        catch (LinkoraApiException exception)
            when (exception.StatusCode is HttpStatusCode.TooManyRequests)
        {
            _devicePollTimer.Interval = TimeSpan.FromSeconds(8);
            _devicePollTimer.Start();
        }
        catch (Exception exception)
        {
            _deviceCode = null;
            DeviceCodePanel.Visibility = Visibility.Collapsed;
            SetStatus(exception.Message, false, true);
        }
    }

    private async void SignOutButton_Click(object sender, RoutedEventArgs e)
    {
        await StopPublishingAsync();
        try
        {
            await _api.RevokeSessionAsync();
        }
        catch
        {
            // Local removal still signs this device out if the server is unavailable.
        }
        CredentialStore.Delete();
        _api.SetAccessToken(null);
        ShowSignedOutState();
        SetStatus("This computer has been disconnected.");
    }

    private void LocalServicesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedServiceText.Text = LocalServicesList.SelectedItem is LocalService service
            ? service.ServiceUrl
            : "Select a service on the left";
        UpdatePublishButton();
    }

    private void ExistingHostnameCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdatePublishButton();

    private void CreateNewHostnameCheckBox_Changed(object sender, RoutedEventArgs e) =>
        UpdateHostnameMode();

    private void UpdateHostnameMode()
    {
        if (NewHostnamePanel is null || ExistingHostnamePanel is null)
        {
            return;
        }
        var createNew = CreateNewHostnameCheckBox.IsChecked == true;
        NewHostnamePanel.Visibility = createNew ? Visibility.Visible : Visibility.Collapsed;
        ExistingHostnamePanel.Visibility = createNew ? Visibility.Collapsed : Visibility.Visible;
        UpdateHostnamePreview();
        UpdatePublishButton();
    }

    private void HostnameLabelTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateHostnamePreview();
        UpdatePublishButton();
    }

    private void DomainPoolCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateHostnamePreview();
        UpdatePublishButton();
    }

    private void UpdateHostnamePreview()
    {
        if (HostnamePreviewText is null)
        {
            return;
        }
        var label = HostnameLabelTextBox.Text.Trim().ToLowerInvariant();
        var pool = DomainPoolCombo.SelectedItem as DomainPool;
        HostnamePreviewText.Text =
            label.Length > 0 && pool is not null ? $"{label}.{pool.Domain}" : "";
    }

    private void UpdatePublishButton()
    {
        if (PublishButton is null)
        {
            return;
        }
        var hasService = LocalServicesList.SelectedItem is LocalService;
        var createNew = CreateNewHostnameCheckBox.IsChecked == true;
        var hasDestination = createNew
            ? DomainPoolCombo.SelectedItem is DomainPool
              && ValidHostnameLabel(HostnameLabelTextBox.Text)
            : ExistingHostnameCombo.SelectedItem is Subdomain;
        PublishButton.IsEnabled =
            !_busy && !_cloudflared.IsRunning && _session is not null && hasService && hasDestination;
    }

    private async void PublishButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || LocalServicesList.SelectedItem is not LocalService service)
        {
            return;
        }
        _busy = true;
        PublishButton.IsEnabled = false;
        SetStatus("Preparing the managed tunnel…");
        try
        {
            string hostname;
            string token;
            if (CreateNewHostnameCheckBox.IsChecked == true)
            {
                if (DomainPoolCombo.SelectedItem is not DomainPool pool)
                {
                    throw new InvalidOperationException("Choose a domain pool.");
                }
                var label = HostnameLabelTextBox.Text.Trim().ToLowerInvariant();
                if (!ValidHostnameLabel(label))
                {
                    throw new InvalidOperationException("Enter a valid hostname label.");
                }
                var claim = await _api.ClaimHostnameAsync(pool.Id, label, service.ServiceUrl);
                if (claim.Status == "UNDER_REVIEW")
                {
                    throw new InvalidOperationException(
                        "This hostname is awaiting a short safety review before it can be published.");
                }
                hostname = claim.Hostname;
                var credential = await _api.CreateTunnelAsync(claim.Id, service.ServiceUrl);
                token = credential.Token ?? "";
            }
            else
            {
                if (ExistingHostnameCombo.SelectedItem is not Subdomain subdomain)
                {
                    throw new InvalidOperationException("Choose a hostname.");
                }
                if (subdomain.ConnectionType != "MANAGED_TUNNEL")
                {
                    throw new InvalidOperationException(
                        "This hostname uses a direct DNS connection. Linkora Local requires a managed tunnel hostname.");
                }
                hostname = subdomain.Hostname;
                if (subdomain.ManagedTunnel is null)
                {
                    var credential = await _api.CreateTunnelAsync(subdomain.Id, service.ServiceUrl);
                    token = credential.Token ?? "";
                }
                else
                {
                    var credential = await _api.ConnectTunnelAsync(
                        subdomain.ManagedTunnel.Id,
                        service.ServiceUrl);
                    token = credential.TunnelToken ?? "";
                }
            }

            SetStatus("Starting the encrypted connector…");
            await _cloudflared.StartAsync(token);
            _publishedUrl = $"https://{hostname}";
            PublishedUrlText.Text = _publishedUrl;
            PublishedPanel.Visibility = Visibility.Visible;
            PublishButton.Visibility = Visibility.Collapsed;
            SetStatus("Connector started. Waiting for the Cloudflare edge…", true);
            _ = VerifyPublishedUrlAsync(_publishedUrl);
            await LoadWorkspaceAsync();
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, false, true);
        }
        finally
        {
            _busy = false;
            UpdatePublishButton();
        }
    }

    private async Task VerifyPublishedUrlAsync(string url)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        for (var attempt = 0; attempt < 8 && _cloudflared.IsRunning; attempt += 1)
        {
            try
            {
                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if ((int)response.StatusCode < 500)
                {
                    await Dispatcher.InvokeAsync(() =>
                        SetStatus($"{url} is online.", true));
                    return;
                }
            }
            catch
            {
                // Edge propagation can take several seconds.
            }
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
        if (_cloudflared.IsRunning)
        {
            await Dispatcher.InvokeAsync(() =>
                SetStatus("The connector is running; public edge verification is still pending."));
        }
    }

    private void OpenPublishedButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_publishedUrl))
        {
            OpenUrl(_publishedUrl);
        }
    }

    private async void StopPublishingButton_Click(object sender, RoutedEventArgs e) =>
        await StopPublishingAsync();

    public async Task StopPublishingAsync()
    {
        await _cloudflared.StopAsync();
        await Dispatcher.InvokeAsync(() =>
        {
            _publishedUrl = null;
            PublishedPanel.Visibility = Visibility.Collapsed;
            PublishButton.Visibility = Visibility.Visible;
            SetStatus("Local publishing stopped.");
            UpdatePublishButton();
        });
    }

    private void HandleCloudflaredOutput(string line)
    {
        if (line.Contains("Registered tunnel connection", StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.Invoke(() => SetStatus("Secure edge connection established.", true));
        }
        else if (line.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.Invoke(() => SetStatus("The connector reported an error. Retrying automatically."));
        }
    }

    private static bool ValidHostnameLabel(string value) =>
        Regex.IsMatch(
            value.Trim(),
            @"^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void SetStatus(string message, bool healthy = false, bool error = false)
    {
        StatusText.Text = message;
        StatusDot.Fill = new SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(
                error ? "#FF6B89" : healthy ? "#B7FF4A" : "#7F94B2"));
    }

    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _devicePollTimer.Stop();
        _api.Dispose();
        _discovery.Dispose();
        _cloudflared.DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}
