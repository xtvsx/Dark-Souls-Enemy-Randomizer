using CommunityToolkit.Mvvm.ComponentModel;
using EnemyRandomizer.App.ViewModels.Options;

namespace EnemyRandomizer.App.ViewModels.Modules;

/// <summary>
/// One randomizer of the program (enemies today, items later). Each module has its own page in the sidebar and an
/// on/off switch; "Randomize" runs every module that is available and switched on.
/// </summary>
public abstract partial class RandomizerModuleViewModel : ObservableObject
{
    /// <summary>Stable id used to remember the on/off state in the user preferences.</summary>
    public abstract string Id { get; }

    public abstract string Title { get; }

    /// <summary>One line under the title in the sidebar and on the page.</summary>
    public abstract string Summary { get; }

    /// <summary>False for modules that are announced but not implemented yet; they cannot be switched on.</summary>
    public virtual bool IsAvailable => true;

    /// <summary>The option categories shown on the module's page.</summary>
    public virtual IReadOnlyList<OptionSectionViewModel> Sections => [];

    /// <summary>The module's page has the enemy config picker.</summary>
    public virtual bool HasEnemyConfig => false;

    /// <summary>Switched on by the user. Always false while <see cref="IsAvailable"/> is false.</summary>
    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    /// <summary>Tooltip of a module that cannot be used (yet); <c>null</c> for usable modules.</summary>
    public virtual string? Hint => null;

    /// <summary>Line under the title in the sidebar: "On" or "Off"; <c>null</c> while the module is unavailable.</summary>
    public string? State => !IsAvailable ? null : IsEnabled ? "On" : "Off";

    partial void OnIsEnabledChanged(bool value) => OnPropertyChanged(nameof(State));
}

/// <summary>The enemy randomizer: its options are the option sections of the main view model.</summary>
public sealed class EnemyRandomizerModuleViewModel(IReadOnlyList<OptionSectionViewModel> sections) : RandomizerModuleViewModel
{
    public const string ModuleId = "enemies";

    public override string Id => ModuleId;

    public override string Title => "Enemy Randomizer";

    public override string Summary => "Replace or shuffle the enemies and bosses of the game.";

    public override IReadOnlyList<OptionSectionViewModel> Sections { get; } = sections;

    public override bool HasEnemyConfig => true;
}

/// <summary>Placeholder for the upcoming item randomizer: listed in the sidebar, but not usable yet.</summary>
public sealed class ItemRandomizerModuleViewModel : RandomizerModuleViewModel
{
    public const string ModuleId = "items";

    public override string Id => ModuleId;

    public override string Title => "Item Randomizer";

    public override string Summary => "Randomize the items found in the world. Planned for a future version.";

    public override bool IsAvailable => false;

    public override string? Hint => "Coming soon";
}
