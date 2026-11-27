using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EnemyRandomizer.App.ViewModels.Options;

/// <summary>
/// One category of options (a section of the settings page). Tracks which of its options are currently shown and
/// whether the section has anything to show at all, so empty sections disappear.
/// </summary>
public sealed partial class OptionSectionViewModel : ObservableObject
{
    public OptionSectionViewModel(string title, IReadOnlyList<OptionViewModel> options)
    {
        Title = title;
        Options = options;
        RefreshVisibility();
    }

    public string Title { get; }

    /// <summary>All options of the section, shown or not.</summary>
    public IReadOnlyList<OptionViewModel> Options { get; }

    /// <summary>Shown options in display order.</summary>
    public ObservableCollection<OptionViewModel> VisibleOptions { get; } = [];

    /// <summary>False when every option of the section is hidden by the current mode.</summary>
    [ObservableProperty]
    public partial bool HasVisibleOptions { get; set; }

    /// <summary>Syncs <see cref="VisibleOptions"/> with <see cref="OptionViewModel.IsVisible"/>.</summary>
    public void RefreshVisibility()
    {
        var wanted = Options.Where(o => o.IsVisible).ToList();

        // Only touch the collection when the content differs, so the controls do not flicker.
        if (!VisibleOptions.SequenceEqual(wanted))
        {
            VisibleOptions.Clear();
            foreach (var option in wanted)
            {
                VisibleOptions.Add(option);
            }
        }

        HasVisibleOptions = VisibleOptions.Count > 0;
    }
}
