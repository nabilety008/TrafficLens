using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media;
using TrafficLens.Core.Conversion;
using TrafficLens.Core.Models;
using TrafficLens.WinUI.Infrastructure;
using Windows.ApplicationModel.DataTransfer;

namespace TrafficLens.WinUI.ViewModels;

public sealed class ConnectionRowViewModel : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _pidText = string.Empty;
    private string _protocolText = string.Empty;
    private string _stateText = string.Empty;
    private string _localText = string.Empty;
    private string _remoteText = string.Empty;
    private string _resolvedHostname = string.Empty;
    private ImageSource? _icon;
    private bool _initialized;
    private string? _processName;
    private int _processId;
    private ConnectionProtocol _protocol;
    private ConnectionState _state;
    private IPAddress? _localAddress;
    private int _localPort;
    private IPAddress? _remoteAddress;
    private int? _remotePort;
    private string? _executablePath;
    private bool _iconAvailable;
    private string? _iconPath;
    private bool _iconCacheAvailable;

    public ConnectionRowViewModel(ConnectionKey identity)
    {
        Identity = identity;
    }

    public ConnectionKey Identity { get; }

    public string? ExecutablePath { get; private set; }

    public bool IconAvailable { get; private set; }

    public string Name
    {
        get => _name;
        private set => SetProperty(ref _name, value);
    }

    public string PidText
    {
        get => _pidText;
        private set => SetProperty(ref _pidText, value);
    }

    public string ProtocolText
    {
        get => _protocolText;
        private set => SetProperty(ref _protocolText, value);
    }

    public string StateText
    {
        get => _stateText;
        private set => SetProperty(ref _stateText, value);
    }

    public string LocalText
    {
        get => _localText;
        private set => SetProperty(ref _localText, value);
    }

    public string RemoteText
    {
        get => _remoteText;
        private set => SetProperty(ref _remoteText, value);
    }

    public string ResolvedHostname
    {
        get => _resolvedHostname;
        private set => SetProperty(ref _resolvedHostname, value);
    }

    public ImageSource? Icon
    {
        get => _icon;
        private set => SetProperty(ref _icon, value);
    }

    public string DisplayName => Name;

    public void CopyLocalEndpoint() => CopyText(LocalText);

    public void CopyRemoteEndpoint() => CopyText(RemoteText);

    public void CopyRemoteIp()
    {
        if (_remoteAddress is not null)
        {
            CopyText(_remoteAddress.ToString());
        }
    }

    public void CopyProcessName() => CopyText(Name);

    internal void SetResolvedHostname(string hostname) => ResolvedHostname = hostname;

    public void Update(
        ConnectionInfo connection,
        CultureInfo culture,
        ConnectionDisplayStrings display,
        ProcessIconCache icons,
        bool force = false)
    {
        if (!force && _initialized
            && string.Equals(_processName, connection.ProcessName, StringComparison.Ordinal)
            && _processId == connection.ProcessId
            && _protocol == connection.Protocol
            && _state == connection.State
            && Equals(_localAddress, connection.LocalAddress)
            && _localPort == connection.LocalPort
            && Equals(_remoteAddress, connection.RemoteAddress)
            && _remotePort == connection.RemotePort
            && string.Equals(_executablePath, connection.ExecutablePath, StringComparison.Ordinal)
            && _iconAvailable == connection.IconAvailable)
        {
            return;
        }

        _initialized = true;
        _processName = connection.ProcessName;
        _processId = connection.ProcessId;
        _protocol = connection.Protocol;
        _state = connection.State;
        _localAddress = connection.LocalAddress;
        _localPort = connection.LocalPort;
        _remoteAddress = connection.RemoteAddress;
        _remotePort = connection.RemotePort;
        _executablePath = connection.ExecutablePath;
        _iconAvailable = connection.IconAvailable;

        Name = string.IsNullOrWhiteSpace(connection.ProcessName)
            ? display.UnknownProcessText
            : connection.ProcessName;
        PidText = connection.ProcessId.ToString(culture);
        ProtocolText = connection.Protocol switch
        {
            ConnectionProtocol.Tcp => display.TcpText,
            ConnectionProtocol.Udp => display.UdpText,
            _ => string.Empty
        };
        StateText = connection.Protocol == ConnectionProtocol.Udp
            ? "—"
            : display.StateText(connection.State);
        LocalText = EndpointFormatter.Format(connection.LocalAddress, connection.LocalPort);
        RemoteText = EndpointFormatter.FormatRemote(connection.RemoteAddress, connection.RemotePort);
        ExecutablePath = connection.ExecutablePath;
        IconAvailable = connection.IconAvailable;

        UpdateIcon(connection, icons);
    }

    public void OnIconReady(ProcessIconCache icons)
    {
        var image = icons.TryGet(_iconPath, _iconCacheAvailable);
        if (image is not null)
        {
            Icon = image;
        }
    }

    private void UpdateIcon(ConnectionInfo connection, ProcessIconCache icons)
    {
        var path = connection.ExecutablePath;
        var available = connection.IconAvailable && !string.IsNullOrWhiteSpace(path);

        var pathChanged = !string.Equals(path, _iconPath, StringComparison.OrdinalIgnoreCase);
        var availableChanged = available != _iconCacheAvailable;

        _iconPath = path;
        _iconCacheAvailable = available;

        if (!pathChanged && !availableChanged && Icon is not null)
        {
            return;
        }

        var cached = icons.TryGet(path, available);
        if (cached is not null)
        {
            Icon = cached;
            return;
        }

        icons.Request(path, available);
    }

    private static void CopyText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var dataPackage = new DataPackage();
        dataPackage.SetText(text);
        Clipboard.SetContent(dataPackage);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed record ConnectionDisplayStrings(
    string UnknownProcessText,
    string TcpText,
    string UdpText,
    Func<ConnectionState, string> StateText);
