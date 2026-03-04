using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace DofusOrganizer.Services;

/// <summary>
/// Parse une chaîne de hotkey (ex: "F3", "Ctrl+1", "B", "Alt+F4")
/// vers les valeurs HOT_KEY_MODIFIERS + virtual key code utilisées par RegisterHotKey.
/// </summary>
public static class HotkeyParser
{
    private static readonly Dictionary<string, uint> VirtualKeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Touches de fonction
        ["F1"]  = 0x70, ["F2"]  = 0x71, ["F3"]  = 0x72, ["F4"]  = 0x73,
        ["F5"]  = 0x74, ["F6"]  = 0x75, ["F7"]  = 0x76, ["F8"]  = 0x77,
        ["F9"]  = 0x78, ["F10"] = 0x79, ["F11"] = 0x7A, ["F12"] = 0x7B,
        ["F13"] = 0x7C, ["F14"] = 0x7D, ["F15"] = 0x7E, ["F16"] = 0x7F,
        ["F17"] = 0x80, ["F18"] = 0x81, ["F19"] = 0x82, ["F20"] = 0x83,
        ["F21"] = 0x84, ["F22"] = 0x85, ["F23"] = 0x86, ["F24"] = 0x87,

        // Chiffres du clavier principal
        ["0"] = 0x30, ["1"] = 0x31, ["2"] = 0x32, ["3"] = 0x33, ["4"] = 0x34,
        ["5"] = 0x35, ["6"] = 0x36, ["7"] = 0x37, ["8"] = 0x38, ["9"] = 0x39,

        // Pavé numérique
        ["NUM0"]   = 0x60, ["NUM1"]   = 0x61, ["NUM2"] = 0x62, ["NUM3"] = 0x63,
        ["NUM4"]   = 0x64, ["NUM5"]   = 0x65, ["NUM6"] = 0x66, ["NUM7"] = 0x67,
        ["NUM8"]   = 0x68, ["NUM9"]   = 0x69,
        ["NUMMUL"] = 0x6A, ["NUMADD"] = 0x6B,
        ["NUMSUB"] = 0x6D, ["NUMDIV"] = 0x6F,

        // Touches de navigation
        ["INS"]    = 0x2D, ["DEL"]    = 0x2E,
        ["HOME"]   = 0x24, ["END"]    = 0x23,
        ["PGUP"]   = 0x21, ["PGDN"]   = 0x22,
        ["UP"]     = 0x26, ["DOWN"]   = 0x28,
        ["LEFT"]   = 0x25, ["RIGHT"]  = 0x27,

        // Touches spéciales
        ["ESC"]    = 0x1B, ["TAB"]    = 0x09,
        ["ENTER"]  = 0x0D, ["SPACE"]  = 0x20,
        ["BACK"]   = 0x08, ["PAUSE"]  = 0x13,
        ["PRINT"]  = 0x2C, ["SCROLL"] = 0x91,
        ["CAPS"]   = 0x14, ["NUMLOCK"] = 0x90,
    };

    private static readonly Dictionary<string, uint> ModifierMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CTRL"]  = (uint)HOT_KEY_MODIFIERS.MOD_CONTROL,
        ["ALT"]   = (uint)HOT_KEY_MODIFIERS.MOD_ALT,
        ["SHIFT"] = (uint)HOT_KEY_MODIFIERS.MOD_SHIFT,
        ["WIN"]   = (uint)HOT_KEY_MODIFIERS.MOD_WIN,
    };

    /// <summary>
    /// Tente de parser "Ctrl+F3" → (modifiers: MOD_CONTROL, vk: 0x72).
    /// Retourne false si le format n'est pas reconnu.
    /// </summary>
    public static bool TryParse(string input, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        // Ajouter MOD_NOREPEAT par défaut pour éviter le spam
        modifiers = (uint)HOT_KEY_MODIFIERS.MOD_NOREPEAT;

        var parts = input.Trim().Split('+', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i].Trim();
            if (ModifierMap.TryGetValue(part, out var mod))
                modifiers |= mod;
            else
                return false;
        }

        var keyPart = parts[^1].Trim();

        // Lettre simple A-Z
        if (keyPart.Length == 1 && char.IsLetter(keyPart[0]))
        {
            virtualKey = (uint)char.ToUpperInvariant(keyPart[0]);
            return true;
        }

        // Touche nommée
        if (VirtualKeyMap.TryGetValue(keyPart, out virtualKey))
            return true;

        // Touche OEM en hexadécimal (ex: "0xDE" pour ²)
        if (keyPart.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            uint.TryParse(keyPart[2..], System.Globalization.NumberStyles.HexNumber, null, out virtualKey))
            return true;

        return false;
    }

    /// <summary>
    /// Formate les modifiers + vk en string lisible (ex: "Ctrl+F3").
    /// </summary>
    public static string Format(uint modifiers, uint virtualKey)
    {
        var parts = new List<string>();

        if ((modifiers & (uint)HOT_KEY_MODIFIERS.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & (uint)HOT_KEY_MODIFIERS.MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & (uint)HOT_KEY_MODIFIERS.MOD_SHIFT) != 0) parts.Add("Shift");
        if ((modifiers & (uint)HOT_KEY_MODIFIERS.MOD_WIN) != 0) parts.Add("Win");

        var keyName = VirtualKeyMap
            .FirstOrDefault(kv => kv.Value == virtualKey).Key
            ?? ((virtualKey >= 0x41 && virtualKey <= 0x5A)
                ? ((char)virtualKey).ToString()
                : VkToDisplayChar(virtualKey));

        parts.Add(keyName);
        return string.Join("+", parts);
    }

    private static string VkToDisplayChar(uint virtualKey)
    {
        // MapVirtualKey MAPVK_VK_TO_CHAR (2) : retourne le caractère Unicode du VK
        // Le bit 31 indique une touche morte (ex: ^) → on garde le format hex dans ce cas
        uint mapped = PInvoke.MapVirtualKey(virtualKey, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_CHAR);
        if (mapped != 0 && (mapped & 0x80000000) == 0)
            return ((char)mapped).ToString();

        return $"0x{virtualKey:X2}";
    }
}
