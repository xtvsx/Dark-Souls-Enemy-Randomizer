using System.Windows;
using EnemyRandomizer.App.ViewModels;

namespace EnemyRandomizer.App.Views;

public partial class BackupsWindow : Window
{
    public BackupsWindow(BackupsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Comparing the files takes a moment, so the window opens first and fills in afterwards.
        Loaded += async (_, _) => await viewModel.RefreshCommand.ExecuteAsync(null);
    }
}
