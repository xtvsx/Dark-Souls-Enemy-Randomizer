using System.IO;
using System.Windows;
using System.Windows.Threading;
using EnemyRandomizer.App.Services;
using EnemyRandomizer.App.ViewModels;
using EnemyRandomizer.App.ViewModels.Options;
using EnemyRandomizer.App.Views;
using EnemyRandomizer.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace EnemyRandomizer.App.Tests.Wpf;

/// <summary>All tests that touch WPF run one after the other on the same STA thread and share one <see cref="Application"/>.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WpfTestGroup
{
    public const string Name = "Wpf";
}

/// <summary>
/// One STA thread with a dispatcher and the real application resources (<c>App.xaml</c>), without starting the app host.
/// WPF allows a single <see cref="Application"/> per process, so the thread and the application live as long as the test run.
/// </summary>
internal static class WpfTestHost
{
    private static readonly Lazy<Dispatcher> LazyDispatcher = new(Start);

    public static Dispatcher Dispatcher => LazyDispatcher.Value;

    public static Task<T> RunAsync<T>(Func<T> work) => Dispatcher.InvokeAsync(work).Task;

    public static Task RunAsync(Action work) => Dispatcher.InvokeAsync(work).Task;

    public static Task<T> RunAsync<T>(Func<Task<T>> work) => Dispatcher.InvokeAsync(work).Task.Unwrap();

    /// <summary>Creates the main window with fake services, as the app would after its startup checks passed.</summary>
    public static async Task<(MainWindow Window, MainViewModel ViewModel)> CreateMainWindowAsync()
    {
        return await RunAsync(async () =>
        {
            var preferences = new FakePreferences();
            var viewModel = new MainViewModel(
                ShippedConfig.Get<AppSettings>(),
                ShippedConfig.Get<RandomizerOptionSettings>(),
                ShippedConfig.Get<MessageSettings>(),
                new OptionViewModelFactory(),
                new FakeLastConfigStore(),
                new FakeEnemyConfigs(),
                new FakeRandomizer(),
                new FakeDialogs(),
                new FakePaths(),
                FakeInstallationStatus.Ok(),
                preferences,
                new AppShell(), // the real shell so the theme command really switches the theme
                NullLogger<MainViewModel>.Instance);
            await viewModel.InitializeAsync();
            return (new MainWindow(viewModel, preferences), viewModel);
        });
    }

    private static Dispatcher Start()
    {
        using var ready = new ManualResetEventSlim();
        Dispatcher? dispatcher = null;
        var thread = new Thread(() =>
        {
            var app = new App();
            app.InitializeComponent(); // loads App.xaml (resources, Fluent theme mode); OnStartup is only called by Run()
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "WPF test thread",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        return dispatcher!;
    }

    /// <summary>Folder for rendered screenshots: <c>TestResults/ui</c> in the repository root.</summary>
    public static string ScreenshotDirectory
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DarkSoulsEnemyRandomizer.slnx")))
            {
                directory = directory.Parent;
            }

            var path = Path.Combine(directory?.FullName ?? AppContext.BaseDirectory, "TestResults", "ui");
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
