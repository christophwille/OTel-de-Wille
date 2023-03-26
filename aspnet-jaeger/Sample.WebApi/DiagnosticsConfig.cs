using System.Diagnostics;
using System.Reflection;

namespace Sample.WebApi
{
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
    }
}
