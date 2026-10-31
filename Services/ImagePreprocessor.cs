using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace GreenWoodWorkAging.Services
{
    /// <summary>
    /// Turns a phone photo of a paper list into something a text reader can work on.
    ///
    /// A raw photo defeats OCR for three reasons, all fixed here:
    ///   • the photographer's shadow — a global threshold turns it into a solid black block,
    ///     so the threshold is local (each pixel against the average around it)
    ///   • blue pen on white paper — the red channel separates ink from paper best
    ///   • the notebook rules and a slight tilt — deskewed, then rows that are mostly dark
    ///     across the page are erased
    /// </summary>
    public static class ImagePreprocessor
    {
        /// <summary>Phone cameras give 4000 px images; OCR works at roughly this size and much faster.</summary>
        private const int MaxSide = 1600;

        public static Bitmap Prepare(string imagePath)
        {
            using var original = new Bitmap(imagePath);
            using var fitted = Fit(original);

            byte[] gray = RedChannel(fitted, out int width, out int height);
            int window = Math.Max(15, (Math.Min(width, height) / 22) | 1);
            byte[] binary = LocalThreshold(gray, width, height, window, minus: 14);

            using var cleaned = ToBitmap(binary, width, height);
            float angle = SkewAngle(cleaned);

            // Rotate hands back a new picture; it belongs to the caller from here on.
            return EraseRuleLines(Rotate(cleaned, angle));
        }

        // ── Steps ────────────────────────────────────────────────────────────

        private static Bitmap Fit(Bitmap src)
        {
            float factor = Math.Min(1f, (float)MaxSide / Math.Max(src.Width, src.Height));
            if (factor >= 1f) return Clone24(src);

            var dst = new Bitmap((int)(src.Width * factor), (int)(src.Height * factor), PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(dst);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.DrawImage(src, 0, 0, dst.Width, dst.Height);
            return dst;
        }

        /// <summary>Blue ink absorbs red light, so the red channel is the cleanest ink/paper split.</summary>
        private static byte[] RedChannel(Bitmap bmp, out int width, out int height)
        {
            width = bmp.Width;
            height = bmp.Height;
            var gray = new byte[width * height];

            var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                var row = new byte[data.Stride];
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, data.Stride);
                    for (int x = 0; x < width; x++)
                        gray[y * width + x] = row[x * 3 + 2];   // stored blue, green, red
                }
            }
            finally { bmp.UnlockBits(data); }

            return gray;
        }

        /// <summary>
        /// Black where the pixel is darker than the average of the box around it, so a shadow
        /// that slowly darkens the page disappears while the pen strokes stay.
        /// </summary>
        private static byte[] LocalThreshold(byte[] gray, int width, int height, int window, int minus)
        {
            int stride = width + 1;
            var integral = new long[(height + 1) * stride];
            for (int y = 0; y < height; y++)
            {
                long rowSum = 0;
                int here = (y + 1) * stride, above = y * stride, src = y * width;
                for (int x = 0; x < width; x++)
                {
                    rowSum += gray[src + x];
                    integral[here + x + 1] = integral[above + x + 1] + rowSum;
                }
            }

            int half = window / 2;
            var binary = new byte[gray.Length];
            for (int y = 0; y < height; y++)
            {
                int y0 = Math.Max(0, y - half), y1 = Math.Min(height, y + half + 1);
                for (int x = 0; x < width; x++)
                {
                    int x0 = Math.Max(0, x - half), x1 = Math.Min(width, x + half + 1);
                    long sum = integral[y1 * stride + x1] - integral[y0 * stride + x1]
                             - integral[y1 * stride + x0] + integral[y0 * stride + x0];
                    double mean = sum / (double)((x1 - x0) * (y1 - y0));
                    binary[y * width + x] = (byte)(gray[y * width + x] < mean - minus ? 0 : 255);
                }
            }
            return binary;
        }

        /// <summary>The tilt whose horizontal projection has the sharpest peaks — that is straight lines of text.</summary>
        private static float SkewAngle(Bitmap image)
        {
            using var probe = Fit(image);   // small and square-ish is enough to judge the tilt
            int width = probe.Width, height = probe.Height;
            byte[] pixels = GrayOf(probe);

            float best = 0;
            double bestScore = -1;
            for (int tenth = -60; tenth <= 60; tenth += 10)
            {
                double score = ProfileScore(pixels, width, height, tenth / 10f);
                if (score > bestScore) { bestScore = score; best = tenth / 10f; }
            }
            for (int tenth = -9; tenth <= 9; tenth += 3)
            {
                float angle = best + tenth / 10f;
                double score = ProfileScore(pixels, width, height, angle);
                if (score > bestScore) { bestScore = score; best = angle; }
            }
            return best;
        }

        private static double ProfileScore(byte[] pixels, int width, int height, float angle)
        {
            double radians = angle * Math.PI / 180.0;
            double cos = Math.Cos(radians), sin = Math.Sin(radians);
            double cx = width / 2.0, cy = height / 2.0;

            var profile = new int[height];
            for (int y = 0; y < height; y++)
            {
                int dark = 0;
                for (int x = 0; x < width; x += 2)
                {
                    int sourceX = (int)(cx + (x - cx) * cos + (y - cy) * sin);
                    int sourceY = (int)(cy - (x - cx) * sin + (y - cy) * cos);
                    if (sourceX < 0 || sourceX >= width || sourceY < 0 || sourceY >= height) continue;
                    if (pixels[sourceY * width + sourceX] < 128) dark++;
                }
                profile[y] = dark;
            }

            double mean = profile.Average();
            return profile.Sum(p => (p - mean) * (p - mean));
        }

        private static Bitmap Rotate(Bitmap src, float angle)
        {
            if (Math.Abs(angle) < 0.05f) return Clone24(src);

            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(dst);
            g.Clear(Color.White);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.TranslateTransform(dst.Width / 2f, dst.Height / 2f);
            g.RotateTransform(angle);
            g.DrawImage(src, -src.Width / 2f, -src.Height / 2f);
            return dst;
        }

        private static Bitmap Clone24(Bitmap src)
        {
            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(dst);
            g.DrawImageUnscaled(src, 0, 0);
            return dst;
        }

        /// <summary>A ruled notebook line runs across almost the whole page; handwriting does not.</summary>
        private static Bitmap EraseRuleLines(Bitmap image)
        {
            int width = image.Width, height = image.Height;
            var data = image.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            try
            {
                var row = new byte[data.Stride];
                var darkCount = new int[height];

                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, data.Stride);
                    int dark = 0;
                    for (int x = 0; x < width; x += 2)
                        if (row[x * 3 + 2] < 128) dark++;
                    darkCount[y] = dark;
                }

                int samples = (width + 1) / 2;
                for (int y = 0; y < height; y++)
                {
                    if (darkCount[y] * 100 / Math.Max(1, samples) < 40) continue;

                    Array.Fill(row, (byte)255);
                    for (int x = 0; x < width; x++) { row[x * 3] = 255; row[x * 3 + 1] = 255; row[x * 3 + 2] = 255; }
                    Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, data.Stride);
                }
            }
            finally { image.UnlockBits(data); }

            return image;
        }

        // ── Bitmap helpers ───────────────────────────────────────────────────

        private static byte[] GrayOf(Bitmap bmp)
            => RedChannel(bmp, out _, out _);

        private static Bitmap ToBitmap(byte[] pixels, int width, int height)
        {
            var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                var row = new byte[data.Stride];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        byte value = pixels[y * width + x];
                        row[x * 3] = value;
                        row[x * 3 + 1] = value;
                        row[x * 3 + 2] = value;
                    }
                    Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, data.Stride);
                }
            }
            finally { bmp.UnlockBits(data); }
            return bmp;
        }
    }
}
