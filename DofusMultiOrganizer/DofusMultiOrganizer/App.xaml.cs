using DofusOrganizer.Services;
using DofusOrganizer.Services.Interfaces;
using DofusOrganizer.ViewModels;
using DofusOrganizer.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System.Threading;
using Windows.Globalization;

namespace DofusOrganizer;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private static MainWindow? _mainWindow;

    // Mutex maintenu en vie pour toute la durée de l'app (sinon le GC le collecte)
    private static Mutex? _singleInstanceMutex;

    private static readonly string[] LanguageCodes = ["fr-FR", "en-US", "pt-BR", "es-ES"];

    public static void ApplyLanguage(int index)
    {
        if (index >= 0 && index < LanguageCodes.Length)
            ApplicationLanguages.PrimaryLanguageOverride = LanguageCodes[index];
    }

    public static void ApplyTheme(int themeIndex)
    {
        if (_mainWindow?.Content is FrameworkElement root)
        {
            root.RequestedTheme = themeIndex switch
            {
                1 => ElementTheme.Light,
                2 => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Instance unique via Mutex (fonctionne avec ou sans MSIX)
        _singleInstanceMutex = new Mutex(true, "DofusOrganizerSingleInstanceMutex", out bool createdNew);
        if (!createdNew)
        {
            System.Environment.Exit(0);
            return;
        }

        var settingsService = new SettingsService();
        var earlySettings = settingsService.Load();
        ApplyLanguage(earlySettings.LanguageIndex);

        var services = new ServiceCollection();
        services.AddSingleton<ISettingsService>(settingsService);
        services.AddSingleton<IWindowDetectionService, WindowDetectionService>();
        services.AddSingleton<IWindowFocusService, WindowFocusService>();
        services.AddSingleton<IHotkeyService, HotkeyService>();
        services.AddSingleton<INotificationListenerService, NotificationListenerService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        Services = services.BuildServiceProvider();

        _mainWindow = Services.GetRequiredService<MainWindow>();
        ApplyTheme(earlySettings.AppTheme);

        var hotkeyService = Services.GetRequiredService<IHotkeyService>();
        hotkeyService.Initialize(_mainWindow);

        var viewModel = Services.GetRequiredService<MainViewModel>();
        viewModel.Initialize();
    }

    public static void ShowSettings()
    {
        _mainWindow?.ShowSettingsWindow();
    }

    public static void ExitApp()
    {
        Services.GetRequiredService<IHotkeyService>().Dispose();
        Services.GetRequiredService<INotificationListenerService>().Dispose();
        _mainWindow?.PrepareExit();
        _mainWindow?.DestroyTrayIcon();
        Current.Exit();
    }
}
