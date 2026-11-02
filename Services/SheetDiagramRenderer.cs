using System.Drawing.Drawing2D;
using GreenWoodWorkAging.Models;

namespace GreenWoodWorkAging.Services
{
    /// <summary>
    /// Draws one sheet of the cutting plan: every piece in the position it was given,
    /// labelled with its height and width, and the plywood left over around them.
    ///
    /// The drawing lives here rather than in the control so the same picture can be put on
    /// screen, on paper, or into a file for a check without opening a window.
    /// </summary>
    public static class SheetDiagramRenderer
    {
        private static readonly MeasurementDisplayFormatter Fmt = new();

        private static readonly Color Paper     = Color.White;
        private static readonly Color SheetLine = Color.FromArgb(38, 52, 45);
        private static readonly Color ScrapFill = Color.FromArgb(243, 245, 243);
        private static readonly Color ScrapLine = Color.FromArgb(190, 201, 192);
        private static readonly Color Ink       = Color.FromArgb(34, 49, 42);
        private static readonly Color Subtle    = Color.FromArgb(122, 137, 128);

        /// <summary>One colour per grid row, so the same size is recognisable across sheets.</summary>
        private static readonly (Color Fill, Color Line)[] Palette =
        {
            (Color.FromArgb(244, 225, 202), Color.FromArgb(186, 148, 100)),
            (Color.FromArgb(214, 232, 222), Color.FromArgb(104, 156, 128)),
            (Color.FromArgb(237, 215, 223), Color.FromArgb(176, 118, 140)),
            (Color.FromArgb(217, 229, 243), Color.FromArgb(104, 138, 182)),
            (Color.FromArgb(241, 237, 205), Color.FromArgb(170, 158, 92)),
            (Color.FromArgb(225, 220, 241), Color.FromArgb(138, 128, 176)),
            (Color.FromArgb(214, 236, 236), Color.FromArgb(100, 156, 156)),
            (Color.FromArgb(244, 222, 215), Color.FromArgb(182, 122, 104))
        };

        public static Color ColorFor(int serialNumber) => Palette[Math.Abs(serialNumber) % Palette.Length].Fill;

        /// <summary>
        /// Paints <paramref name="sheet"/> centred inside <paramref name="box"/>, scaled to
        /// fit, and returns the rectangle the sheet itself occupies.
        /// </summary>
        public static RectangleF Draw(Graphics g, Rectangle box, SheetLayout sheet)
        {
            const int margin = 8;

            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float scale = Math.Min((box.Width  - margin * 2) / (float)sheet.SheetWidthEighths,
                                   (box.Height - margin * 2) / (float)sheet.SheetHeightEighths);
            if (scale <= 0) return RectangleF.Empty;

            var origin = new PointF(
                box.X + (box.Width  - sheet.SheetWidthEighths * scale) / 2f,
                box.Y + (box.Height - sheet.SheetHeightEighths * scale) / 2f);

            var sheetRect = Pixels(origin, scale, 0, 0, sheet.SheetWidthEighths, sheet.SheetHeightEighths);

            using (var paper = new SolidBrush(Paper))
                g.FillRectangle(paper, sheetRect);

            foreach (var scrap in sheet.Scrap)
                DrawScrap(g, origin, scale, scrap);

            foreach (var piece in sheet.Placements)
                DrawPiece(g, origin, scale, piece);

            using (var border = new Pen(SheetLine, 1.8f))
                g.DrawRectangle(border, sheetRect);

            return sheetRect;
        }

        // ── Pieces ────────────────────────────────────────────────────────────

        private static void DrawPiece(Graphics g, PointF origin, float scale, CutPlacement piece)
        {
            var rect = Pixels(origin, scale, piece.X, piece.Y, piece.WidthEighths, piece.HeightEighths);
            var (fill, line) = Palette[Math.Abs(piece.SerialNumber) % Palette.Length];

            using (var body = new SolidBrush(fill))
                g.FillRectangle(body, rect);
            using (var edge = new Pen(line, 1.2f))
                g.DrawRectangle(edge, rect);

            // The two numbers belong to the two edges they measure: width along the top,
            // height down the left — the same way the reference plans read.
            DrawPieceLabels(g, rect,
                Fmt.FormatCompactFromEighths(piece.WidthEighths),
                Fmt.FormatCompactFromEighths(piece.HeightEighths));

            if (piece.Rotated && rect.Width > 24 && rect.Height > 24)
            {
                using var mark = new Font("Segoe UI", 8f);
                var size = g.MeasureString("↻", mark);
                using var grey = new SolidBrush(Color.FromArgb(150, 110, 110, 110));
                g.DrawString("↻", mark, grey, rect.Right - size.Width - 2, rect.Bottom - size.Height - 1);
            }
        }

