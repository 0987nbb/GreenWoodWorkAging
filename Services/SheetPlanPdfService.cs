using GreenWoodWorkAging.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PdfColor = QuestPDF.Infrastructure.Color;

namespace GreenWoodWorkAging.Services
{
    /// <summary>
    /// Prints the cutting plan: the same sheet pictures the Optimize window shows, one block per
    /// sheet, with the sizes listed beside each picture so a number that is too small to sit on a
    /// 3-inch strip is still readable on paper.
    ///
    /// This is a separate document from the customer bill — the bill keeps its own layout and
    /// nothing here feeds into it.
    /// </summary>
    public static class SheetPlanPdfService
    {
        static readonly PdfColor BrandDark = PdfColor.FromHex("#1E4B33");
        static readonly PdfColor Brand     = PdfColor.FromHex("#34855A");
        static readonly PdfColor BrandSoft = PdfColor.FromHex("#EDF5F0");
        static readonly PdfColor Ink       = PdfColor.FromHex("#22312A");
        static readonly PdfColor Subtle    = PdfColor.FromHex("#6C7A72");
        static readonly PdfColor Line      = PdfColor.FromHex("#D9E3DC");
        static readonly PdfColor Paper     = Colors.White;

        private const int SheetImageWidth  = 240;   // points on the page
        private const int SheetImageHeight = 480;   // a 4 ft × 8 ft sheet, so 1 : 2

        static readonly MeasurementDisplayFormatter Fmt = new();

        public static void Generate(string path, OptimizationResult plan, string customer, DateTime date,
                                    byte[]? logo = null)
            => Build(plan, customer, date, logo).GeneratePdf(path);

        public static IDocument Build(OptimizationResult plan, string customer, DateTime date, byte[]? logo = null)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            // Which sheet numbers belong to which material, for the meta box and the intro line.
            var bySource = plan.Sheets
                               .GroupBy(s => s.Source)
                               .Where(g => g.Key.Length > 0)
                               .OrderBy(g => g.Min(s => s.Number))
                               .ToList();

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginVertical(28);
                    page.MarginHorizontal(36);
                    page.DefaultTextStyle(x => x.FontSize(9).FontColor(Ink));

                    page.Content().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            if (logo != null)
                                row.ConstantItem(106).Height(70).Image(logo);

                            row.ConstantItem(14);

                            row.RelativeItem().AlignMiddle().Column(brand =>
                            {
                                brand.Item().Text(t => t.Span(BillPdfService.ShopName)
                                               .FontSize(22).Bold().FontColor(BrandDark));
                                brand.Item().PaddingTop(3).Text("Sheet cutting plan").FontSize(9.5f).FontColor(Subtle);
                            });

