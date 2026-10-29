using GreenWoodWorkAging.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PdfColor = QuestPDF.Infrastructure.Color;

namespace GreenWoodWorkAging.Services
{
    /// <summary>Everything a printed bill says about one customer's job.</summary>
    public sealed class BillPdfData
    {
        public string CustomerName { get; init; } = "";
        public string Phone        { get; init; } = "";
        public DateTime Date       { get; init; } = DateTime.Today;
        public int    BillNumber   { get; init; }
        public IReadOnlyList<MeasurementEntry> Rows    { get; init; } = Array.Empty<MeasurementEntry>();
        public BillCharges Charges { get; init; } = new();
        public byte[]? Logo        { get; init; }
    }

    /// <summary>
    /// Renders a customer bill as a letterhead-style A4 PDF: shop header with the logo,
    /// the measurement table exactly as the grid shows it, the tape / gari / sheet-cut
    /// charges and the final amount. Every figure comes from the same calculator and
    /// formatter the on-screen bill uses, so print and screen can never disagree.
    /// </summary>
    public static class BillPdfService
    {
        public const string ShopName    = "GreenWood Works";
        public const string ShopTagline = "Carpenter measurement & cutting";

        static readonly PdfColor Brand     = PdfColor.FromHex("#34855A");
        static readonly PdfColor BrandDark = PdfColor.FromHex("#1E4B33");
        static readonly PdfColor BrandSoft = PdfColor.FromHex("#EDF5F0");
        static readonly PdfColor Ink       = PdfColor.FromHex("#22312A");
        static readonly PdfColor Subtle    = PdfColor.FromHex("#6C7A72");
        static readonly PdfColor Line      = PdfColor.FromHex("#D9E3DC");
        static readonly PdfColor RowAlt    = PdfColor.FromHex("#F6F9F7");
        static readonly PdfColor Paper     = Colors.White;

        static readonly MeasurementCalculatorService Calc = new();
        static readonly MeasurementDisplayFormatter  Fmt  = new();

        /// <summary>Builds the bill document, ready to be written to PDF or rasterised for a preview.</summary>
        public static IDocument Build(BillPdfData data)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var    totals  = Calc.CalculateGrandTotal(data.Rows);
            int    eighths = totals.GrandTotalEighths;
            string grandIn = Fmt.FormatCompactFromEighths(eighths);
            string grandFt = Fmt.FormatGrandTotalFeetDot(totals);

            return QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginVertical(28);
                    page.MarginHorizontal(36);
                    page.DefaultTextStyle(x => x.FontSize(9).FontColor(Ink));

                    // The letterhead lives in the content, not in page.Header(): a header
                    // band repeats, and the logo would reprint on every page of a long bill.
                    page.Content().Column(col =>
                    {
                        Header(col, data);
                        Content(col, data, totals, eighths, grandIn, grandFt);
                    });

                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(0.6f).LineColor(Line);
                        col.Item().PaddingTop(6).Row(row =>
                        {
                            row.RelativeItem()
                               .Text($"Thank you for your business  ·  Generated {DateTime.Now:dd MMM yyyy  hh:mm tt}")
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

        /// <summary>Writes the bill to <paramref name="path"/>, overwriting an existing file.</summary>
        public static void Generate(string path, BillPdfData data) => Build(data).GeneratePdf(path);

        // ── Header ────────────────────────────────────────────────────────────

        private static void Header(ColumnDescriptor col, BillPdfData data)
        {
            col.Item().Row(row =>
            {
                if (data.Logo != null)
                    row.ConstantItem(106).Height(70).Image(data.Logo);

                row.ConstantItem(14);

                row.RelativeItem().AlignMiddle().Column(brand =>
                {
                    brand.Item().Text(t => t.Span(ShopName).FontSize(22).Bold().FontColor(BrandDark));
                    brand.Item().PaddingTop(3).Text(ShopTagline).FontSize(9.5f).FontColor(Subtle);
                });

                row.ConstantItem(176).AlignMiddle().Background(BrandSoft)
                   .BorderLeft(3).BorderColor(Brand)
                   .PaddingVertical(9).PaddingHorizontal(12).Column(meta =>
                {
                    meta.Item().Text(t => t.Span("MEASUREMENT BILL").FontSize(10).Bold().FontColor(BrandDark));
                    MetaRow("Bill #", data.BillNumber == 0 ? "—" : data.BillNumber.ToString());
                    MetaRow("Date",   data.Date.ToString("dd MMM yyyy"));

                    void MetaRow(string caption, string value) => meta.Item().PaddingTop(3).Row(r =>
                    {
                        r.ConstantItem(40).Text(caption).FontSize(8.5f).Bold().FontColor(BrandDark);
                        r.RelativeItem().Text(value).FontSize(8.5f).FontColor(Subtle);
                    });
                });
            });

            col.Item().PaddingTop(12).LineHorizontal(1.6f).LineColor(Brand);
        }

        // ── Content ──────────────────────────────────────────────────────────

        private static void Content(ColumnDescriptor col, BillPdfData data, GrandTotalResult totals,
                                    int eighths, string grandIn, string grandFt)
        {
            col.Item().PaddingTop(18).Border(0.8f).BorderColor(Line).CornerRadius(6)
               .PaddingHorizontal(14).PaddingVertical(10).Row(row =>
            {
                row.RelativeItem(2).Column(c => Detail(c, "BILLED TO", data.CustomerName));
                row.RelativeItem().Column(c => Detail(c, "PHONE",
                    string.IsNullOrWhiteSpace(data.Phone) ? "—" : data.Phone));
                row.RelativeItem().Column(c => Detail(c, "BILL DATE", data.Date.ToString("dd MMM yyyy")));
                row.RelativeItem().Column(c => Detail(c, "ITEMS", $"{totals.TotalRows} rows · {totals.TotalPieces} pieces"));
            });

            col.Item().PaddingTop(16).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(34);              // #
                    columns.RelativeColumn(1.1f);            // Height
                    columns.RelativeColumn(1.1f);            // Width
                    columns.ConstantColumn(44);              // Qty
                    columns.RelativeColumn(1.5f);            // Tape sides
                    columns.RelativeColumn(1.6f);            // Total inches
                    columns.RelativeColumn(1.5f);            // Feet
                });

                table.Header(header =>
                {
                    HeaderCell(header, "#");
                    HeaderCell(header, "Height (in)");
                    HeaderCell(header, "Width (in)");
                    HeaderCell(header, "Qty");
                    HeaderCell(header, "Tape sides");
                    HeaderCell(header, "Total inches");
                    HeaderCell(header, "Feet");
                });

                int i = 0;
                foreach (var m in data.Rows)
                {
                    var shade = i++ % 2 == 0 ? Paper : RowAlt;
                    RowCell(shade, m.SerialNumber.ToString());
                    RowCell(shade, Fmt.FormatCompact(m.HeightInches, m.HeightSooter));
                    RowCell(shade, Fmt.FormatCompact(m.WidthInches,  m.WidthSooter));
                    RowCell(shade, m.Quantity.ToString());
                    RowCell(shade, MeasurementDisplayFormatter.FormatSides(m.Side));
                    RowCell(shade, Fmt.FormatCompactFromEighths(m.RowTotalEighths));
                    RowCell(shade, $"{m.RowTotalEighths / 96m:0.00}");

                    void RowCell(PdfColor back, string text) =>
                        table.Cell().Background(back).BorderBottom(0.6f).BorderColor(Line)
                             .PaddingVertical(7).PaddingHorizontal(10)
                             .Text(text).FontSize(9).FontColor(Ink);
                }

                table.Cell().ColumnSpan(7).Background(BrandDark).PaddingVertical(9).PaddingHorizontal(10)
                     .Text(t => t.Span($"GRAND TOTAL     {grandIn} in          {grandFt}")
                                 .FontSize(9.5f).Bold().FontColor(Paper));
            });

