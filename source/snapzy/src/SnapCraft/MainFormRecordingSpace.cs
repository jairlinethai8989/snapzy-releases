namespace SnapCraft;

internal sealed partial class MainForm
{
    private bool checkingRecordingSpace;
    private TimeSpan lastSpaceCheck;

    private async Task CheckRecordingSpaceAsync(VideoSession active)
    {
        checkingRecordingSpace = true; lastSpaceCheck = active.Elapsed;
        try
        {
            var bytes = await Task.Run(() => RecordingPolicy.FreeBytes(active.OutputDirectory));
            if (video != active || !RecordingPolicy.MustStop(bytes)) return;
            await StopVideoAsync(true);
            var message = Localization.CurrentLanguage == "th" ? "หยุดบันทึกเพราะพื้นที่ดิสก์ใกล้เต็ม กรุณาบันทึกคลิปไปยังไดรฟ์ที่มีพื้นที่ว่าง" : "Recording stopped because disk space is low. Save the clip to a drive with free space.";
            MessageBox.Show(this, message, AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            if (video == active) { await StopVideoAsync(true); MessageBox.Show(this, error.Message, AppInfo.ProductName); }
        }
        finally { checkingRecordingSpace = false; }
    }
}
