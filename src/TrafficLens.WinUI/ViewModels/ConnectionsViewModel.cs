using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Localization;
using TrafficLens.Core.Models;
using TrafficLens.Core.Selection;
using TrafficLens.WinUI.Infrastructure;

namespace TrafficLens.WinUI.ViewModels;

public sealed class ConnectionsViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IConnectionProvider _provider;
    private readonly ILocalizationService _localization;
    private readonly ISettingsService _settings;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ProcessIconCache _iconCache;
    private readonly Dictionary<ConnectionKey, ConnectionRowViewModel> _rows = new();
    private readonly List<ConnectionKey> _displayedKeys = new();

    private IReadOnlyList<ConnectionInfo> _connections = Array.Empty<ConnectionInfo>();
    private CultureInfo _culture;

    private string _searchText = string.Empty;
    private ConnectionFilter _filter = ConnectionFilter.All;
    private ConnectionFilter _familyFilter = ConnectionFilter.All;
    private ConnectionSortKey _sortKey = ConnectionSortKey.Default;
    private bool _hideListeners;

    public const string HideListenersKey = "ConnectionsHideListeners";

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
    private string _connectionErrorDetailText = string.Empty;
    private bool _hasError;
    private string _hideListenersLabel = string.Empty;
    private string _processLabel = string.Empty;
    private string _pidLabel = string.Empty;
    private string _copyLocalEndpointLabel = string.Empty;
    private string _copyRemoteEndpointLabel = string.Empty;
    private string _copyRemoteIpLabel = string.Empty;
    private string _copyProcessNameLabel = string.Empty;
    private string _unknownProcessText = string.Empty;
    private string _tcpText = string.Empty;
    private string _udpText = string.Empty;
    private ConnectionDisplayStrings? _displayStrings;
    private readonly Dictionary<ConnectionState, string> _stateTexts = new();
    private bool _isEmpty = true;
    private bool _isActive;
    private bool _refreshPending;
    private bool _disposed;
    private IReadOnlyList<ConnectionInfo> _pendingConnections = Array.Empty<ConnectionInfo>();

    public ConnectionsViewModel(
        IConnectionProvider provider,
        ILocalizationService localization,
        ISettingsService settings,
        DispatcherQueue dispatcherQueue,
        ProcessIconCache iconCache)
    {
        _provider = provider;
        _localization = localization;
        _settings = settings;
        _dispatcherQueue = dispatcherQueue;
        _iconCache = iconCache;
        _culture = localization.CurrentCulture;

        _hideListeners = GetBool(_settings, HideListenersKey, defaultValue: false);

        _provider.ConnectionsChanged += OnConnectionsChanged;
        _localization.CultureChanged += OnCultureChanged;
        _iconCache.IconReady += OnIconReady;

        RefreshLocalizedStrings();
        UpdateErrorState();
        RefreshConnections(_provider.GetCurrentConnections());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ConnectionRowViewModel> Connections { get; } = new();

    public ObservableCollection<ConnectionFilterOption> FilterOptions { get; } = new();

    public ObservableCollection<ConnectionFilterOption> FamilyFilterOptions { get; } = new();

    public ObservableCollection<ConnectionSortOption> SortOptions { get; } = new();

    public IEnumerable<ConnectionRowViewModel> Rows => _rows.Values;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _isActive = false;

        _provider.ConnectionsChanged -= OnConnectionsChanged;
        _localization.CultureChanged -= OnCultureChanged;
        _iconCache.IconReady -= OnIconReady;
    }

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
                OnPropertyChanged(nameof(FilterIndex));
                RebuildDisplayList(force: true);
            }
        }
    }

    /// <summary>
    /// ComboBox selection as a position in the fixed Show list. SelectedValue
    /// with a rebuilt item collection drops the visual selection, because the
    /// re-announced enum value cannot resolve while the list is being replaced;
    /// an index survives the same rebuild because the list order is fixed.
    /// </summary>
    public int FilterIndex
    {
        get => IndexOf(FilterOptionKeys, _filter);
        set => Filter = FilterOptionKeys[value];
    }

    public ConnectionFilter FamilyFilter
    {
        get => _familyFilter;
        set
        {
            if (SetProperty(ref _familyFilter, value))
            {
                OnPropertyChanged(nameof(FamilyFilterIndex));
                RebuildDisplayList(force: true);
            }
        }
    }

    /// <summary>ComboBox selection as a position in the fixed Address Family list.</summary>
    public int FamilyFilterIndex
    {
        get => IndexOf(FamilyOptionKeys, _familyFilter);
        set => FamilyFilter = FamilyOptionKeys[value];
    }

    public ConnectionSortKey SortKey
    {
        get => _sortKey;
        set
        {
            if (SetProperty(ref _sortKey, value))
            {
                OnPropertyChanged(nameof(SortIndex));
                RebuildDisplayList(force: true);
            }
        }
    }

    /// <summary>ComboBox selection as a position in the fixed Sort by list.</summary>
    public int SortIndex
    {
        get => IndexOf(SortOptionKeys, _sortKey);
        set => SortKey = SortOptionKeys[value];
    }

    private static int IndexOf<T>(IReadOnlyList<T> list, T value)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(list[i], value))
            {
                return i;
            }
        }

        return 0;
    }

    private static readonly IReadOnlyList<ConnectionFilter> FilterOptionKeys = new[]
    {
        ConnectionFilter.All,
        ConnectionFilter.Established,
        ConnectionFilter.Listening,
        ConnectionFilter.Tcp,
        ConnectionFilter.Udp
    };

    private static readonly IReadOnlyList<ConnectionFilter> FamilyOptionKeys = new[]
    {
        ConnectionFilter.All,
        ConnectionFilter.Ipv4,
        ConnectionFilter.Ipv6
    };

    private static readonly IReadOnlyList<ConnectionSortKey> SortOptionKeys = new[]
    {
        ConnectionSortKey.Default,
        ConnectionSortKey.Process,
        ConnectionSortKey.ProcessId,
        ConnectionSortKey.Protocol,
        ConnectionSortKey.State,
        ConnectionSortKey.Local,
        ConnectionSortKey.Remote
    };

    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    public bool HideListeners
    {
        get => _hideListeners;
        set
        {
            if (SetProperty(ref _hideListeners, value))
            {
                _settings.Set(HideListenersKey, value.ToString());
                _settings.Save();
                RebuildDisplayList(force: true);
            }
        }
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

    public string CopyLocalEndpointLabel
    {
        get => _copyLocalEndpointLabel;
        private set => SetProperty(ref _copyLocalEndpointLabel, value);
    }

    public string CopyRemoteEndpointLabel
    {
        get => _copyRemoteEndpointLabel;
        private set => SetProperty(ref _copyRemoteEndpointLabel, value);
    }

    public string CopyRemoteIpLabel
    {
        get => _copyRemoteIpLabel;
        private set => SetProperty(ref _copyRemoteIpLabel, value);
    }

    public string CopyProcessNameLabel
    {
        get => _copyProcessNameLabel;
        private set => SetProperty(ref _copyProcessNameLabel, value);
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

    public string HideListenersLabel
    {
        get => _hideListenersLabel;
        private set => SetProperty(ref _hideListenersLabel, value);
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
        private set
        {
            if (SetProperty(ref _isEmpty, value))
            {
                OnPropertyChanged(nameof(IsNotEmpty));
            }
        }
    }

    public bool IsNotEmpty => !_isEmpty;

    public void SetActive(bool active)
    {
        _isActive = active;
        _provider.SetPollingEnabled(active);
        if (!active)
        {
            return;
        }

        _pendingConnections = _provider.GetCurrentConnections();
        RefreshConnections(_pendingConnections);
    }

    private void OnConnectionsChanged(object? sender, IReadOnlyList<ConnectionInfo> connections)
    {
        if (!_isActive || _disposed)
        {
            _pendingConnections = connections;
            return;
        }

        CoalesceRefresh(() => RefreshConnections(connections));
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        RunOnUi(() =>
        {
            RefreshLocalizedStrings();
            RefreshConnections(_connections, force: true);
        });
    }

    private void OnIconReady(object? sender, string path)
    {
        if (!_isActive || _disposed)
        {
            return;
        }

        RunOnUi(() =>
        {
            foreach (var row in _rows.Values)
            {
                row.OnIconReady(_iconCache);
            }
        });
    }

    private void CoalesceRefresh(Action action)
    {
        if (_refreshPending || _disposed)
        {
            return;
        }

        _refreshPending = true;

        if (_dispatcherQueue.HasThreadAccess)
        {
            _refreshPending = false;
            if (_isActive && !_disposed)
            {
                action();
            }

            return;
        }

        _dispatcherQueue.TryEnqueue(() =>
        {
            _refreshPending = false;
            if (_isActive && !_disposed)
            {
                action();
            }
        });
    }

    private void RunOnUi(Action action)
    {
        if (_disposed)
        {
            return;
        }

        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        _dispatcherQueue.TryEnqueue(() =>
        {
            if (!_disposed)
            {
                action();
            }
        });
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
        CopyLocalEndpointLabel = _localization["CopyLocalEndpointLabel"];
        CopyRemoteEndpointLabel = _localization["CopyRemoteEndpointLabel"];
        CopyRemoteIpLabel = _localization["CopyRemoteIpLabel"];
        CopyProcessNameLabel = _localization["CopyProcessNameLabel"];
        ShowLabel = _localization["ShowLabel"];
        AddressFamilyLabel = _localization["AddressFamilyLabel"];
        SortByLabel = _localization["SortByLabel"];
        SearchPlaceholder = _localization["SearchConnectionsPlaceholder"];
        NoActiveConnectionsText = _localization["NoActiveConnectionsLabel"];
        _errorTitle = _localization["ConnectionErrorLabel"];
        _connectionErrorDetailText = _localization["ConnectionsErrorDetailLabel"];
        _unknownProcessText = _localization["UnknownProcessLabel"];
        _tcpText = _localization["TcpLabel"];
        _udpText = _localization["UdpLabel"];
        HideListenersLabel = _localization["HideListenersLabel"];

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

        // The option collections were just cleared and repopulated, which drops the
        // ComboBox selection to null even though the view-model value never changed.
        // Re-raising the change notifications makes the bindings re-resolve
        // SelectedValue against the new lists, so the visual selection survives a
        // culture switch.
        OnPropertyChanged(nameof(Filter));
        OnPropertyChanged(nameof(FamilyFilter));
        OnPropertyChanged(nameof(SortKey));
    }

    private void UpdateErrorState()
    {
        var error = _provider.LastError;
        HasError = !string.IsNullOrWhiteSpace(error);
        ErrorDetail = HasError ? _connectionErrorDetailText : string.Empty;
        if (HasError)
        {
            ErrorTitle = _errorTitle;
        }
    }

    private void RefreshConnections(IReadOnlyList<ConnectionInfo> connections, bool force = false)
    {
        _connections = connections;
        UpdateErrorState();
        _iconCache.BeginRefresh();

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
                row.Update(connection, _culture, _displayStrings, _iconCache, force);
            }
        }

        if (seen.Count != _rows.Count)
        {
            foreach (var key in _rows.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                _rows.Remove(key);
            }
        }

        RebuildDisplayList(force: false);
    }

    private void RebuildDisplayList(bool force)
    {
        IEnumerable<ConnectionInfo> source = _connections
            .Where(c => ConnectionFiltering.Matches(c, _filter))
            .Where(c => ConnectionFiltering.Matches(c, _familyFilter));

        if (_hideListeners)
        {
            source = source.Where(c => ConnectionFiltering.Matches(c, ConnectionFilter.HideListeners));
        }

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var query = _searchText.Trim();
            source = source.Where(c =>
                ConnectionFiltering.MatchesSearch(c, query, DisplayName(c)));
        }

        var ordered = source
            .OrderBy(c => c, ConnectionSort.Create(_sortKey))
            .ToList();

        var orderedKeys = new ConnectionKey[ordered.Count];
        for (var i = 0; i < ordered.Count; i++)
        {
            orderedKeys[i] = ConnectionKey.From(ordered[i]);
        }

        if (!force && SameKeys(orderedKeys, _displayedKeys))
        {
            return;
        }

        ApplyDisplayKeys(ordered, orderedKeys);
    }

    private void ApplyDisplayKeys(
        IReadOnlyList<ConnectionInfo> ordered,
        IReadOnlyList<ConnectionKey> orderedKeys)
    {
        var target = new List<ConnectionRowViewModel>(ordered.Count);
        var targetKeys = new List<ConnectionKey>(orderedKeys.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            if (_rows.TryGetValue(orderedKeys[i], out var row))
            {
                target.Add(row);
                targetKeys.Add(orderedKeys[i]);
            }
        }

        var present = new HashSet<ConnectionRowViewModel>(target);

        for (var i = Connections.Count - 1; i >= 0; i--)
        {
            if (!present.Contains(Connections[i]))
            {
                Connections.RemoveAt(i);
            }
        }

        var expected = 0;
        foreach (var row in target)
        {
            var current = -1;
            for (var j = expected; j < Connections.Count; j++)
            {
                if (ReferenceEquals(Connections[j], row))
                {
                    current = j;
                    break;
                }
            }

            if (current == -1)
            {
                Connections.Insert(expected, row);
            }
            else if (current != expected)
            {
                Connections.Move(current, expected);
            }

            expected++;
        }

        while (Connections.Count > target.Count)
        {
            Connections.RemoveAt(Connections.Count - 1);
        }

        _displayedKeys.Clear();
        _displayedKeys.AddRange(targetKeys);

        IsEmpty = Connections.Count == 0;
    }

    private string DisplayName(ConnectionInfo connection) =>
        string.IsNullOrWhiteSpace(connection.ProcessName)
            ? _unknownProcessText
            : connection.ProcessName;

    private static bool SameKeys(
        IReadOnlyList<ConnectionKey> keys,
        IReadOnlyList<ConnectionKey> expected)
    {
        if (keys.Count != expected.Count)
        {
            return false;
        }

        for (var i = 0; i < keys.Count; i++)
        {
            if (!keys[i].Equals(expected[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool GetBool(ISettingsService settings, string key, bool defaultValue)
    {
        var value = settings.Get(key, string.Empty);
        return string.IsNullOrEmpty(value) ? defaultValue : bool.TryParse(value, out var parsed) && parsed;
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