            // Rows/pieces and the inch/feet grand total are already printed on the items
            // line and the table's total bar, so charges take the full width here.
            if (!data.Charges.IsEmpty)
                col.Item().PaddingTop(22).PreventPageBreak()
                   .Table(table => ChargesTable(table, data.Charges, eighths));
            else
                col.Item().PaddingTop(22)
                   .Text("No rates entered — this bill shows measurements only.")
                   .FontSize(8.5f).FontColor(Subtle);

            col.Item().PaddingTop(36).Row(row =>
            {
                row.RelativeItem().Column(c => Sign(c, "Customer signature"));
                row.ConstantItem(26);
                row.RelativeItem().Column(c => Sign(c, $"For {ShopName}"));
            });
        }

        private static void HeaderCell(TableCellDescriptor header, string caption) =>
            header.Cell().Background(BrandDark).PaddingVertical(8).PaddingHorizontal(10)
                  .Text(caption).FontSize(8.5f).Bold().FontColor(Paper);

        private static void Detail(ColumnDescriptor col, string caption, string value)
        {
            col.Item().Text(caption).FontSize(7.5f).FontColor(Subtle);
            col.Item().PaddingTop(2).Text(value).FontSize(11).Bold().FontColor(Ink);
        }

        private static void Sign(ColumnDescriptor col, string caption)
        {
            col.Item().LineHorizontal(0.8f).LineColor(Subtle);
            col.Item().PaddingTop(5).Text(caption).FontSize(8.5f).FontColor(Subtle);
        }

        // ── Charges ──────────────────────────────────────────────────────────

        private static void ChargesTable(TableDescriptor table, BillCharges charges, int eighths)
        {
            decimal feet = BillCharges.Feet(eighths);

            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn();
                c.ConstantColumn(84);
            });

            table.Cell().ColumnSpan(2).Background(BrandDark).PaddingVertical(7).PaddingHorizontal(12)
                 .Text("CHARGES & AMOUNT").FontSize(9).Bold().FontColor(Paper);

            ChargeRow("Rate per foot", BillCharges.Rupees(charges.RatePerFoot) + " / ft");
            ChargeRow($"Tape amount  ({feet:0.00} ft)", BillCharges.Rupees(charges.TapeAmount(eighths)));
            ChargeRow("Gari carriage", BillCharges.Rupees(charges.CarriageFare));
            ChargeRow($"Sheet cuts  ({charges.SheetCuts} × {BillCharges.Rupees(charges.RatePerSheet)})",
                      BillCharges.Rupees(charges.SheetAmount));

            table.Cell().ColumnSpan(2).Background(Brand).PaddingVertical(10).PaddingHorizontal(12).Row(r =>
            {
                r.RelativeItem().Text("TOTAL AMOUNT").FontSize(10).Bold().FontColor(Paper);
                r.ConstantItem(84).AlignRight()
                 .Text(BillCharges.Rupees(charges.TotalAmount(eighths))).FontSize(11).Bold().FontColor(Paper);
            });

            void ChargeRow(string caption, string amount)
            {
                table.Cell().BorderBottom(0.6f).BorderColor(Line)
                      .PaddingVertical(7).PaddingLeft(12).PaddingRight(6)
                      .Text(caption).FontSize(8.5f).FontColor(Ink);
                table.Cell().BorderBottom(0.6f).BorderColor(Line)
                      .PaddingVertical(7).PaddingRight(12).AlignRight()
                      .Text(amount).FontSize(9).Bold().FontColor(Ink);
            }
        }
    }
}
