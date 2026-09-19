using System.ComponentModel;
using System.Globalization;
using System.Net;
using TrafficLens.App.ViewModels;
using TrafficLens.Core.Models;

namespace TrafficLens.App.Tests;

public sealed class ConnectionRowViewModelTests
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    private static ConnectionDisplayStrings Display() => new(
        UnknownProcessText: "Unknown",
        TcpText: "TCP",
        UdpText: "UDP",
        StateText: s => s.ToString());

    private static ConnectionInfo Tcp(string name = "chrome", ConnectionState state = ConnectionState.Established) =>
        new(42, name, ConnectionProtocol.Tcp, ConnectionAddressFamily.Ipv4,
            IPAddress.Parse("10.0.0.1"), 100, IPAddress.Parse("8.8.8.8"), 53, state);

    [Fact]
    public void Update_FirstCall_FormatsAllCellsAndRaisesNotifications()
    {
        var row = NewRow();
        var raised = PropertyChangedRaised(row, r => r.Update(Tcp(), EnUs, Display()));

        Assert.Equal("chrome", row.Name);
        Assert.Equal("42", row.PidText);
        Assert.Equal("TCP", row.ProtocolText);
        Assert.Equal("Established", row.StateText);
        Assert.Equal("10.0.0.1:100", row.LocalText);
        Assert.Equal("8.8.8.8:53", row.RemoteText);
        Assert.True(raised.Count > 0);
    }

    [Fact]
    public void Update_UnchangedRawValues_SkipsReformattingAndRaisesNothing()
    {
        var row = NewRow();
        row.Update(Tcp(), EnUs, Display());

        var raised = PropertyChangedRaised(row, r => r.Update(Tcp(), EnUs, Display()));

        Assert.Empty(raised);
        Assert.Equal("10.0.0.1:100", row.LocalText);
        Assert.Equal("8.8.8.8:53", row.RemoteText);
        Assert.Equal("Established", row.StateText);
    }

    [Fact]
    public void Update_UnchangedStateButNewRemotePort_RaisesStateAndReformatsPeer()
    {
        var row = NewRow();
        row.Update(Tcp(), EnUs, Display());

        var changed = Tcp() with { RemotePort = 443 };
        var raised = PropertyChangedRaised(row, r => r.Update(changed, EnUs, Display()));

        Assert.Equal("8.8.8.8:443", row.RemoteText);
        Assert.Contains(nameof(ConnectionRowViewModel.RemoteText), raised);
        Assert.DoesNotContain(nameof(ConnectionRowViewModel.StateText), raised);
    }

    [Fact]
    public void Update_Force_ReformatsUnderDifferentLocalizedStrings()
    {
        var row = NewRow();
        row.Update(Tcp(name: null!), EnUs, Display());
        Assert.Equal("Unknown", row.Name);

        var spanish = new ConnectionDisplayStrings(
            UnknownProcessText: "Desconocido",
            TcpText: "TCP",
            UdpText: "UDP",
            StateText: s => s.ToString());
        var raised = PropertyChangedRaised(
            row, r => r.Update(Tcp(name: null!), EnUs, spanish, force: true));

        Assert.Equal("Desconocido", row.Name);
        Assert.Contains(nameof(ConnectionRowViewModel.Name), raised);
    }

    [Fact]
    public void Update_NullToPresentProcessName_RaisesName()
    {
        var row = NewRow();
        row.Update(Tcp(name: null!), EnUs, Display());
        Assert.Equal("Unknown", row.Name);

        var raised = PropertyChangedRaised(row, r => r.Update(Tcp(name: "svc"), EnUs, Display()));

        Assert.Equal("svc", row.Name);
        Assert.Contains(nameof(ConnectionRowViewModel.Name), raised);
    }

    private static ConnectionRowViewModel NewRow()
    {
        var key = new ConnectionKey(
            ConnectionProtocol.Tcp,
            ConnectionAddressFamily.Ipv4,
            IPAddress.Parse("10.0.0.1"),
            100,
            IPAddress.Parse("8.8.8.8"),
            53,
            42);
        return new ConnectionRowViewModel(key);
    }

    private static List<string> PropertyChangedRaised(
        ConnectionRowViewModel row,
        Action<ConnectionRowViewModel> action)
    {
        var raised = new List<string>();
        PropertyChangedEventHandler handler = (_, e) => raised.Add(e.PropertyName!);
        row.PropertyChanged += handler;
        try
        {
            action(row);
        }
        finally
        {
            row.PropertyChanged -= handler;
        }

        return raised;
    }
}