using TrafficLens.Core.Selection;

namespace TrafficLens.App.ViewModels;

/// <summary>One localized "Show" filter option backed by a <see cref="ConnectionFilter"/>.</summary>
public sealed record ConnectionFilterOption(ConnectionFilter Key, string Label);

/// <summary>One localized "Sort by" option backed by a <see cref="ConnectionSortKey"/>.</summary>
public sealed record ConnectionSortOption(ConnectionSortKey Key, string Label);