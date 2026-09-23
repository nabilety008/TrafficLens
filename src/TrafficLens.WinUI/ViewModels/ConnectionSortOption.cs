using TrafficLens.Core.Selection;
namespace TrafficLens.WinUI.ViewModels;
public sealed record ConnectionFilterOption(ConnectionFilter Key, string Label);
public sealed record ConnectionSortOption(ConnectionSortKey Key, string Label);
