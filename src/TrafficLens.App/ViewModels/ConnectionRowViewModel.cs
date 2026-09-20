using System.Globalization;
using System.Net;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TrafficLens.App.Commands;
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

        public ConnectionRowViewModel(ConnectionKey identity)
        {
            Identity = identity;

            CopyLocalEndpointCommand = new RelayCommand(CopyLocalEndpoint);
            CopyRemoteEndpointCommand = new RelayCommand(CopyRemoteEndpoint);
            CopyRemoteIpCommand = new RelayCommand(CopyRemoteIp);
            CopyProcessNameCommand = new RelayCommand(CopyProcessName);
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

        /// <summary>
        /// Friendly hostname resolved from the remote IP (when reverse DNS is enabled).
        /// Empty when reverse DNS is disabled, resolution failed, or no remote IP.
        /// </summary>
        public string ResolvedHostname
        {
            get => _resolvedHostname;
            private set => SetProperty(ref _resolvedHostname, value);
        }

        public ImageSource? Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        public string DisplayName => Name;

        public ICommand CopyLocalEndpointCommand { get; }
        public ICommand CopyRemoteEndpointCommand { get; }
        public ICommand CopyRemoteIpCommand { get; }
        public ICommand CopyProcessNameCommand { get; }

        private void CopyLocalEndpoint()
        {
            if (!string.IsNullOrEmpty(LocalText))
            {
                Clipboard.SetText(LocalText);
            }
        }

        private void CopyRemoteEndpoint()
        {
            if (!string.IsNullOrEmpty(RemoteText))
            {
                Clipboard.SetText(RemoteText);
            }
        }

        private void CopyRemoteIp()
        {
            if (_remoteAddress is not null)
            {
                Clipboard.SetText(_remoteAddress.ToString());
            }
        }

        private void CopyProcessName()
        {
            if (!string.IsNullOrEmpty(Name))
            {
                Clipboard.SetText(Name);
            }
        }

        /// <summary>
        /// Updates the resolved hostname from a background DNS lookup.
        /// Called by ConnectionsViewModel when reverse DNS is enabled and lookup completes.
        /// </summary>
        internal void SetResolvedHostname(string hostname)
        {
            ResolvedHostname = hostname;
        }

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