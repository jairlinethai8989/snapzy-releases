using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SnapCraft;

internal sealed class GlobalHotkey(IntPtr window, int firstId = 1) : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
    private int activeId;
    private uint modifiers;
    private uint key;

    public bool Matches(Message message) => message.Msg == 0x0312 && activeId != 0 && message.WParam.ToInt32() == activeId;

    public static bool IsValid(uint modifiers, uint key) =>
        (modifiers & ~7u) == 0 && (modifiers & 3) != 0 &&
        (key is 44 or >= 65 and <= 90 or >= 48 and <= 57 or >= 112 and <= 122);

    public void Set(uint nextModifiers, uint nextKey)
    {
        if (!IsValid(nextModifiers, nextKey))
            throw new ArgumentException("เลือก Ctrl หรือ Alt ร่วมกับ PrtSc ตัวอักษร ตัวเลข หรือ F1–F11");
        if (activeId != 0 && modifiers == nextModifiers && key == nextKey) return;
        var nextId = activeId == firstId ? firstId + 1 : firstId;
        // Keep the old binding if Windows rejects the replacement.
        if (!RegisterHotKey(window, nextId, nextModifiers | 0x4000, nextKey))
            throw new InvalidOperationException("คีย์ลัดนี้ถูกใช้งานหรือ Windows ไม่อนุญาต กรุณาเลือกชุดอื่น",
                new Win32Exception(Marshal.GetLastWin32Error()));
        if (activeId != 0) UnregisterHotKey(window, activeId);
        activeId = nextId; modifiers = nextModifiers; key = nextKey;
    }

    public void Dispose()
    {
        if (activeId != 0) UnregisterHotKey(window, activeId);
        activeId = 0;
    }
}
