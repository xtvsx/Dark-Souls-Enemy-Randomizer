using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using EnemyRandomizer.App.ViewModels;

namespace EnemyRandomizer.App.Views;

public partial class BackupsWindow : Window
{
    public BackupsWindow(BackupsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Group the rows by kind of file (maps, events, AI scripts, effects).
        var view = CollectionViewSource.GetDefaultView(viewModel.Items);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(BackupItemViewModel.Group)));
    }
}
