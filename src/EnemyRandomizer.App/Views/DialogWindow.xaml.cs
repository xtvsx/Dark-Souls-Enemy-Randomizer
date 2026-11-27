using System.Windows;
using EnemyRandomizer.App.ViewModels;

namespace EnemyRandomizer.App.Views;

/// <summary>The one dialog window of the app, see <see cref="DialogViewModel"/>.</summary>
public partial class DialogWindow : Window
{
    private const double MaxScreenFraction = 0.85;

    public DialogWindow(DialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
        MaxHeight = SystemParameters.WorkArea.Height * MaxScreenFraction;
        viewModel.CloseRequested += (_, _) => Close();
    }
}
