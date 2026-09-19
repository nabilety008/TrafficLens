using System.Globalization;
using System.Net;
using System.Windows.Media;
using TrafficLens.Core.Conversion;
using TrafficLens.Core.Models;

namespace TrafficLens.App.ViewModels;

/// <summary>
/// One displayed connection row (keyed by <see cref="ConnectionKey"/>: protocol,
/// family, local/remote endpoint, PID). Values are re-formatted in place on
/// every ~1 s snapshot; <see cref="SetProperty"/> short-circuits so unchanged
/// cells raise no property-changed during rapid UDP churn. Endpoint strings are
/// forced LeftToRight at the view layer and are never localized through
/// format-provider digit replacement for addresses.
/// </summary>
public sealed class ConnectionRowViewModel : ViewModelBase
{
    private string _name = string.Empty;
    private string _pidText = string.Empty;
    private string _protocolText = string.Empty;
    private string _stateText = string.Empty;
    private string _localText = string.Empty;
    private string _remoteText = string.Empty;
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

    public ImageSource? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public string DisplayName => Name;

    public void Update(ConnectionInfo connection, CultureInfo culture, ConnectionDisplayStrings display, bool force = false)
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
    }
}

/// <summary>Localized text needed to render a connection row.</summary>
public sealed record ConnectionDisplayStrings(
    string UnknownProcessText,
    string TcpText,
    string UdpText,
    Func<ConnectionState, string> StateText);