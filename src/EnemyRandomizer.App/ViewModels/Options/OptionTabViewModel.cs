using System.Collections.ObjectModel;

namespace EnemyRandomizer.App.ViewModels.Options;

/// <summary>
/// One settings tab. Coordinates the hover highlighting of its options in the description panel and splits the
/// options that are currently shown into the full-width cards and the wrapped grid below them.
/// </summary>
public sealed class OptionTabViewModel
{
    public OptionTabViewModel(string title, IReadOnlyList<OptionViewModel> options)
    {
        Title = title;
        Options = options;
        foreach (var option in options)
        {
            option.HoverChanged += (_, _) => UpdateStates();
        }

        RefreshVisibility();
    }

    public string Title { get; }

    /// <summary>All options of the tab, shown or not.</summary>
    public IReadOnlyList<OptionViewModel> Options { get; }

    /// <summary>Shown options drawn as cards (the randomize method); they sit above the grid and span its width.</summary>
    public ObservableCollection<OptionViewModel> CardOptions { get; } = [];

    /// <summary>Shown options drawn in the wrapped grid.</summary>
    public ObservableCollection<OptionViewModel> GridOptions { get; } = [];

    /// <summary>Everything that is shown, in display order (the description panel lists exactly these).</summary>
    public ObservableCollection<OptionViewModel> VisibleOptions { get; } = [];

    /// <summary>Syncs the three lists with <see cref="OptionViewModel.IsVisible"/>.</summary>
    public void RefreshVisibility()
    {
        Sync(CardOptions, Options.Where(o => o.IsVisible && o.IsCard));
        Sync(GridOptions, Options.Where(o => o.IsVisible && !o.IsCard));
        Sync(VisibleOptions, Options.Where(o => o.IsVisible));
        UpdateStates();
    }

    /// <summary>Only touches the collection when the content differs, so the controls do not flicker.</summary>
    private static void Sync(ObservableCollection<OptionViewModel> target, IEnumerable<OptionViewModel> source)
    {
        var wanted = source.ToList();
        if (target.SequenceEqual(wanted))
        {
            return;
        }

        target.Clear();
        foreach (var option in wanted)
        {
            target.Add(option);
        }
    }

    private void UpdateStates()
    {
        var hovered = VisibleOptions.FirstOrDefault(o => o.IsHovered);
        foreach (var option in Options)
        {
            option.State = hovered is null ? DescriptionState.Normal
                : option != hovered ? DescriptionState.Faded
                : option.HoveredChoice is { } choice && choice.Value != option.Value ? DescriptionState.Preview
                : DescriptionState.Hovered;
        }
    }
}
