namespace SnapCraft;

internal sealed record ProductProfile(
    string ProductName,
    string ApplicationFolder,
    string DataFolder,
    string StartupRegistryValue,
    string UninstallRegistryKey,
    string InstanceId,
    string AppUserModelId,
    string DefaultLanguage)
{
    public string IconFile => "snapzy.ico";

    public static ProductProfile Current { get; } = new(
        "SnapZy",
        "Snapzy",
        "Snapzy",
        "Snapzy",
        "Snapzy",
        "6A2D75A0-73EE-4B83-9D4F-C3E14C7D5C10",
        "jairlinethai.Snapzy",
        "en");
}
