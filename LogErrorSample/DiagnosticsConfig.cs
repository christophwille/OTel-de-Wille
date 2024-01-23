using System.Diagnostics;
using System.Reflection;

namespace OTelPlayground;
public static class DiagnosticsConfig
{
    public const string ServiceName = "Bad Pun Service";
    public static ActivitySource ActivitySource = new ActivitySource(ServiceName);

    // Read-only Version (eg)
    private static string _versionInfo;
    public static string GetVersion() => _versionInfo ??= GetFileVersion();
    private static string GetFileVersion()
    {
        var assembly = Assembly.GetAssembly(typeof(Program));
        var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
        return fvi.FileVersion;
    }

    // Settable Saas Tenant (eg)
    public static string SaasTenant = "";
    public static string GetSaasTenant() => SaasTenant;

    public static string GetWebSiteInstanceId()
    {
        // For Azure will be a dash-less guid like e64634d070057a8ee5b9b991a4b10eff42286c68899f006fcdb23a9b436cddc6
        string? instanceId = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");

        if (String.IsNullOrWhiteSpace(instanceId)) // Happens locally / in dev
        {
            instanceId = "local";
        }

        return instanceId;
    }
}
