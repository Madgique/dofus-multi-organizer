using DofusOrganizer.Services.Interfaces;
using DofusOrganizer.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;
using WinRT.Interop;

namespace DofusOrganizer.Views;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    private readonly HWND _hwnd;
    private readonly AppWindow _appWindow;
    private bool _isExiting;

    // Exposé en nint pour éviter un problème d'accessibilité avec le type HWND (CsWin32 internal)
    public nint WindowHandle => _hwnd;

    public MainWindow(MainViewModel viewModel, ISettingsService settings)
    {
        InitializeComponent();

        ViewModel = viewModel;

        // Récupérer le HWND natif et l'AppWindow WinUI
        var hWnd = WindowNative.GetWindowHandle(this);
        _hwnd = new HWND(hWnd);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        // Retirer de la barre des tâches et de Alt+Tab
        _appWindow.IsShownInSwitchers = false;

        // Configurer le presenter : redimensionnable, pas de maximize
        var presenter = OverlappedPresenter.Create();
        presenter.IsMaximizable = false;
        presenter.IsResizable = true;
        presenter.SetBorderAndTitleBar(true, true);
        _appWindow.SetPresenter(presenter);

        // Icône de la fenêtre (barre des tâches, titre) — à définir explicitement dans WinUI 3
        _appWindow.SetIcon("Assets/TrayIcon.ico");

        // Restaurer la taille sauvegardée (ou 540×620 par défaut au premier lancement)
        var saved = settings.Load();
        _appWindow.Resize(new SizeInt32(saved.WindowWidth, saved.WindowHeight));

        // Centrer la fenêtre à l'écran
        CenterWindow();

        // Activer brièvement pour que WinUI + H.NotifyIcon s'initialisent correctement,
        // puis cacher immédiatement via la queue du dispatcher (frame suivante)
        this.Activate();
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            () => _appWindow.Hide());

        // Interception de la fermeture → cacher au lieu de quitter
        Closed += OnWindowClosed;
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (_isExiting) return; // laisser WinUI terminer le process
        args.Handled = true;
        HideToTray();
    }

    public void ShowSettingsWindow()
    {
        CenterWindow();
        _appWindow.IsShownInSwitchers = true;
        _appWindow.Show();
        _appWindow.MoveInZOrderAtTop();
        PInvoke.SetForegroundWindow(_hwnd);
    }

    private void HideToTray()
    {
        // Sauvegarder la taille courante avant de masquer (une seule fois, pas à chaque pixel)
        ViewModel.UpdateWindowSize((int)_appWindow.Size.Width, (int)_appWindow.Size.Height);
        _appWindow.IsShownInSwitchers = false;
        _appWindow.Hide();
    }

    public void PrepareExit()
    {
        _isExiting = true;
    }

    public void DestroyTrayIcon()
    {
        TrayIcon.Dispose();
    }

    private void CenterWindow()
    {
        var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Nearest);
        var workArea = displayArea.WorkArea;
        _appWindow.Move(new PointInt32(
            (workArea.Width - _appWindow.Size.Width) / 2,
            (workArea.Height - _appWindow.Size.Height) / 2));
    }
}
