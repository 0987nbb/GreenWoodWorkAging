namespace GreenWoodWorkAging.Services
{
    /// <summary>
    /// The branded icon shown in the title bar, taskbar and alt-tab of every window.
    /// </summary>
    public static class AppIcon
    {
        private static readonly Lazy<Icon?> _icon = new(Load);

        public static Icon? Value => _icon.Value;

        /// <summary>Gives a window the branded title-bar and taskbar icon.</summary>
        public static void Apply(Form form)
        {
            if (Value != null) form.Icon = Value;
        }

        private static Icon? Load()
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "Resources", "app.ico");
                if (File.Exists(path))
                {
                    // The bytes are copied into memory first: an Icon keeps its stream open for
                    // its whole life, so a FileStream here would lock the install folder and make
                    // an update-over-a-running-app fail. The MemoryStream is intentionally left
                    // alive — the cached Icon reads from it whenever Windows paints.
                    var stream = new MemoryStream(File.ReadAllBytes(path));
                    return new Icon(stream, 48, 48);
                }

                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                return null;
            }
        }
    }
}
