using TrafficLens.Core.Selection;

namespace TrafficLens.App.ViewModels;

/// <summary>One localized "Sort by" option backed by a <see cref="ProcessSortKey"/>.</summary>
public sealed record ApplicationSortOption(ProcessSortKey Key, string Label);