        private static void DrawScrap(Graphics g, PointF origin, float scale, ScrapRect scrap)
        {
            var rect = Pixels(origin, scale, scrap.X, scrap.Y, scrap.WidthEighths, scrap.HeightEighths);
            if (rect.Width < 3 || rect.Height < 3) return;

            using (var hatch = new HatchBrush(HatchStyle.LightDownwardDiagonal, ScrapLine, ScrapFill))
                g.FillRectangle(hatch, rect);
            using (var edge = new Pen(ScrapLine) { DashStyle = DashStyle.Dot })
                g.DrawRectangle(edge, rect);

            // Only worth a label while the off-cut could still be used.
            if (rect.Width < 44 || rect.Height < 26) return;

            string label = $"{Fmt.FormatCompactFromEighths(scrap.WidthEighths)} × " +
                           $"{Fmt.FormatCompactFromEighths(scrap.HeightEighths)}";
            using var font = new Font("Segoe UI", 7.5f);
            using var ink  = new SolidBrush(Subtle);
            var size = g.MeasureString(label, font);
            g.DrawString(label, font, ink,
                rect.X + (rect.Width - size.Width) / 2f,
                rect.Y + (rect.Height - size.Height) / 2f);
        }

        // ── Labels ────────────────────────────────────────────────────────────

        /// <summary>
        /// Writes the two numbers a piece needs: width along the top edge, height down the left.
        ///
        /// A number is only drawn where it actually fits. On an 8 ft × 4 ft sheet a 3-inch strip
        /// is a few pixels wide, and a label forced in there overlaps its neighbour and reads as
        /// a wrong size — worse than no label. Anything the box cannot hold is listed under the
        /// sheet instead.
        /// </summary>
        private static void DrawPieceLabels(Graphics g, RectangleF rect, string widthText, string heightText)
        {
            using var bold  = new Font("Segoe UI", 9f, FontStyle.Bold);
            using var small = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            using var ink   = new SolidBrush(Ink);

            float band = g.MeasureString("0", bold).Height;

            // The height runs up the left edge and owns a strip as wide as the text is tall.
            float strip = 0;
            if (rect.Height >= g.MeasureString(heightText, bold).Width + 4 && rect.Width >= band + 6)
            {
                strip = band;
                DrawVertical(g, rect, heightText, strip, bold, ink);
            }

            // The width then gets whatever is left of the top edge, so the two never share space.
            float room = rect.Width - strip;
            if (rect.Height < band + 4) return;

            var font = g.MeasureString(widthText, bold).Width <= room - 2 ? bold : small;
            float textW = g.MeasureString(widthText, font).Width;

            if (textW <= room && room >= 12)
            {
                DrawLabel(g, widthText, font, ink,
                    rect.X + strip + (room - textW) / 2f,
                    rect.Y + (band - g.MeasureString(widthText, font).Height) / 2f + 2);
                return;
            }

            // A long strip has no top edge wide enough for its own width, but it is tall — so
            // the number runs up the middle of it instead of disappearing off the plan.
            if (rect.Width < band + 4) return;
            if (rect.Height < g.MeasureString(widthText, small).Width + 6) return;

            DrawUpTheMiddle(g, rect, widthText, small, ink);
        }

        /// <summary>Text on a pale plate, because ink alone on a coloured fill is hard to read.</summary>
        private static void DrawLabel(Graphics g, string text, Font font, Brush ink, float x, float y)
        {
            var size = g.MeasureString(text, font);
            using (var plate = new SolidBrush(Color.FromArgb(165, Color.White)))
                g.FillRectangle(plate, x - 1, y - 1, size.Width + 2, size.Height);
            g.DrawString(text, font, ink, x, y);
        }

        /// <summary>The width number bottom-to-top through the middle of a narrow piece.</summary>
        private static void DrawUpTheMiddle(Graphics g, RectangleF rect, string text, Font font, Brush ink)
        {
            float width = g.MeasureString(text, font).Width;
            float x = rect.X + rect.Width / 2f - g.MeasureString(text, font).Height / 2f;

            var state = g.Save();
            g.TranslateTransform(x, rect.Bottom - 2);
            g.RotateTransform(-90);
            g.DrawString(text, font, ink, 1, (rect.Height - width) / 2f - 2);
            g.Restore(state);
        }

        /// <summary>Text running bottom-to-top along the piece's left edge.</summary>
        private static void DrawVertical(Graphics g, RectangleF rect, string text, float strip, Font font, Brush ink)
        {
            var width = g.MeasureString(text, font).Width;

            var state = g.Save();
            g.TranslateTransform(rect.X + 2, rect.Bottom - (rect.Height - width) / 2f);
            g.RotateTransform(-90);
            g.DrawString(text, font, ink, 0, 0);
            g.Restore(state);
        }

        private static RectangleF Pixels(PointF origin, float scale, int x, int y, int w, int h) =>
            new(origin.X + x * scale, origin.Y + y * scale, w * scale, h * scale);
    }
}
