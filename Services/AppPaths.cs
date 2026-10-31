namespace GreenWoodWorkAging.Services
{
    /// <summary>
    /// Single source of truth for the folders this application reads and writes.
    /// </summary>
    public static class AppPaths
    {
        /// <summary>
        /// Roaming application-data folder, e.g. %APPDATA%\GreenWoodWorkAging.
        ///
        /// It deliberately lives outside the install directory: the installer replaces or
        /// removes that directory at will, and customer bills must survive both an update and
        /// an uninstall. GREENWOOD_DATA_DIR redirects it, which is how the installer's
        /// install → update → uninstall persistence check is run without touching a real
        /// customer's database.
        /// </summary>
        public static string DataDirectory { get; } = Resolve();

        public static string DatabaseFile => Path.Combine(DataDirectory, "greenwoodwork.db");

        private static string Resolve()
        {
            string? redirect = Environment.GetEnvironmentVariable("GREENWOOD_DATA_DIR");
            if (!string.IsNullOrWhiteSpace(redirect)) return redirect;

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GreenWoodWorkAging");
        }
    }
}
