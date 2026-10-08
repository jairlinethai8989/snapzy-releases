namespace SnapCraft;

internal static class Localization
{
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["เลือกภาพตามลำดับแท็บ"] = "Select images in tab order",
        ["มีภาพเปิดอยู่ {0} ภาพ ต้องการปิดแบบใด?"] = "There are {0} open images. What would you like to close?",
        ["ภาพที่ยังไม่ได้เก็บจะถามก่อนปิด"] = "Unsaved images will be confirmed before closing.",
        ["ปิดหน้าต่างนี้"] = "Close this window", ["ปิดภาพทั้งหมด"] = "Close all images",
        ["บันทึกภาพทั้งหมดก่อนปิดหรือไม่?"] = "Save all unsaved images before closing?",
        ["มีภาพที่ยังไม่ได้เก็บหรือแก้ไขค้างอยู่ {0} ภาพ"] = "{0} images have not been saved or have unsaved changes.",
        ["บันทึกทั้งหมด"] = "Save all", ["ไม่บันทึกทั้งหมด"] = "Discard all",
        ["บางภาพกำลังทำงาน กรุณารอแล้วลองปิดอีกครั้ง"] = "Some images are busy. Please wait and try closing again.",
        ["ภาพใหญ่เกินไป กรุณาแบ่งเป็นหลายภาพ"] = "Image is too large. Please split it into multiple images.",
        ["กำลังเตรียม SnapZy…"] = "Preparing SnapZy…",
        ["บันทึก {0} ก่อนปิดหรือไม่?"] = "Save {0} before closing?",
        ["คลิปนี้ยังไม่ได้คัดลอกหรือบันทึก"] = "This clip has not been copied or saved.",
        ["ภาพนี้ยังไม่ได้เก็บ หรือมีการแก้ไขเพิ่มเติม"] = "This image has not been saved or has unsaved changes.",
        ["บันทึก"] = "Save", ["ไม่บันทึก"] = "Discard", ["ยกเลิก"] = "Cancel", ["เพิ่มภาพ"] = "Add images", ["รวมภาพ"] = "Combine images",
        ["แนวตั้ง"] = "Vertical", ["แนวนอน"] = "Horizontal", ["จัดเอง"] = "Custom", ["ระยะห่าง"] = "Spacing",
        ["เลือกอย่างน้อยหนึ่งภาพ"] = "Select at least one image.", ["เปิดภาพไม่ได้ กรุณาเลือกภาพ PNG, JPEG, BMP, GIF หรือ TIFF ที่ถูกต้อง"] = "Could not open image. Choose a valid PNG, JPEG, BMP, GIF, or TIFF file.",
        ["ไม่พบหน้าต่างใต้กรอบที่เลือก"] = "No window was found under the selection.", ["ไม่ได้เลือกหน้าต่าง"] = "No window was selected.",
        ["กรอบจับภาพต้องอยู่ภายในพื้นที่หน้าจอที่มองเห็น"] = "The capture area must be within the visible screen.",
        ["ภาพที่จับได้เป็นสีดำทั้งเฟรม กรุณาตรวจหน้าต่างเป้าหมายหรือไดรเวอร์จอภาพ"] = "The captured frame is black. Check the target window or display driver.",
        ["ยังไม่มีภาพสำหรับต่อ"] = "There are no images to stitch yet.", ["ขนาดพื้นที่จับภาพเปลี่ยนระหว่างเลื่อน กรุณาเลือกพื้นที่ใหม่"] = "The capture area changed while scrolling. Select the area again.",
        ["อ่านขอบหน้าต่างไม่ได้"] = "Could not read the window bounds.", ["Windows ไม่ยอมรับคำสั่งเลื่อน โปรดลองเลื่อนเอง"] = "Windows did not accept the scroll command. Try scrolling manually.",
        ["เปิดปุ่ม Esc สำหรับหยุดจับภาพไม่ได้"] = "Could not enable Esc to stop capture.", ["ภาพยังไม่พร้อมบันทึก กรุณารอให้ภาพเปิดเสร็จ"] = "The image is not ready to save. Wait for it to finish loading.",
        ["ไฟล์งานใหญ่เกินไป"] = "The project file is too large.", ["ไฟล์งานใหญ่เกินไป กรุณาแบ่งเป็นหลายงาน"] = "The project file is too large. Please split it into multiple projects.",
        ["งานรวมภาพใหญ่เกินไป"] = "The combined project is too large.", ["กรุณารอให้ภาพเปิดเสร็จ"] = "Wait for the image to finish loading.", ["ภาพยังไม่พร้อมรวม กรุณารอให้ภาพเปิดเสร็จ"] = "The image is not ready to combine. Wait for it to finish loading.",
        ["เปิดภาพอีกแท็บก่อนเพิ่มภาพจากแท็บ"] = "Open another image tab before adding images from tabs.", ["ไฟล์นี้ไม่ใช่งาน SnapZy"] = "This is not a SnapZy project file.",
        ["ไม่พบไฟล์หน้าตาโปรแกรม SnapZy"] = "SnapZy interface files could not be found.", ["เปิดหน้าแก้ไขภาพไม่ได้"] = "Could not open the image editor", ["ยังปิดภาพไม่ได้ ภาพยังเปิดอยู่"] = "Could not close the image. It is still open.",
        ["บันทึกภาพไม่สำเร็จ ภาพยังเปิดอยู่"] = "Could not save the image. It remains open.", ["ยังทำรายการไม่ได้ งานเดิมยังอยู่"] = "Could not complete the operation. Your existing project is unchanged.",
        ["กำลังจับภาพยาว... Esc เพื่อจบ"] = "Capturing scrolling page… Press Esc to finish.", ["เสร็จ"] = "Done", ["เลื่อนเอง"] = "Manual scroll", ["อัตโนมัติ"] = "Automatic",
        ["หยุด"] = "Stop", ["เตรียมบันทึก  {0}"] = "Preparing recording  {0}", ["กำลังเริ่มบันทึก…"] = "Starting recording…", ["กำลังบันทึก…"] = "Recording…",
        ["หยุดก่อนเริ่มอัด"] = "Recording stopped before it started.", ["ยังไม่ได้เริ่มบันทึกวิดีโอ"] = "Video recording has not started.", ["ไม่พบไฟล์วิดีโอที่บันทึกเสร็จ"] = "The completed video file could not be found.",
        ["คัดลอกคลิป"] = "Copy clip", ["บันทึก MP4"] = "Save MP4", ["ไม่พบคลิปที่บันทึกไว้"] = "The saved clip could not be found.", ["คัดลอกคลิปแล้ว"] = "Clip copied.",
        ["คัดลอกไม่สำเร็จ"] = "Copy failed", ["ส่งต่อคลิปแล้ว"] = "Clip shared.", ["ลากคลิปไม่สำเร็จ"] = "Could not drag the clip", ["ยกเลิกการบันทึก"] = "Save canceled", ["บันทึก MP4 แล้ว"] = "MP4 saved.",
        ["วิดีโอ"] = "Video", ["พรีวิววิดีโอ"] = "Video preview", ["เปิดพรีวิวไม่ได้"] = "Could not open preview", ["คลิปยังอยู่ที่:"] = "The clip is still at:", ["ติดตั้งตัวบันทึก CPU แล้ว"] = "CPU recorder installed.",
        ["ดาวน์โหลดและติดตั้ง FFmpeg จากแพ็กเกจ Gyan.FFmpeg ผ่าน WinGet? ต้องใช้อินเทอร์เน็ตและพื้นที่ดิสก์เพิ่มเติม"] = "Download and install FFmpeg from the Gyan.FFmpeg package using WinGet? This requires an internet connection and additional disk space.",
        ["เริ่มพร้อม Windows"] = "Start with Windows", ["ออกจาก SnapZy"] = "Exit SnapZy", ["เปิด SnapZy"] = "Open SnapZy", ["หยุดและบันทึก MP4"] = "Stop and save MP4", ["ยกเลิกวิดีโอ"] = "Cancel recording",
        ["ยกเลิกการจับภาพ"] = "Capture canceled.", ["บันทึกคีย์ลัดแล้ว"] = "Shortcut saved.", ["กำลังติดตั้งตัวบันทึก CPU..."] = "Installing CPU recorder…", ["ติดตั้งแล้วแต่ยังไม่พบ FFmpeg"] = "Installation finished, but FFmpeg was not found.",
        ["กำลังปิดไฟล์วิดีโอ..."] = "Finalizing video…", ["ยกเลิกการเตรียมบันทึก"] = "Recording setup canceled.", ["บันทึก MP4 ไม่ได้"] = "Could not save MP4", ["เริ่มบันทึก MP4 ไม่ได้"] = "Could not start MP4 recording",
        ["คลิกหน้าต่างที่ต้องการจับ  •  Esc ยกเลิก"] = "Click a window to capture • Esc to cancel", ["คลิกหน้าต่างเพื่อจับภาพยาว  •  Ctrl + ลาก เลือกพื้นที่  •  Esc ยกเลิก"] = "Click a window to scroll-capture • Ctrl + drag to select an area • Esc to cancel",
        ["เลือกพื้นที่: ลากบริเวณที่ต้องการจับ  •  Esc ยกเลิก"] = "Select an area: drag over it • Esc to cancel", ["โหมด CPU ต้องใช้ FFmpeg กรุณาติดตั้งจากปุ่ม ติดตั้งตัวบันทึก CPU แล้วลองอีกครั้ง"] = "CPU mode requires FFmpeg. Install it from the Install CPU recorder button and try again.",
        ["หน้าต่างเป้าหมายถูกย่อหรือไม่มีพื้นที่ที่มองเห็น"] = "The target window is minimized or has no visible area.", ["เปิด FFmpeg ไม่ได้"] = "Could not launch FFmpeg", ["ยังไม่ได้เริ่มอัด CPU"] = "CPU recording has not started.",
        ["บันทึกเสียงไม่ได้"] = "Could not record audio", ["ปิดไฟล์ MP4 ไม่สำเร็จ"] = "Could not finalize MP4", ["รวมเสียง MP4 ไม่สำเร็จ"] = "Could not mux MP4 audio", ["ไม่รู้จักโหมดคีย์ลัด"] = "Unknown shortcut mode.",
        ["เลือก Ctrl หรือ Alt ร่วมกับ PrtSc ตัวอักษร ตัวเลข หรือ F1–F11"] = "Choose Ctrl or Alt with PrtSc, a letter, a number, or F1–F11.", ["คีย์ลัดนี้ถูกใช้งานหรือ Windows ไม่อนุญาต กรุณาเลือกชุดอื่น"] = "This shortcut is in use or blocked by Windows. Choose another combination.",
        ["จับภาพ"] = "Capture", ["พื้นที่"] = "Area", ["แก้ไขภาพ"] = "Image editor", ["เกี่ยวกับ SnapZy"] = "About SnapZy", ["มีอะไรใหม่ใน 1.0.1"] = "What's new in 1.0.1",
        ["SnapZy | เลือกภาพเพื่อแก้ไข"] = "SnapZy | Choose images to edit", ["เปิดหน้าตา SnapZy ไม่ได้"] = "Could not open the SnapZy interface",
        ["ภาพใหญ่เกินไป"] = "Image is too large", ["กรุณาแบ่งเป็นหลายภาพ"] = "Please split it into multiple images."
    };

    public static string CurrentLanguage { get; private set; } = ProductProfile.Current.DefaultLanguage;

    public static void SetLanguage(string? language) => CurrentLanguage = language is "en" or "th" ? language : ProductProfile.Current.DefaultLanguage;

    public static string Translate(string value)
    {
        if (CurrentLanguage == "th") return value.Replace("SnapZy", AppInfo.ProductName, StringComparison.Ordinal);
        var translated = English.TryGetValue(value, out var exact) ? exact : value;
        if (translated == value)
        {
            foreach (var (thai, english) in English.OrderByDescending(pair => pair.Key.Length))
                if (thai.Length > 5 && translated.Contains(thai, StringComparison.Ordinal))
                    translated = translated.Replace(thai, english, StringComparison.Ordinal);
        }
        return translated.Replace("SnapZy", AppInfo.ProductName, StringComparison.Ordinal);
    }
}
