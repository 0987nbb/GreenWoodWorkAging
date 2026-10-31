using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace IconGen;

/// <summary>
/// Turns the shop logo into the Windows application icon.
///
/// The full logo carries the words "GREEN WOOD WORKS", which turn to mud at the 16 and
/// 32 px sizes Windows uses for taskbar buttons and shortcut overlays, so the icon is
/// cropped to the GW monogram and given rounded corners like every other modern app tile.
///
/// Usage:  dotnet run --project tools/IconGen -- &lt;logo.jpg&gt; &lt;out.ico&gt; [preview.png]
/// </summary>
internal static class Program
{
    // The logo sits on a dark navy gradient that a plain brightness cut mistakes for artwork.
    // The mark itself is either strongly coloured (green leaf, wood letters) or near white.
    private const float Saturation = 45f;
    private const float WhiteAt    = 150f;
    private const float Margin     = 0.06f;

    private static readonly int[] Sizes = { 16, 24, 32, 48, 64, 128, 256 };

    /// <summary>Prints the artwork bands so the automatic crop can be checked by eye.</summary>
    private static void Analyse(string source)
    {
        using var logo = Image.FromFile(source);
        using var small = Downscale(logo);
        var bright = BrightMap(small);

        Console.WriteLine($"source {logo.Width}x{logo.Height}, analysed at {small.Width}x{small.Height}");

        // A coarse picture of what the detector sees, so a bad threshold is obvious.
        for (int y = 0; y < small.Height; y += 5)
        {
            var line = new System.Text.StringBuilder();
            for (int x = 0; x < small.Width; x += 5)
            {
                int hit = 0;
                for (int dy = 0; dy < 5 && y + dy < small.Height; dy++)
                    for (int dx = 0; dx < 5 && x + dx < small.Width; dx++)
                        if (bright[x + dx, y + dy]) hit++;
                line.Append(hit == 0 ? '.' : hit < 5 ? '-' : hit < 13 ? '+' : '#');
            }
            Console.WriteLine("  " + line);
        }

        foreach (var band in Rows(bright))
        {
            var (x, y, w, h) = Extent(bright, band);
            Console.WriteLine($"  band y {y}..{y + h}  x {x}..{x + w}  area {w * h}");
        }

        var crop = ContentRect(logo);
        Console.WriteLine($"  -> mark {crop} (source pixels)");
    }

