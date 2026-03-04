using DofusOrganizer.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace DofusOrganizer.Views.Controls;

public sealed partial class HotkeyBox : UserControl
{
    // ─── Dependency Property ────────────────────────────────────────────────

    public static readonly DependencyProperty HotkeyProperty =
        DependencyProperty.Register(
            nameof(Hotkey),
            typeof(string),
            typeof(HotkeyBox),
            new PropertyMetadata(string.Empty, OnHotkeyChanged));

    public string Hotkey
    {
        get => (string)GetValue(HotkeyProperty) ?? string.Empty;
        set => SetValue(HotkeyProperty, value ?? string.Empty);
    }

    private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Mise à jour de l'affichage uniquement quand la valeur change en dehors d'une capture
        if (d is HotkeyBox box && !box._isCapturing)
            box.UpdateDisplay();
    }

    // ─── État interne ────────────────────────────────────────────────────────

    private bool _isCapturing;
    private string _previousHotkey = string.Empty;

    // ─── Constructeur ────────────────────────────────────────────────────────

    public HotkeyBox()
    {
        InitializeComponent();
        UpdateDisplay();
    }

    // ─── Handlers ────────────────────────────────────────────────────────────

    private void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isCapturing) return;
        EnterCaptureMode();
    }

    private void CaptureButton_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_isCapturing) return;

        // Intercepter TOUTES les touches pour éviter les comportements par défaut du Button
        e.Handled = true;

        var key = e.Key;

        // Ignorer les touches modificatrices seules (Ctrl, Shift, Alt, Win, CapsLock…)
        if (IsModifierOnly(key)) return;

        if (key == VirtualKey.Escape)
        {
            // Annuler → restaurer la valeur précédente
            Hotkey = _previousHotkey;
            ExitCaptureMode();
            return;
        }

        if (key == VirtualKey.Back || key == VirtualKey.Delete)
        {
            // Effacer le raccourci
            Hotkey = string.Empty;
            ExitCaptureMode();
            return;
        }

        // Construire et enregistrer le raccourci
        var modifiers = GetCurrentModifiers();
        var vk = (uint)key;
        Hotkey = HotkeyParser.Format(modifiers, vk);
        ExitCaptureMode();
    }

    private void CaptureButton_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!_isCapturing) return;

        // Perte de focus = annulation silencieuse
        Hotkey = _previousHotkey;
        ExitCaptureMode();
    }

    // ─── Modes ───────────────────────────────────────────────────────────────

    private void EnterCaptureMode()
    {
        _isCapturing = true;
        _previousHotkey = Hotkey;

        HotkeyText.Visibility = Visibility.Collapsed;
        EmptyText.Visibility = Visibility.Collapsed;
        CapturingText.Visibility = Visibility.Visible;

        CaptureButton.Focus(FocusState.Programmatic);
    }

    private void ExitCaptureMode()
    {
        _isCapturing = false;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (string.IsNullOrEmpty(Hotkey))
        {
            HotkeyText.Visibility = Visibility.Collapsed;
            EmptyText.Visibility = Visibility.Visible;
            CapturingText.Visibility = Visibility.Collapsed;
        }
        else
        {
            HotkeyText.Text = Hotkey;
            HotkeyText.Visibility = Visibility.Visible;
            EmptyText.Visibility = Visibility.Collapsed;
            CapturingText.Visibility = Visibility.Collapsed;
        }
    }

    // ─── Helpers Win32 ───────────────────────────────────────────────────────

    /// <summary>Retourne true si la touche est un modificateur seul (ne doit pas être capturé).</summary>
    private static bool IsModifierOnly(VirtualKey key) => key is
        VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift or
        VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl or
        VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu or
        VirtualKey.LeftWindows or VirtualKey.RightWindows or
        VirtualKey.CapitalLock or VirtualKey.NumberKeyLock or VirtualKey.Scroll;

    /// <summary>Lit les modificateurs actuellement enfoncés via GetKeyState.</summary>
    private static uint GetCurrentModifiers()
    {
        uint mods = 0;
        if (IsKeyDown(0x11)) mods |= (uint)HOT_KEY_MODIFIERS.MOD_CONTROL; // VK_CONTROL
        if (IsKeyDown(0x10)) mods |= (uint)HOT_KEY_MODIFIERS.MOD_SHIFT;   // VK_SHIFT
        if (IsKeyDown(0x12)) mods |= (uint)HOT_KEY_MODIFIERS.MOD_ALT;     // VK_MENU
        if (IsKeyDown(0x5B) || IsKeyDown(0x5C)) mods |= (uint)HOT_KEY_MODIFIERS.MOD_WIN; // VK_LWIN / VK_RWIN
        return mods;
    }

    /// <summary>Retourne true si le bit haut de GetKeyState est positionné (touche enfoncée).</summary>
    private static bool IsKeyDown(int vk) => PInvoke.GetKeyState(vk) < 0;
}
