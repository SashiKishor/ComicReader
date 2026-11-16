using ComicReader.Services;
using ComicReader.Services.Interfaces;
using ComicReader.ViewModels;
using ComicReader.Views;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace ComicReader;

/// <summary>
/// Application entry point configuring dependency injection and initial window instantiation.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public IServiceProvider Services => _serviceProvider ?? throw new InvalidOperationException("Services not initialized.");

    public App()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    private static string GetLogPath(string filename)
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "ComicReader", "Logs");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, filename);
        }
        catch
        {
            return filename;
        }
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            File.WriteAllText(GetLogPath("crash.log"), e.Exception.ToString());
        }
        catch { }

        MessageBox.Show($"An unexpected error occurred: {e.Exception.Message}\n\nSee AppData\\Local\\ComicReader\\Logs\\crash.log for details.", "ComicReader Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = false;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        try
        {
            File.WriteAllText(GetLogPath("crash_domain.log"), e.ExceptionObject?.ToString() ?? "Unknown domain error");
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);

            var services = new ServiceCollection();
            ConfigureServices(services);

            _serviceProvider = services.BuildServiceProvider();

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            File.WriteAllText("startup_error.log", ex.ToString());
            MessageBox.Show($"Startup Error: {ex.Message}\n\nStack:\n{ex.StackTrace}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Infrastructure & Services
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<ICacheService, CacheService>();
        services.AddSingleton<ICoverExtractorService, CoverExtractorService>();
        services.AddSingleton<IProgressTrackingService, ProgressTrackingService>();
        services.AddTransient<IComicDocumentService, ComicDocumentService>();
        services.AddSingleton<ILibraryScannerService, LibraryScannerService>();

        // ViewModels
        services.AddSingleton<StartupViewModel>();
        services.AddSingleton<LibraryViewModel>();
        services.AddSingleton<ReaderViewModel>();
        services.AddSingleton<MainViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
