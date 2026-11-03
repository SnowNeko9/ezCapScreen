using System.Windows.Forms;
using Microsoft.Win32;

static class HotkeyRules
{
    public static bool IsModifier(Keys code)
    {
        return code == Keys.ShiftKey || code == Keys.ControlKey || code == Keys.Menu;
    }

    // Ctrl or Alt is required so an ordinary key keeps working for typing. Keys with no typing role may stand alone.
    // Print Screen is left out: Windows 11 registers it without complaint, then opens Snipping Tool instead of firing it.
    public static bool IsValid(Keys hotkey)
    {
        var code = hotkey & Keys.KeyCode;
        if ((hotkey & ~(Keys.KeyCode | Keys.Control | Keys.Alt | Keys.Shift)) != 0) return false;
        if (code == Keys.None || IsModifier(code)) return false;
        bool standalone = (code >= Keys.F1 && code <= Keys.F24) || code == Keys.Pause || code == Keys.Scroll;
        return standalone || (hotkey & (Keys.Control | Keys.Alt)) != 0;
    }

    public static string Label(Keys hotkey)
    {
        return new KeysConverter().ConvertToString(hotkey);
    }
}

// The Run entry is itself the autostart switch, so no second copy of that state can drift from what Windows does.
static class Settings
{
    const string AppKey = @"HKEY_CURRENT_USER\Software\ezCapScreen", HotkeyValue = "Hotkey", LanguageValue = "Language", ThaiCode = "th";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", RunValue = "ezCapScreen";
    public const string AutoStartArgument = "--autostart";

    public static Keys Hotkey
    {
        get
        {
            var stored = Registry.GetValue(AppKey, HotkeyValue, null);
            if (stored is int && HotkeyRules.IsValid((Keys)(int)stored)) return (Keys)(int)stored;
            return Keys.Control | Keys.Alt | Keys.A;
        }
        set { Registry.SetValue(AppKey, HotkeyValue, (int)value, RegistryValueKind.DWord); }
    }

    public static Language Language
    {
        get { return (Registry.GetValue(AppKey, LanguageValue, null) as string) == ThaiCode ? Language.Thai : Language.English; }
        set { Registry.SetValue(AppKey, LanguageValue, value == Language.Thai ? ThaiCode : "en", RegistryValueKind.String); }
    }

    public static bool AutoStart
    {
        get
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                return key != null && key.GetValue(RunValue) != null;
        }
        set
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (value) key.SetValue(RunValue, "\"" + Application.ExecutablePath + "\" " + AutoStartArgument);
                else key.DeleteValue(RunValue, false);
            }
        }
    }
}
