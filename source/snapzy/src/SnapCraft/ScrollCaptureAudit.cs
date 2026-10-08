using System.Text.Json;

namespace SnapCraft;

internal sealed class ScrollCaptureAudit
{
    public List<int> Joins { get; set; } = new();
    public int UnstableFrames { get; set; }
    public int RejectedFrames { get; set; }
    public bool ReachedLimit { get; set; }
    internal static string PathFor(string image) => image + ".scroll.json";
    internal Task SaveAsync(string image) => File.WriteAllTextAsync(PathFor(image), JsonSerializer.Serialize(this));
    internal static ScrollCaptureAudit? Load(string image)
    {
        try { return JsonSerializer.Deserialize<ScrollCaptureAudit>(File.ReadAllText(PathFor(image))); }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }
}
