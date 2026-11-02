using System.Runtime.CompilerServices;

namespace GreenWoodWorkAging.Tests
{
    /// <summary>
    /// Every test in this assembly runs against a throwaway data folder.
    ///
    /// AppPaths resolves the folder once per process, on the first database access, so the redirect
    /// has to happen before any test starts: a class-level redirect only wins if that class touches
    /// the database first, and when it does not, the whole run writes its fixtures into the real
    /// customer file.
    /// </summary>
    internal static class TestIsolation
    {
        [ModuleInitializer]
        internal static void RedirectDataFolder()
        {
            string dir = Path.Combine(Path.GetTempPath(), "gwa-tests", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(dir);
            Environment.SetEnvironmentVariable("GREENWOOD_DATA_DIR", dir);
        }
    }
}
