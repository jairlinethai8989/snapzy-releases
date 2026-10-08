using System.Globalization;

namespace SnapCraft;

internal static class RecordingPolicy
{
    internal const long MinimumFreeBytes = 256L * 1024 * 1024;
    internal static string ElapsedText(TimeSpan elapsed) => $"{(long)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    internal static bool MustStop(long freeBytes) => freeBytes < MinimumFreeBytes;
    internal static long FreeBytes(string path) => new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).AvailableFreeSpace;
    internal static TimeSpan FinalizeTimeout(long inputBytes, long audioBytes = 0) => TimeSpan.FromSeconds(Math.Max(300, (inputBytes + (double)audioBytes) / (10d * 1024 * 1024) + 120));
}

internal sealed record PcmAudioInput(string Path, string Format, int SampleRate, int Channels);
