using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnemyRandomizer.App.Services;

namespace EnemyRandomizer.App.ViewModels;

/// <summary>What a dialog is about; decides the icon and its colour.</summary>
public enum DialogKind
{
    Info,
    Warning,
    Error,
    Question,
}

/// <summary>The button the user pressed; <see cref="None"/> when the window was closed without choosing.</summary>
public enum DialogChoice
{
    None,
    Ok,
    Yes,
    No,
    Cancel,
}

/// <summary>One button of a dialog. <paramref name="IsCancel"/> is the one Esc presses.</summary>
public sealed record DialogButton(string Label, DialogChoice Choice, bool IsDefault = false, bool IsCancel = false);

/// <summary>
/// The one dialog of the app (About, info, warnings, errors and questions): same look in light and dark mode,
/// wrapped and scrollable text, optional link, a set of buttons.
/// </summary>
public sealed partial class DialogViewModel : ObservableObject
{
    private readonly IAppShell? _shell;

    public DialogViewModel(string title, string message, DialogKind kind, IReadOnlyList<DialogButton> buttons, string? linkUrl = null, IAppShell? shell = null)
    {
        Title = title;
        Message = message;
        Kind = kind;
        Buttons = buttons;
        LinkUrl = linkUrl;
        _shell = shell;
    }

    /// <summary>Raised when a button was pressed; the window closes itself.</summary>
    public event EventHandler? CloseRequested;

    public string Title { get; }

    public string Message { get; }

    public DialogKind Kind { get; }

    public IReadOnlyList<DialogButton> Buttons { get; }

    public string? LinkUrl { get; }

    public bool HasLink => !string.IsNullOrEmpty(LinkUrl);

    public DialogChoice Result { get; private set; }

    /// <summary>Icon from the Segoe Fluent Icons font.</summary>
    public string Glyph => Kind switch
    {
        DialogKind.Warning => "",
        DialogKind.Error => "",
        DialogKind.Question => "",
        _ => "",
    };

    public static DialogViewModel Info(string title, string message) =>
        new(title, message, DialogKind.Info, [new("OK", DialogChoice.Ok, IsDefault: true, IsCancel: true)]);

    public static DialogViewModel Warning(string title, string message) =>
        new(title, message, DialogKind.Warning, [new("OK", DialogChoice.Ok, IsDefault: true, IsCancel: true)]);

    public static DialogViewModel Error(string title, string message) =>
        new(title, message, DialogKind.Error, [new("OK", DialogChoice.Ok, IsDefault: true, IsCancel: true)]);

    public static DialogViewModel Confirm(string title, string message) =>
        new(title, message, DialogKind.Question,
        [
            new("Yes", DialogChoice.Yes, IsDefault: true),
            new("No", DialogChoice.No, IsCancel: true),
        ]);

    public static DialogViewModel ConfirmOrCancel(string title, string message) =>
        new(title, message, DialogKind.Question,
        [
            new("Yes", DialogChoice.Yes, IsDefault: true),
            new("No", DialogChoice.No),
            new("Cancel", DialogChoice.Cancel, IsCancel: true),
        ]);

    /// <summary>The About dialog: text, the link to the original project, a Close button.</summary>
    public static DialogViewModel About(string title, string message, string projectUrl, IAppShell shell) =>
        new(title, message, DialogKind.Info, [new("Close", DialogChoice.Ok, IsDefault: true, IsCancel: true)], projectUrl, shell);

    [RelayCommand]
    private void Choose(DialogButton button)
    {
        Result = button.Choice;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void OpenLink()
    {
        if (HasLink)
        {
            _shell?.OpenPath(LinkUrl!);
        }
    }
}
