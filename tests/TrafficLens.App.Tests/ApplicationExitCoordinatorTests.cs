using Microsoft.Extensions.Logging.Abstractions;
using TrafficLens.App.Services;

namespace TrafficLens.App.Tests;

public sealed class ApplicationExitCoordinatorTests
{
    private readonly FakeTrayService _tray = new();
    private readonly FakeFloatingWidgetService _widget = new();
    private readonly ApplicationExitCoordinator _coordinator;

    public ApplicationExitCoordinatorTests()
    {
        _coordinator = new ApplicationExitCoordinator(
            _tray,
            _widget,
            NullLogger<ApplicationExitCoordinator>.Instance);
    }

    [Fact]
    public void RequestApplicationExit_SetsFlagAndDisposesTrayAndWidget()
    {
        _coordinator.RequestApplicationExit();

        Assert.True(_coordinator.IsExitRequested);
        Assert.Equal(1, _tray.DisposeCalls);
        Assert.Equal(1, _widget.DisposeCalls);
    }

    [Fact]
    public void RequestApplicationExit_SecondCall_IsIdempotent()
    {
        _coordinator.RequestApplicationExit();
        _coordinator.RequestApplicationExit();

        Assert.Equal(1, _tray.DisposeCalls);
        Assert.Equal(1, _widget.DisposeCalls);
    }

    [Fact]
    public void RequestApplicationExit_BeforeAnyClose_WindowCloseCannotIntercept()
    {
        _coordinator.RequestApplicationExit();

        Assert.True(_coordinator.IsExitRequested);

        var action = TrayBehavior.ResolveCloseAction(_coordinator.IsExitRequested, new FakeSettingsService());

        Assert.Equal(WindowCloseAction.Exit, action);
    }

    [Fact]
    public void IsExitRequested_False_UntilRequested()
    {
        Assert.False(_coordinator.IsExitRequested);
    }
}