    private static Bitmap Downscale(Image logo)
    {
        int w = 300;
        int h = Math.Max(1, logo.Height * w / logo.Width);
        var small = new Bitmap(w, h);
        using (var g = Graphics.FromImage(small))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(logo, 0, 0, w, h);
        }
        return small;
    }

    private static bool[,] BrightMap(Bitmap bmp)
    {
        var map = new bool[bmp.Width, bmp.Height];
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var c = bmp.GetPixel(x, y);
                float lum = 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
                float sat = Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B));
                map[x, y] = sat > Saturation || lum > WhiteAt;
            }
        return map;
    }

    /// <summary>Runs of rows that contain artwork, as (first row, height) pairs.</summary>
    private static List<(int Y, int H)> Rows(bool[,] bright)
    {
        int w = bright.GetLength(0), h = bright.GetLength(1);
        var rows = new List<bool>(h);
        for (int y = 0; y < h; y++)
        {
            int hit = 0;
            for (int x = 0; x < w; x++) if (bright[x, y]) hit++;
            rows.Add(hit >= 2);
        }

        var bands = new List<(int, int)>();
        for (int y = 0; y < h; y++)
        {
            if (!rows[y]) continue;
            int start = y;
            while (y < h && rows[y]) y++;
            bands.Add((start, y - start));
        }
        return bands;
    }

    private static (int X, int Y, int W, int H) Extent(bool[,] bright, (int Y, int H) band)
    {
        int w = bright.GetLength(0);
        int left = int.MaxValue, right = -1;
        for (int x = 0; x < w; x++)
            for (int y = band.Y; y < band.Y + band.H; y++)
                if (bright[x, y]) { left = Math.Min(left, x); right = x; break; }

        return (left, band.Y, Math.Max(1, right - left + 1), band.H);
    }

    /// <summary>
    /// The GW monogram, in source pixels. It is wider than it is tall, so it is drawn into
    /// the square icon with padding rather than cropped to a square — a square crop of a wide
    /// mark reaches down into the "GREEN WOOD" wordmark and pulls text into the icon.
    /// </summary>
    private static Rectangle ContentRect(Image logo)
    {
        using var small = Downscale(logo);
        var bright = BrightMap(small);

        var best = Rows(bright)
            .Select(b => Extent(bright, b))
            .OrderByDescending(e => (long)e.W * e.H)
            .First();

        // Back to source pixels with a little air around the mark.
        float sx = logo.Width  / (float)small.Width;
        float sy = logo.Height / (float)small.Height;
        var rect = RectangleF.FromLTRB(best.X * sx, best.Y * sy, (best.X + best.W) * sx, (best.Y + best.H) * sy);
        rect.Inflate(rect.Width * Margin, rect.Height * Margin);

        int x = Math.Clamp((int)Math.Floor(rect.X), 0, logo.Width  - 1);
        int y = Math.Clamp((int)Math.Floor(rect.Y), 0, logo.Height - 1);
        int w = Math.Clamp((int)Math.Ceiling(rect.Width),  1, logo.Width  - x);
        int h = Math.Clamp((int)Math.Ceiling(rect.Height), 1, logo.Height - y);
        return new Rectangle(x, y, w, h);
    }

    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--analyse")
        {
            Analyse(Path.GetFullPath(args[1]));
            return 0;
        }

        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: IconGen <logo.jpg> <out.ico> [preview.png]");
            Console.Error.WriteLine("       IconGen --analyse <logo.jpg>");
            return 1;
        }

        string source = Path.GetFullPath(args[0]);
        string target = Path.GetFullPath(args[1]);
        string? preview = args.Length > 2 ? Path.GetFullPath(args[2]) : null;

        if (!File.Exists(source))
        {
            Console.Error.WriteLine($"logo not found: {source}");
            return 1;
        }

        using var logo = Image.FromFile(source);
        var crop = ContentRect(logo);

        var frames = Sizes.Select(size => (size, Png(logo, crop, size))).ToList();
        File.WriteAllBytes(target, Pack(frames));
        Console.WriteLine($"{target}: {frames.Count} sizes, {new FileInfo(target).Length:N0} bytes");

        if (preview != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(preview)!);
            WritePreview(frames, preview);
        }
        return 0;
    }

    private static byte[] Png(Image logo, Rectangle crop, int size)
    {
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode   = PixelOffsetMode.HighQuality;
            g.CompositingMode   = CompositingMode.SourceCopy;

            // Aspect-fit: the mark keeps its shape and the leftover height becomes padding.
            float fit = size / (float)Math.Max(crop.Width, crop.Height);
            int w = Math.Max(1, (int)Math.Round(crop.Width  * fit));
            int h = Math.Max(1, (int)Math.Round(crop.Height * fit));
            var dest = new Rectangle((size - w) / 2, (size - h) / 2, w, h);

            // Clipping to a rounded rectangle leaves the corners at their cleared
            // (fully transparent) value, which is what makes the tile look like an app icon.
            using var path = Rounded(new Rectangle(0, 0, size, size), Math.Max(2f, size * 0.18f));
            g.SetClip(path, CombineMode.Replace);
            g.DrawImage(logo, dest, crop, GraphicsUnit.Pixel);
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    private static GraphicsPath Rounded(Rectangle r, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(r.X,             r.Y,             d, d, 180, 90);
        path.AddArc(r.Right - d,     r.Y,             d, d, 270, 90);
        path.AddArc(r.Right - d,     r.Bottom - d,    d, d,   0, 90);
        path.AddArc(r.X,             r.Bottom - d,    d, d,  90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>An .ico file: a header, one directory entry per size, then the PNG blobs.</summary>
    private static byte[] Pack(List<(int size, byte[] data)> frames)
    {
        using var ms = new MemoryStream();
        using var w  = new BinaryWriter(ms);

        w.Write((ushort)0);                 // reserved
        w.Write((ushort)1);                 // type: icon
        w.Write((ushort)frames.Count);

        int offset = 6 + frames.Count * 16;
        foreach (var (size, data) in frames)
        {
            w.Write((byte)(size >= 256 ? 0 : size));   // 0 means 256
            w.Write((byte)(size >= 256 ? 0 : size));
            w.Write((byte)0);                 // palette colours
            w.Write((byte)0);                 // reserved
            w.Write((ushort)1);               // colour planes
            w.Write((ushort)32);              // bits per pixel
            w.Write((uint)data.Length);
            w.Write((uint)offset);
            offset += data.Length;
        }

        foreach (var (_, data) in frames) w.Write(data);
        w.Flush();
        return ms.ToArray();
    }

    /// <summary>Every size laid out at its real pixel scale, so the small ones can be judged.</summary>
    private static void WritePreview(List<(int size, byte[] data)> frames, string path)
    {
        int cell = 264, gap = 12;
        using var sheet = new Bitmap(cell * frames.Count + gap, cell + 26);
        using (var g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.WhiteSmoke);
            int x = gap;
            foreach (var (size, data) in frames)
            {
                using var ms  = new MemoryStream(data);
                using var img = Image.FromStream(ms);
                // Draw the icon at its own size, then blow it up beside it for inspection.
                g.DrawImage(img, x, 26, size, size);
                if (size < 128)
                    g.DrawImage(img, x, 26 + size + 8, size * 3, size * 3);

                g.DrawString($"{size}px", new Font("Segoe UI", 9f), Brushes.Black, x, 6);
                x += cell;
            }
        }
        sheet.Save(path, ImageFormat.Png);
        Console.WriteLine($"preview: {path}");
    }
}
