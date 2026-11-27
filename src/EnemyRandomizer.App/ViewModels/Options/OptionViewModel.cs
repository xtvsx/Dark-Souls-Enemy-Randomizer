using CommunityToolkit.Mvvm.ComponentModel;
using EnemyRandomizer.Core.Configuration;

namespace EnemyRandomizer.App.ViewModels.Options;

/// <summary>Base of all option view models (template method: subclasses provide the current description).</summary>
public abstract partial class OptionViewModel : ObservableObject
{
    protected OptionViewModel(OptionDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _value = definition.Default;
    }

    public OptionDefinition Definition { get; }

    public string Key => Definition.Key;

    public string Title => Definition.Title;

    /// <summary>False while another option makes this one irrelevant (<c>EnabledWhen</c>); the control is greyed out.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Caption))]
    public partial bool IsEnabled { get; set; } = true;

    /// <summary>False while another option hides this one (<c>VisibleWhen</c>); hidden options are not listed at all.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    /// <summary>Drawn as a selectable card instead of a radio group.</summary>
    public bool IsCard => Definition.Presentation == OptionDefinition.CardsPresentation;

    /// <summary>Description of the current value.</summary>
    public abstract string Description { get; }

    /// <summary>Line under the title: why the option is disabled, otherwise the description of the current value.</summary>
    public string Caption => !IsEnabled && Definition.DisabledReason.Length > 0 ? Definition.DisabledReason : Description;

    public int Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                OnValueChanged();
                OnPropertyChanged(nameof(Description));
                OnPropertyChanged(nameof(Caption));
            }
        }
    }

    private int _value;

    /// <summary>Updates <see cref="IsEnabled"/> from the option's conditions (any condition true = enabled).</summary>
    public void UpdateEnabled(Func<string, int> valueOf)
    {
        ArgumentNullException.ThrowIfNull(valueOf);
        IsEnabled = Definition.EnabledWhen.Count == 0
                    || Definition.EnabledWhen.Any(c => c.Values.Contains(valueOf(c.Option)));
    }

    /// <summary>Updates <see cref="IsVisible"/> from the option's conditions (any condition true = visible).</summary>
    public void UpdateVisible(Func<string, int> valueOf)
    {
        ArgumentNullException.ThrowIfNull(valueOf);
        IsVisible = Definition.VisibleWhen.Count == 0
                    || Definition.VisibleWhen.Any(c => c.Values.Contains(valueOf(c.Option)));
    }

    protected virtual void OnValueChanged()
    {
    }
}
