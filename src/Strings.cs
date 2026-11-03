enum Language { English, Thai }

static class Strings
{
    public static Language Current = Language.English;

    static string Pick(string english, string thai) { return Current == Language.Thai ? thai : english; }

    public static string MenuCapture { get { return Pick("Capture screen", "แคปหน้าจอ"); } }
    public static string MenuSettings { get { return Pick("Settings", "ตั้งค่า"); } }
    public static string MenuExit { get { return Pick("Exit", "ออก"); } }
    public static string WindowTitle { get { return Pick("ezCapScreen Settings", "ตั้งค่า ezCapScreen"); } }
    public static string Heading { get { return Pick("Settings", "ตั้งค่า"); } }
    public static string HotkeyLabel { get { return Pick("Capture shortcut", "ปุ่มลัดแคปหน้าจอ"); } }
    public static string HotkeyPrompt { get { return Pick("Press the new shortcut now...", "กดปุ่มลัดใหม่ได้เลย..."); } }
    public static string HintIdle { get { return Pick("Click the box to change the shortcut.", "คลิกช่องนี้เพื่อเปลี่ยนปุ่มลัด"); } }
    public static string HintListening { get { return Pick("Use Ctrl or Alt with a key, or F1-F24, Pause, ScrollLock. Esc cancels.", "ใช้ Ctrl หรือ Alt ร่วมกับปุ่มอื่น หรือ F1-F24, Pause, ScrollLock กด Esc เพื่อยกเลิก"); } }
    public static string HintRejected { get { return Pick("That key needs Ctrl or Alt. Try again, or press Esc to cancel.", "ปุ่มนี้ต้องมี Ctrl หรือ Alt ร่วมด้วย ลองใหม่ หรือกด Esc เพื่อยกเลิก"); } }
    public static string AutoStart { get { return Pick("Start ezCapScreen when Windows starts", "เปิดโปรแกรมอัตโนมัติตอนเข้า Windows"); } }
    public static string Accept { get { return Pick("OK", "ตกลง"); } }
    public static string Cancel { get { return Pick("Cancel", "ยกเลิก"); } }
    public static string SaveDialogTitle { get { return Pick("Save Screen Capture", "บันทึกภาพหน้าจอ"); } }

    public static string PressToCapture(string hotkey) { return Pick("Press " + hotkey + " to capture the screen", "กด " + hotkey + " เพื่อแคปหน้าจอ"); }
    public static string HotkeyBusy(string hotkey) { return Pick(hotkey + " is in use by another app. Click this icon to capture, or change the shortcut in Settings.", hotkey + " ถูกโปรแกรมอื่นใช้อยู่ คลิกไอคอนนี้เพื่อแคป หรือเปลี่ยนปุ่มในเมนูตั้งค่า"); }
    public static string HotkeyTaken(string hotkey) { return Pick(hotkey + " is in use by another app. Please choose a different key.", hotkey + " ถูกโปรแกรมอื่นใช้อยู่ กรุณาเลือกปุ่มอื่น"); }
    public static string CaptureFailed(string reason) { return Pick("Could not capture the screen: ", "แคปหน้าจอไม่สำเร็จ: ") + reason; }
    public static string CopyFailed(string reason) { return Pick("Could not copy to the clipboard: ", "คัดลอกไปคลิปบอร์ดไม่สำเร็จ: ") + reason; }
    public static string SaveFailed(string reason) { return Pick("Could not save the file: ", "บันทึกไฟล์ไม่สำเร็จ: ") + reason; }
    public static string SettingsFailed(string reason) { return Pick("Could not save settings: ", "บันทึกการตั้งค่าไม่สำเร็จ: ") + reason; }
}
