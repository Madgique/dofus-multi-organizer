using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DofusOrganizer.Models;
using DofusOrganizer.Services;
using DofusOrganizer.Services.Interfaces;
using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel;

namespace DofusOrganizer.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IWindowDetectionService _detection;
    private readonly IHotkeyService _hotkeys;
    private readonly IWindowFocusService _focus;
    private readonly ISettingsService _settings;
    private readonly INotificationListenerService _notificationListener;
    private readonly DispatcherQueue _dispatcherQueue;

    [ObservableProperty]
    private ObservableCollection<DofusWindowViewModel> _dofusWindows = [];

    [ObservableProperty]
    private int _selectedLanguageIndex;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private string _nextWindowHotkey = "";

    [ObservableProperty]
    private string _prevWindowHotkey = "";

    [ObservableProperty]
    private bool _ignoreMinimizedWindows = true;

    [ObservableProperty]
    private int _appTheme;

    [ObservableProperty]
    private bool _isUnityMode = true;

    [ObservableProperty]
    private bool _autoFocusOnTurn;

    /// <summary>
    /// Vrai si au moins un raccourci global n'a pas pu être enregistré
    /// (touche déjà utilisée par une autre application ou doublon dans les réglages).
    /// </summary>
    [ObservableProperty]
    private bool _hasHotkeyConflict;

    /// <summary>Visibilité des options Rétro-only dans le XAML (x:Bind ne supporte pas les converters sur Window).</summary>
    public Microsoft.UI.Xaml.Visibility RetroOptionsVisibility =>
        IsUnityMode ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;

    /// <summary>Version de l'application lue depuis le package MSIX (ex: "v1.1.0").</summary>
    public string AppVersion
    {
        get
        {
            try
            {
                var v = Package.Current.Id.Version;
                return $"v{v.Major}.{v.Minor}.{v.Build}";
            }
            catch { return string.Empty; }
        }
    }

    // Textes localisés pour le menu tray (mis à jour selon la langue)
    public string OpenSettingsText => GetLocalizedString("Tray_OpenSettings");
    public string QuitText => GetLocalizedString("Tray_Quit");
    public string StartWithWindowsText => GetLocalizedString("Tray_StartWithWindows");

    public MainViewModel(
        IWindowDetectionService detection,
        IHotkeyService hotkeys,
        IWindowFocusService focus,
        ISettingsService settings,
        INotificationListenerService notificationListener)
    {
        _detection = detection;
        _hotkeys = hotkeys;
        _focus = focus;
        _settings = settings;
        _notificationListener = notificationListener;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _hotkeys.HotkeyTriggered += OnHotkeyTriggered;
        _notificationListener.NotificationTextReceived += OnNotificationTextReceived;
        DofusWindows.CollectionChanged += OnWindowOrderChanged;
    }

    // Empêche les handlers partiels de déclencher ReregisterAllHotkeys pendant l'init
    private bool _isInitialized;

    // Taille de la fenêtre settings — pas observable, juste persistée
    private int _windowWidth = 540;
    private int _windowHeight = 620;

    // Dernière fenêtre Dofus au premier plan — sert de point de départ à la navigation
    // et reste mémorisée quand la fenêtre settings (ou une autre app) passe devant.
    private nint _currentDofusHwnd;
    private DispatcherQueueTimer? _foregroundTimer;

    /// <summary>Appelé par App.OnLaunched après l'initialisation du HotkeyService.</summary>
    public void Initialize()
    {
        var appSettings = _settings.Load();

        // Assigner via les propriétés générées (MVVMTK best practice)
        // Les handlers partiels vérifient _isInitialized avant d'agir
        NextWindowHotkey = appSettings.NextWindowHotkey;
        PrevWindowHotkey = appSettings.PrevWindowHotkey;
        SelectedLanguageIndex = appSettings.LanguageIndex;
        IgnoreMinimizedWindows = appSettings.IgnoreMinimizedWindows;
        AppTheme = appSettings.AppTheme;
        IsUnityMode = appSettings.DofusMode == DofusMode.Unity;
        AutoFocusOnTurn = appSettings.AutoFocusOnTurn;
        _windowWidth = appSettings.WindowWidth;
        _windowHeight = appSettings.WindowHeight;

        _isInitialized = true;

        RefreshStartupState();
        RefreshDofusWindows();
        StartForegroundTracking();

        // Démarrer le listener si l'option était activée en mode Rétro
        if (!IsUnityMode && AutoFocusOnTurn)
            _ = StartNotificationListenerAsync();

        // Créer le fichier settings.json dès le premier lancement
        SaveSettings();
    }

    [RelayCommand]
    public void RefreshDofusWindows()
    {
        // Désabonner temporairement pour éviter les sauvegardes pendant le rebuild
        DofusWindows.CollectionChanged -= OnWindowOrderChanged;

        var detected = _detection.DetectDofusWindows(IsUnityMode ? DofusMode.Unity : DofusMode.Retro);
        var appSettings = _settings.Load();

        // Reconstruire dans l'ordre sauvegardé
        var ordered = appSettings.WindowOrder
            .Select(name => detected.FirstOrDefault(w => w.CharacterName == name))
            .OfType<DofusWindowInfo>()
            .ToList();

        // Ajouter les nouvelles fenêtres pas encore dans l'ordre sauvegardé
        ordered.AddRange(detected.Where(w => !appSettings.WindowOrder.Contains(w.CharacterName)));

        DofusWindows = new ObservableCollection<DofusWindowViewModel>(
            ordered.Select((w, i) => new DofusWindowViewModel(
                w,
                i + 1,
                appSettings.WindowHotkeys.GetValueOrDefault(w.CharacterName, ""),
                OnWindowHotkeyChanged)));

        DofusWindows.CollectionChanged += OnWindowOrderChanged;

        // Oublier la fenêtre suivie si elle n'existe plus
        if (_currentDofusHwnd != 0 &&
            !DofusWindows.Any(w => w.WindowInfo.Hwnd == _currentDofusHwnd))
            _currentDofusHwnd = 0;

        UpdateNavigationIndicators();
        ReregisterAllHotkeys();
    }

    private void OnWindowOrderChanged(object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // Mettre à jour les numéros d'ordre
        for (int i = 0; i < DofusWindows.Count; i++)
            DofusWindows[i].OrderIndex = i + 1;

        UpdateNavigationIndicators();
        ReregisterAllHotkeys();
        SaveSettings();
    }

    private void OnWindowHotkeyChanged()
    {
        ReregisterAllHotkeys();
        SaveSettings();
    }

    partial void OnNextWindowHotkeyChanged(string value)
    {
        if (!_isInitialized) return;
        ReregisterAllHotkeys();
        SaveSettings();
    }

    partial void OnPrevWindowHotkeyChanged(string value)
    {
        if (!_isInitialized) return;
        ReregisterAllHotkeys();
        SaveSettings();
    }

    partial void OnIgnoreMinimizedWindowsChanged(bool value)
    {
        if (!_isInitialized) return;
        UpdateNavigationIndicators();
        SaveSettings();
    }

    partial void OnAppThemeChanged(int value)
    {
        if (!_isInitialized) return;
        App.ApplyTheme(value);
        SaveSettings();
    }

    partial void OnIsUnityModeChanged(bool value)
    {
        // Toujours notifier la visibilité, même pendant l'init (avant _isInitialized)
        // → corrige l'affichage au démarrage quand le mode Rétro est sauvegardé
        OnPropertyChanged(nameof(RetroOptionsVisibility));
        if (!_isInitialized) return;
        SaveSettings();
        RefreshDofusWindows();

        // Gérer le listener selon le nouveau mode
        if (!value && AutoFocusOnTurn)
            _ = StartNotificationListenerAsync();
        else
            _notificationListener.Stop();
    }

    partial void OnAutoFocusOnTurnChanged(bool value)
    {
        if (!_isInitialized) return;
        SaveSettings();

        if (value && !IsUnityMode)
            _ = StartNotificationListenerAsync();
        else
            _notificationListener.Stop();
    }

    private async Task StartNotificationListenerAsync()
    {
        var granted = await _notificationListener.RequestAccessAsync();
        if (granted)
            _notificationListener.Start();
        else
            // Accès refusé — désactiver silencieusement l'option
            AutoFocusOnTurn = false;
    }

    private void OnNotificationTextReceived(object? sender, string text)
    {
        if (!AutoFocusOnTurn || IsUnityMode) return;

        // L'événement peut venir d'un thread background → marshaling UI
        _dispatcherQueue.TryEnqueue(() =>
        {
            // Le service ne transmet que le titre de la notif : "[CharacterName] - Dofus Retro v..."
            // → le destinataire est toujours dans ce titre, simple Contains suffit
            var match = DofusWindows.FirstOrDefault(w =>
                text.Contains(w.WindowInfo.CharacterName, StringComparison.OrdinalIgnoreCase));

            if (match == null) return;

            if (!_focus.IsWindowValid(match.WindowInfo.Hwnd))
            {
                RefreshDofusWindows();
                return;
            }

            if (IgnoreMinimizedWindows && _focus.IsWindowMinimized(match.WindowInfo.Hwnd))
                return;

            _focus.FocusWindow(match.WindowInfo.Hwnd);
        });
    }

    partial void OnSelectedLanguageIndexChanged(int value)
    {
        if (!_isInitialized) return;
        // Mettre à jour le override (effet complet au prochain démarrage pour les x:Uid)
        App.ApplyLanguage(value);
        SaveSettings();
        OnPropertyChanged(nameof(OpenSettingsText));
        OnPropertyChanged(nameof(QuitText));
        OnPropertyChanged(nameof(StartWithWindowsText));
    }

    private void ReregisterAllHotkeys()
    {
        _hotkeys.UnregisterAllHotkeys();

        bool conflict = false;

        // Hotkeys globaux Next / Prev
        if (HotkeyParser.TryParse(NextWindowHotkey, out var nextMod, out var nextVk))
            conflict |= !_hotkeys.RegisterGlobalHotkey(HotkeyService.ID_NEXT_WINDOW, nextMod, nextVk);

        if (HotkeyParser.TryParse(PrevWindowHotkey, out var prevMod, out var prevVk))
            conflict |= !_hotkeys.RegisterGlobalHotkey(HotkeyService.ID_PREV_WINDOW, prevMod, prevVk);

        // Hotkeys directs par fenêtre (ID = ID_WINDOW_BASE + index)
        for (int i = 0; i < DofusWindows.Count; i++)
        {
            var wvm = DofusWindows[i];
            if (!string.IsNullOrWhiteSpace(wvm.Hotkey) &&
                HotkeyParser.TryParse(wvm.Hotkey, out var mod, out var vk))
            {
                conflict |= !_hotkeys.RegisterWindowHotkey(
                    HotkeyService.ID_WINDOW_BASE + i,
                    mod,
                    vk,
                    wvm.WindowInfo.Hwnd);
            }
        }

        HasHotkeyConflict = conflict;
    }

    private void OnHotkeyTriggered(object? sender, HotkeyTriggeredEventArgs e)
    {
        if (e.HotkeyId == HotkeyService.ID_NEXT_WINDOW)
            FocusNextWindow();
        else if (e.HotkeyId == HotkeyService.ID_PREV_WINDOW)
            FocusPrevWindow();
        else if (e.HotkeyId >= HotkeyService.ID_WINDOW_BASE)
            FocusWindowAt(e.HotkeyId - HotkeyService.ID_WINDOW_BASE);
    }

    private void FocusNextWindow()
    {
        int idx = FindNextIndex(FindCurrentIndex());
        if (idx >= 0)
            FocusWindowAt(idx);
    }

    private void FocusPrevWindow()
    {
        int idx = FindPreviousIndex(FindCurrentIndex());
        if (idx >= 0)
            FocusWindowAt(idx);
    }

    private void FocusWindowAt(int index)
    {
        if (index < 0 || index >= DofusWindows.Count)
            return;

        var target = DofusWindows[index];
        if (!_focus.IsWindowValid(target.WindowInfo.Hwnd))
        {
            RefreshDofusWindows();
            return;
        }
        if (IgnoreMinimizedWindows && _focus.IsWindowMinimized(target.WindowInfo.Hwnd))
            return;

        _focus.FocusWindow(target.WindowInfo.Hwnd);
        _currentDofusHwnd = target.WindowInfo.Hwnd;
        UpdateNavigationIndicators();
    }

    /// <summary>
    /// Fenêtre de départ de la navigation : la fenêtre Dofus réellement au premier plan,
    /// sinon la dernière fenêtre Dofus mémorisée (settings ou autre application devant).
    /// </summary>
    private int FindCurrentIndex()
    {
        var activeHwnd = _focus.GetForegroundWindow();
        for (int i = 0; i < DofusWindows.Count; i++)
        {
            if (DofusWindows[i].WindowInfo.Hwnd == activeHwnd)
                return i;
        }

        if (_currentDofusHwnd != 0)
        {
            for (int i = 0; i < DofusWindows.Count; i++)
            {
                if (DofusWindows[i].WindowInfo.Hwnd == _currentDofusHwnd)
                    return i;
            }
        }

        return -1;
    }

    private int FindNextIndex(int currentIdx) => FindCycleIndex(currentIdx, direction: 1);

    private int FindPreviousIndex(int currentIdx) => FindCycleIndex(currentIdx, direction: -1);

    /// <summary>
    /// Cherche la prochaine fenêtre dans la direction donnée, en sautant les fenêtres
    /// minimisées si l'option est active. Même logique pour le focus réel et les indicateurs.
    /// </summary>
    private int FindCycleIndex(int currentIdx, int direction)
    {
        int count = DofusWindows.Count;
        if (count == 0) return -1;

        // Depuis "aucune fenêtre Dofus active" : suivant → première, précédent → dernière
        if (currentIdx < 0 && direction < 0)
            currentIdx = 0;

        for (int i = 1; i <= count; i++)
        {
            int idx = ((currentIdx + (direction * i)) % count + count) % count;
            if (IgnoreMinimizedWindows && _focus.IsWindowMinimized(DofusWindows[idx].WindowInfo.Hwnd))
                continue;
            return idx;
        }

        return -1;
    }

    private void UpdateNavigationIndicators()
    {
        if (DofusWindows.Count == 0)
            return;

        int currentIdx = FindCurrentIndex();
        int nextIdx = FindNextIndex(currentIdx);
        int prevIdx = FindPreviousIndex(currentIdx);

        for (int i = 0; i < DofusWindows.Count; i++)
        {
            WindowNavigationRole role;
            if (i == currentIdx)
                role = WindowNavigationRole.Current;
            else if (i == nextIdx && i == prevIdx)
                role = WindowNavigationRole.NextAndPrevious;
            else if (i == nextIdx)
                role = WindowNavigationRole.Next;
            else if (i == prevIdx)
                role = WindowNavigationRole.Previous;
            else
                role = WindowNavigationRole.None;

            DofusWindows[i].NavigationRole = role;
        }
    }

    private void StartForegroundTracking()
    {
        if (_foregroundTimer != null) return;

        _foregroundTimer = _dispatcherQueue.CreateTimer();
        _foregroundTimer.Interval = TimeSpan.FromMilliseconds(400);
        _foregroundTimer.IsRepeating = true;
        _foregroundTimer.Tick += OnForegroundTimerTick;
        _foregroundTimer.Start();
    }

    private void OnForegroundTimerTick(DispatcherQueueTimer sender, object args)
    {
        var activeHwnd = _focus.GetForegroundWindow();
        bool changed = false;

        // Une fenêtre Dofus vient de passer au premier plan → mémoriser
        if (activeHwnd != 0 &&
            DofusWindows.Any(w => w.WindowInfo.Hwnd == activeHwnd) &&
            _currentDofusHwnd != activeHwnd)
        {
            _currentDofusHwnd = activeHwnd;
            changed = true;
        }

        // La fenêtre mémorisée a été fermée → oublier
        if (_currentDofusHwnd != 0 &&
            !DofusWindows.Any(w => w.WindowInfo.Hwnd == _currentDofusHwnd))
        {
            _currentDofusHwnd = 0;
            changed = true;
        }

        if (changed)
            UpdateNavigationIndicators();
    }

    [RelayCommand]
    private async Task ToggleStartupAsync()
    {
        try
        {
            var task = await StartupTask.GetAsync("DofusOrganizerStartup");
            if (task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy)
                task.Disable();
            else
                await task.RequestEnableAsync();

            RefreshStartupState();
        }
        catch
        {
            // Pas en contexte MSIX → ignorer
        }
    }

    private async void RefreshStartupState()
    {
        try
        {
            var task = await StartupTask.GetAsync("DofusOrganizerStartup");
            StartWithWindows = task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        }
        catch
        {
            StartWithWindows = false;
        }
    }

    [RelayCommand]
    private void ShowWindow() => App.ShowSettings();

    [RelayCommand]
    private void Quit() => App.ExitApp();

    /// <summary>
    /// Appelé par MainWindow lors du masquage vers le tray.
    /// Met à jour la taille et sauvegarde l'ensemble des settings en une seule passe.
    /// </summary>
    public void UpdateWindowSize(int width, int height)
    {
        _windowWidth = width;
        _windowHeight = height;
        SaveSettings();
    }

    private void SaveSettings()
    {
        _settings.Save(new AppSettings
        {
            NextWindowHotkey = NextWindowHotkey,
            PrevWindowHotkey = PrevWindowHotkey,
            LanguageIndex = SelectedLanguageIndex,
            IgnoreMinimizedWindows = IgnoreMinimizedWindows,
            AppTheme = AppTheme,
            DofusMode = IsUnityMode ? DofusMode.Unity : DofusMode.Retro,
            AutoFocusOnTurn = AutoFocusOnTurn,
            WindowWidth = _windowWidth,
            WindowHeight = _windowHeight,
            WindowOrder = [.. DofusWindows.Select(w => w.WindowInfo.CharacterName)],
            WindowHotkeys = DofusWindows
                .Where(w => !string.IsNullOrWhiteSpace(w.Hotkey))
                .GroupBy(w => w.WindowInfo.CharacterName)
                .ToDictionary(g => g.Key, g => g.First().Hotkey)
        });
    }

    private static string GetLocalizedString(string key)
    {
        // Windows App SDK ResourceLoader — constructeur sans argument, charge Resources.resw
        try
        {
            return new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader()
                .GetString(key);
        }
        catch
        {
            return key;
        }
    }
}
