using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using TrafficLens.App.Services;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Localization;
using TrafficLens.Core.Models;
using TrafficLens.Core.Selection;

namespace TrafficLens.App.ViewModels;

/// <summary>
/// Active Connections page. Consumes only the <see cref="IConnectionProvider"/>
/// abstraction: subscribes to ~1 s snapshots of current connections (never raw
/// native rows), marshals them to the WPF dispatcher, and renders a
/// filtered/searched/sorted view keyed by stable <see cref="ConnectionKey"/>.
/// Filtering, searching and sorting are pure <see cref="ConnectionFiltering"/> /
/// <see cref="ConnectionSort"/> operations over the snapshot and never mutate
/// provider state. Rows are replaced or removed only when the key set or order
/// actually changes, so UDP churn does not churn the collection. Provider
/// failures surface as a banner and never crash the app or other pages.
/// </summary>
public sealed class ConnectionsViewModel : ViewModelBase, IDisposable
{
    private readonly IConnectionProvider _provider;
    private readonly ILocalizationService _localization;
    private readonly ProcessIconResolver _iconResolver;
    private readonly Dispatcher? _dispatcher;
    private readonly Dictionary<ConnectionKey, ConnectionRowViewModel> _rows = new();
    private readonly List<ConnectionKey> _displayedKeys = new();

    private IReadOnlyList<ConnectionInfo> _connections = Array.Empty<ConnectionInfo>();
    private CultureInfo _culture;

    private string _searchText = string.Empty;
    private ConnectionFilter _filter = ConnectionFilter.All;
    private ConnectionFilter _familyFilter = ConnectionFilter.All;
    private ConnectionSortKey _sortKey = ConnectionSortKey.Default;

    private string _connectionsLabel = string.Empty;
    private string _protocolLabel = string.Empty;
    private string _stateLabel = string.Empty;
    private string _localEndpointLabel = string.Empty;
    private string _remoteEndpointLabel = string.Empty;
    private string _showLabel = string.Empty;
    private string _addressFamilyLabel = string.Empty;
    private string _sortByLabel = string.Empty;
    private string _searchPlaceholder = string.Empty;
    private string _noActiveConnectionsText = string.Empty;
    private string _errorTitle = string.Empty;
    private string _errorDetail = string.Empty;
    private bool _hasError;
    private string _processLabel = string.Empty;
    private string _pidLabel = string.Empty;
    private string _unknownProcessText = string.Empty;
    private string _tcpText = string.Empty;
    private string _udpText = string.Empty;
    private ConnectionDisplayStrings? _displayStrings;
    private readonly Dictionary<ConnectionState, string> _stateTexts = new();
    private bool _isEmpty = true;

    public ConnectionsViewModel(
        IConnectionProvider provider,
        ILocalizationService localization,
        ProcessIconResolver iconResolver)
    {
        _provider = provider;
        _localization = localization;
        _iconResolver = iconResolver;
        _dispatcher = Application.Current?.Dispatcher;
        _culture = localization.CurrentCulture;

        _provider.ConnectionsChanged += OnConnectionsChanged;
        _localization.CultureChanged += OnCultureChanged;

        RefreshLocalizedStrings();
        UpdateErrorState();
        RefreshConnections(_provider.GetCurrentConnections());
    }

    public ObservableCollection<ConnectionRowViewModel> Connections { get; } = new();

    public ObservableCollection<ConnectionFilterOption> FilterOptions { get; } = new();

    public ObservableCollection<ConnectionFilterOption> FamilyFilterOptions { get; } = new();

    public ObservableCollection<ConnectionSortOption> SortOptions { get; } = new();

    public void Dispose()
    {
        _provider.ConnectionsChanged -= OnConnectionsChanged;
        _localization.CultureChanged -= OnCultureChanged;
    }