                            row.ConstantItem(190).AlignMiddle().Background(BrandSoft)
                               .BorderLeft(3).BorderColor(Brand)
                               .PaddingVertical(9).PaddingHorizontal(12).Column(meta =>
                            {
                                meta.Item().Text(t => t.Span("CUTTING PLAN").FontSize(10).Bold().FontColor(BrandDark));
                                MetaRow("Customer", string.IsNullOrWhiteSpace(customer) ? "—" : customer);
                                MetaRow("Date",    date.ToString("dd MMM yyyy"));
                                MetaRow("Sheets",  $"{plan.Sheets.Count} × 8 ft × 4 ft");
                                if (bySource.Count > 0)
                                    MetaRow("From", bySource.Count == 1 ? bySource[0].Key
                                                                        : $"{bySource.Count} sheet codes");

                                void MetaRow(string caption, string value) => meta.Item().PaddingTop(3).Row(r =>
                                {
                                    r.ConstantItem(52).Text(caption).FontSize(8.5f).Bold().FontColor(BrandDark);
                                    r.RelativeItem().Text(value).FontSize(8.5f).FontColor(Subtle);
                                });
                            });
                        });

                        col.Item().PaddingTop(12).LineHorizontal(1.6f).LineColor(Brand);

                        col.Item().PaddingTop(10).Text(t =>
                        {
                            t.Span($"{plan.PieceCount} pieces   ·   ").FontSize(9.5f).FontColor(BrandDark);
                            t.Span($"{plan.Sheets.Count} sheet(s)   ·   ").FontSize(9.5f).FontColor(BrandDark);
                            t.Span($"{plan.OverallUsedPercent:0.#}% of the plywood used").FontSize(9.5f).FontColor(BrandDark);
                        });

                        // A bill cut from several materials lists which sheets are which, so the
                        // carpenter never has to work it out from the sizes.
                        if (bySource.Count > 1)
                        {
                            string split = string.Join("     ", bySource.Select(g =>
                            {
                                int first = g.Min(s => s.Number);
                                int last  = g.Max(s => s.Number);
                                return $"{g.Key}: sheet{(g.Count() == 1 ? "" : "s")} " +
                                       (first == last ? first.ToString() : $"{first}–{last}");
                            }));
                            col.Item().PaddingTop(6).Text(split).FontSize(8.5f).Bold().FontColor(BrandDark);
                        }

                        if (plan.Unplaced.Count > 0)
                            // No ⚠ here: the PDF font (Lato) has no such glyph and QuestPDF 2026.9
                            // refuses to draw text it cannot render.
                            col.Item().PaddingTop(6).Text(
                                $"NOTE: {plan.Unplaced.Count} piece(s) are larger than one sheet and are not on this plan.")
                               .FontSize(8.5f).Bold().FontColor(PdfColor.FromHex("#B32828"));

                        foreach (var sheet in plan.Sheets)
                            col.Item().PaddingTop(16).PreventPageBreak().Row(row => Sheet(row, sheet));
                    });

                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(0.6f).LineColor(Line);
                        col.Item().PaddingTop(6).Row(row =>
                        {
                            row.RelativeItem()
                               .Text($"Dashed areas are unused plywood.  ·  Generated {DateTime.Now:dd MMM yyyy  hh:mm tt}")
                               .FontSize(7.5f).FontColor(Subtle);
                            row.RelativeItem().AlignRight().Text(t =>
                            {
                                t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Subtle));
                                t.Span("Page ");
                                t.CurrentPageNumber();
                                t.Span(" of ");
                                t.TotalPages();
                            });
                        });
                    });
                });
            });
        }

        // ── One sheet ─────────────────────────────────────────────────────────

        private static void Sheet(RowDescriptor row, SheetLayout sheet)
        {
            row.ConstantItem(SheetImageWidth).Height(SheetImageHeight)
               .Image(RenderPng(sheet, SheetImageWidth * 3, SheetImageHeight * 3));

            row.ConstantItem(16);

            row.RelativeItem().AlignTop().Column(col =>
            {
                col.Item().Background(BrandDark).PaddingVertical(7).PaddingHorizontal(10).Text(t =>
                {
                    t.Span($"Sheet {sheet.Number}").FontSize(10).Bold().FontColor(Paper);
                    if (!string.IsNullOrWhiteSpace(sheet.Source))
                        t.Span($"   {sheet.Source}").FontSize(10).Bold().FontColor(PdfColor.FromHex("#BFE3CE"));
                    t.Span($"      {sheet.Placements.Count} pieces · {sheet.UsedPercent:0.#}% used")
                     .FontSize(8.5f).FontColor(Paper);
                });

                col.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(34);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(1.2f);
                        c.ConstantColumn(40);
                    });

                    table.Header(header =>
                    {
                        Head(header, "#");
                        Head(header, "Height");
                        Head(header, "Width");
                        Head(header, "Qty");

                        void Head(TableCellDescriptor h, string caption) =>
                            h.Cell().Background(BrandSoft).PaddingVertical(5).PaddingHorizontal(8)
                             .Text(caption).FontSize(8).Bold().FontColor(BrandDark);
                    });

                    foreach (var group in PiecesOn(sheet))
                    {
                        BodyCell(group.Serial.ToString());
                        BodyCell(Fmt.FormatCompactFromEighths(group.HeightEighths));
                        BodyCell(Fmt.FormatCompactFromEighths(group.WidthEighths));
                        BodyCell(group.Count.ToString());
                    }

                    void BodyCell(string text) =>
                        table.Cell().BorderBottom(0.6f).BorderColor(Line)
                             .PaddingVertical(6).PaddingHorizontal(8)
                             .Text(text).FontSize(9).FontColor(Ink);
                });
            });
        }

        /// <summary>
        /// The sizes cut from one sheet, counted up. A piece turned 90° on the sheet is listed at
        /// the size it was ordered at, because that is the row the carpenter recognises.
        /// </summary>
        private record SheetPieceGroup(int Serial, int HeightEighths, int WidthEighths, int Count);

        private static List<SheetPieceGroup> PiecesOn(SheetLayout sheet) =>
            sheet.Placements
                 .GroupBy(p => (
                     Serial:         p.SerialNumber,
                     TypedHeight:    p.Rotated ? p.WidthEighths : p.HeightEighths,
                     TypedWidth:     p.Rotated ? p.HeightEighths : p.WidthEighths))
                 .OrderBy(g => g.Key.Serial)
                 .Select(g => new SheetPieceGroup(g.Key.Serial, g.Key.TypedHeight, g.Key.TypedWidth, g.Count()))
                 .ToList();

        /// <summary>The same picture the window paints, at three times the size so it prints sharp.</summary>
        private static byte[] RenderPng(SheetLayout sheet, int widthPx, int heightPx)
        {
            using var bmp = new System.Drawing.Bitmap(widthPx, heightPx);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
                SheetDiagramRenderer.Draw(g, new System.Drawing.Rectangle(0, 0, widthPx, heightPx), sheet);

            using var stream = new MemoryStream();
            bmp.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            return stream.ToArray();
        }
    }
}
