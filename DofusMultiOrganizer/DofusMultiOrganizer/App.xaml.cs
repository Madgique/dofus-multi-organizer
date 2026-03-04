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

    public App()
    {
        InitializeComponent();
    }

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

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Instance unique via Mutex (fonctionne avec ou sans MSIX)
        _singleInstanceMutex = new Mutex(true, "DofusOrganizerSingleInstanceMutex", out bool createdNew);
        if (!createdNew)
        {
            System.Environment.Exit(0);
            return;
        }

        // Charger les settings tôt pour appliquer la langue AVANT de créer l'UI
        // (x:Uid utilise les ressources chargées au démarrage)
        var settingsService = new SettingsService();
        var earlySettings = settingsService.Load();
        ApplyLanguage(earlySettings.LanguageIndex);

        // Construction du conteneur DI — réutiliser la même instance SettingsService
        var services = new ServiceCollection();
        services.AddSingleton<ISettingsService>(settingsService);
        services.AddSingleton<IWindowDetectionService, WindowDetectionService>();
        services.AddSingleton<IWindowFocusService, WindowFocusService>();
        services.AddSingleton<IHotkeyService, HotkeyService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();

        // Créer la fenêtre principale — elle se cache d'elle-même au démarrage
        _mainWindow = Services.GetRequiredService<MainWindow>();

        // Appliquer le thème après création de la fenêtre
        ApplyTheme(earlySettings.AppTheme);

        // Initialiser le service de hotkeys (requiert le HWND de la fenêtre)
        var hotkeyService = Services.GetRequiredService<IHotkeyService>();
        hotkeyService.Initialize(_mainWindow);

        // Charger les paramètres et enregistrer les hotkeys initiaux
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
        _mainWindow?.PrepareExit();
        _mainWindow?.DestroyTrayIcon();
        Current.Exit();
    }
}