    public IEnumerable<ConnectionRowViewModel> Rows => _rows.Values;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RebuildDisplayList(force: true);
            }
        }
    }

    public ConnectionFilter Filter
    {
        get => _filter;
        set
        {
            if (SetProperty(ref _filter, value))
            {
                RebuildDisplayList(force: true);
            }
        }
    }

    public ConnectionFilter FamilyFilter
    {
        get => _familyFilter;
        set
        {
            if (SetProperty(ref _familyFilter, value))
            {
                RebuildDisplayList(force: true);
            }
        }
    }

    public ConnectionSortKey SortKey
    {
        get => _sortKey;
        set
        {
            if (SetProperty(ref _sortKey, value))
            {
                RebuildDisplayList(force: true);
            }
        }
    }

    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    public string ErrorTitle
    {
        get => _errorTitle;
        private set => SetProperty(ref _errorTitle, value);
    }

    public string ErrorDetail
    {
        get => _errorDetail;
        private set => SetProperty(ref _errorDetail, value);
    }

    public string ConnectionsLabel
    {
        get => _connectionsLabel;
        private set => SetProperty(ref _connectionsLabel, value);
    }

    public string ProtocolLabel
    {
        get => _protocolLabel;
        private set => SetProperty(ref _protocolLabel, value);
    }

    public string StateLabel
    {
        get => _stateLabel;
        private set => SetProperty(ref _stateLabel, value);
    }

    public string LocalEndpointLabel
    {
        get => _localEndpointLabel;
        private set => SetProperty(ref _localEndpointLabel, value);
    }

    public string RemoteEndpointLabel
    {
        get => _remoteEndpointLabel;
        private set => SetProperty(ref _remoteEndpointLabel, value);
    }

    public string ProcessLabel
    {
        get => _processLabel;
        private set => SetProperty(ref _processLabel, value);
    }

    public string PidLabel
    {
        get => _pidLabel;
        private set => SetProperty(ref _pidLabel, value);
    }

    public string ShowLabel
    {
        get => _showLabel;
        private set => SetProperty(ref _showLabel, value);
    }

    public string AddressFamilyLabel
    {
        get => _addressFamilyLabel;
        private set => SetProperty(ref _addressFamilyLabel, value);
    }

    public string SortByLabel
    {
        get => _sortByLabel;
        private set => SetProperty(ref _sortByLabel, value);
    }

    public string SearchPlaceholder
    {
        get => _searchPlaceholder;
        private set => SetProperty(ref _searchPlaceholder, value);
    }

    public string NoActiveConnectionsText
    {
        get => _noActiveConnectionsText;
        private set => SetProperty(ref _noActiveConnectionsText, value);
    }

    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    public bool IsNotEmpty
    {
        get => !_isEmpty;
    }

    private void OnConnectionsChanged(object? sender, IReadOnlyList<ConnectionInfo> connections) =>
        RunOnUi(() => RefreshConnections(connections));

    private void OnCultureChanged(object? sender, EventArgs e) =>
        RunOnUi(() =>
        {
            RefreshLocalizedStrings();
            RefreshConnections(_connections);
        });

    private void RunOnUi(Action action)
    {
        if (_dispatcher is null || _dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _dispatcher.InvokeAsync(action);
    }

    private void RefreshLocalizedStrings()
    {
        _culture = _localization.CurrentCulture;

        ConnectionsLabel = _localization["ConnectionsLabel"];
        ProtocolLabel = _localization["ProtocolLabel"];
        StateLabel = _localization["StateLabel"];
        LocalEndpointLabel = _localization["LocalEndpointLabel"];
        RemoteEndpointLabel = _localization["RemoteEndpointLabel"];
        ProcessLabel = _localization["ProcessLabel"];
        PidLabel = _localization["PidLabel"];
        ShowLabel = _localization["ShowLabel"];
        AddressFamilyLabel = _localization["AddressFamilyLabel"];
        SortByLabel = _localization["SortByLabel"];
        SearchPlaceholder = _localization["SearchConnectionsPlaceholder"];
        NoActiveConnectionsText = _localization["NoActiveConnectionsLabel"];
        _errorTitle = _localization["ConnectionErrorLabel"];
        _unknownProcessText = _localization["UnknownProcessLabel"];
        _tcpText = _localization["TcpLabel"];
        _udpText = _localization["UdpLabel"];

        _stateTexts[ConnectionState.Closed] = _localization["StateClosedLabel"];
        _stateTexts[ConnectionState.Listen] = _localization["StateListenLabel"];
        _stateTexts[ConnectionState.SynSent] = _localization["StateSynSentLabel"];
        _stateTexts[ConnectionState.SynReceived] = _localization["StateSynReceivedLabel"];
        _stateTexts[ConnectionState.Established] = _localization["StateEstablishedLabel"];
        _stateTexts[ConnectionState.FinWait1] = _localization["StateFinWait1Label"];
        _stateTexts[ConnectionState.FinWait2] = _localization["StateFinWait2Label"];
        _stateTexts[ConnectionState.CloseWait] = _localization["StateCloseWaitLabel"];
        _stateTexts[ConnectionState.Closing] = _localization["StateClosingLabel"];
        _stateTexts[ConnectionState.LastAck] = _localization["StateLastAckLabel"];
        _stateTexts[ConnectionState.TimeWait] = _localization["StateTimeWaitLabel"];
        _stateTexts[ConnectionState.Unknown] = string.Empty;

        _displayStrings = new ConnectionDisplayStrings(
            _unknownProcessText,
            _tcpText,
            _udpText,
            state => _stateTexts.TryGetValue(state, out var text) ? text : string.Empty);

        FilterOptions.Clear();
        FilterOptions.Add(new ConnectionFilterOption(ConnectionFilter.All, _localization["AllLabel"]));
        FilterOptions.Add(new ConnectionFilterOption(ConnectionFilter.Established, _localization["EstablishedLabel"]));
        FilterOptions.Add(new ConnectionFilterOption(ConnectionFilter.Listening, _localization["ListeningLabel"]));
        FilterOptions.Add(new ConnectionFilterOption(ConnectionFilter.Tcp, _localization["TcpLabel"]));
        FilterOptions.Add(new ConnectionFilterOption(ConnectionFilter.Udp, _localization["UdpLabel"]));

        FamilyFilterOptions.Clear();
        FamilyFilterOptions.Add(new ConnectionFilterOption(ConnectionFilter.All, _localization["AllLabel"]));
        FamilyFilterOptions.Add(new ConnectionFilterOption(ConnectionFilter.Ipv4, _localization["Ipv4Label"]));
        FamilyFilterOptions.Add(new ConnectionFilterOption(ConnectionFilter.Ipv6, _localization["Ipv6Label"]));

        SortOptions.Clear();
        SortOptions.Add(new ConnectionSortOption(ConnectionSortKey.Default, _localization["SortDefaultLabel"]));
        SortOptions.Add(new ConnectionSortOption(ConnectionSortKey.Process, _localization["SortProcessLabel"]));
        SortOptions.Add(new ConnectionSortOption(ConnectionSortKey.ProcessId, _localization["SortPidLabel"]));
        SortOptions.Add(new ConnectionSortOption(ConnectionSortKey.Protocol, _localization["SortProtocolLabel"]));
        SortOptions.Add(new ConnectionSortOption(ConnectionSortKey.State, _localization["SortStateLabel"]));
        SortOptions.Add(new ConnectionSortOption(ConnectionSortKey.Local, _localization["SortLocalLabel"]));
        SortOptions.Add(new ConnectionSortOption(ConnectionSortKey.Remote, _localization["SortRemoteLabel"]));
    }

    private void UpdateErrorState()
    {
        var error = _provider.LastError;
        ErrorDetail = string.IsNullOrWhiteSpace(error) ? string.Empty : error;
        HasError = !string.IsNullOrWhiteSpace(ErrorDetail);
        if (HasError)
        {
            ErrorTitle = _errorTitle;
        }
    }

    private void RefreshConnections(IReadOnlyList<ConnectionInfo> connections)
    {
        _connections = connections;
        UpdateErrorState();

        var seen = new HashSet<ConnectionKey>(connections.Count);
        foreach (var connection in connections)
        {
            var key = ConnectionKey.From(connection);
            seen.Add(key);

            if (!_rows.TryGetValue(key, out var row))
            {
                row = new ConnectionRowViewModel(key);
                _rows[key] = row;
            }

            if (_displayStrings is not null)
            {
                row.Update(connection, _culture, _displayStrings);
            }
        }

        if (seen.Count != _rows.Count)
        {
            foreach (var key in _rows.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                _rows.Remove(key);
            }
        }

        ResolveIcons();
        RebuildDisplayList(force: false);
    }

    private void ResolveIcons()
    {
        var budget = ProcessIconResolver.MaxExtractionsPerRefresh;
        foreach (var row in _rows.Values)
        {
            if (row.Icon is not null)
            {
                continue;
            }

            if (budget <= 0)
            {
                break;
            }

            budget--;
            row.Icon = _iconResolver.GetOrExtract(row.ExecutablePath, row.IconAvailable);
        }
    }

    private void RebuildDisplayList(bool force)
    {
        IEnumerable<ConnectionInfo> source = _connections
            .Where(c => ConnectionFiltering.Matches(c, _filter))
            .Where(c => ConnectionFiltering.Matches(c, _familyFilter));

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var query = _searchText.Trim();
            source = source.Where(c =>
                ConnectionFiltering.MatchesSearch(c, query, DisplayName(c)));
        }

        var ordered = source
            .OrderBy(c => c, ConnectionSort.Create(_sortKey))
            .ToList();

        if (!force && SameKeys(ordered, _displayedKeys))
        {
            return;
        }

        Connections.Clear();
        _displayedKeys.Clear();
        foreach (var connection in ordered)
        {
            var key = ConnectionKey.From(connection);
            if (_rows.TryGetValue(key, out var row))
            {
                Connections.Add(row);
                _displayedKeys.Add(key);
            }
        }

        IsEmpty = Connections.Count == 0;
        OnPropertyChanged(nameof(IsNotEmpty));
    }

    private string DisplayName(ConnectionInfo connection) =>
        string.IsNullOrWhiteSpace(connection.ProcessName)
            ? _unknownProcessText
            : connection.ProcessName;

    private static bool SameKeys(
        IReadOnlyList<ConnectionInfo> connections,
        IReadOnlyList<ConnectionKey> keys)
    {
        if (connections.Count != keys.Count)
        {
            return false;
        }

        for (var i = 0; i < connections.Count; i++)
        {
            if (!ConnectionKey.From(connections[i]).Equals(keys[i]))
            {
                return false;
            }
        }

        return true;
    }
}