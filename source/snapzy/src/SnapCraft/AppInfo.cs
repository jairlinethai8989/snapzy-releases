namespace SnapCraft;

internal static class AppInfo
{
    public static string Version => typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    public const string Developer = "jairlinethai";
    public static string ProductName => ProductProfile.Current.ProductName;
    public static string AppUserModelId => ProductProfile.Current.AppUserModelId;
    public const string InstanceId = "6A2D75A0-73EE-4B83-9D4F-C3E14C7D5C10";
}
