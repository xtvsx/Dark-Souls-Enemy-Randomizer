using EnemyRandomizer.Core.Configuration;

namespace EnemyRandomizer.App.ViewModels.Options;

/// <summary>Radio button group option.</summary>
public sealed class ChoiceOptionViewModel : OptionViewModel
{
    public ChoiceOptionViewModel(OptionDefinition definition)
        : base(definition)
    {
        Choices = definition.Choices.Select(c => new ChoiceViewModel(this, c)).ToList();
    }

    public IReadOnlyList<ChoiceViewModel> Choices { get; }

    /// <summary>Many choices go into a drop-down; a few fit in a row of radio buttons.</summary>
    public bool UseDropdown => Choices.Count > MaxRadioChoices;

    private const int MaxRadioChoices = 3;

    public override string Description =>
        Choices.FirstOrDefault(c => c.Value == Value)?.Description ?? "";

    protected override void OnValueChanged()
    {
        foreach (var choice in Choices)
        {
            choice.NotifySelectionChanged();
        }
    }
}

/// <summary>One radio button. Selection lives in the owner, so exactly one choice is selected.</summary>
public sealed class ChoiceViewModel(ChoiceOptionViewModel owner, OptionChoice definition)
    : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public ChoiceOptionViewModel Owner { get; } = owner;

    public int Value => definition.Value;

    public string Label => definition.Label;

    public string Description => definition.Description;

    /// <summary>Short line shown under the label when the option is drawn as cards.</summary>
    public string Summary => definition.Summary;

    public bool IsSelected
    {
        get => Owner.Value == Value;
        set
        {
            if (value)
            {
                Owner.Value = Value;
            }
        }
    }

    internal void NotifySelectionChanged() => OnPropertyChanged(nameof(IsSelected));
}
