using GreenWoodWorkAging.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PdfColor = QuestPDF.Infrastructure.Color;

namespace GreenWoodWorkAging.Services
{
    /// <summary>
    /// Renders a standalone manual invoice as a clean, professional letterhead-style A4 PDF.
    /// Includes shop branding, customer information, tape edge-banding breakdown, sheet cutting
    /// charges, carriage / transportation, and grand total.
    /// </summary>
    public static class ManualInvoicePdfService
    {
        public const string ShopName    = "GreenWood Works";
        public const string ShopTagline = "Carpenter measurement & cutting";
        public const string OwnerName   = "Shabir";
        public const string OwnerPhone  = "0345 3345136";

        static readonly PdfColor Brand     = PdfColor.FromHex("#34855A");
        static readonly PdfColor BrandDark = PdfColor.FromHex("#1E4B33");
        static readonly PdfColor BrandSoft = PdfColor.FromHex("#EDF5F0");
        static readonly PdfColor Ink       = PdfColor.FromHex("#22312A");
        static readonly PdfColor Subtle    = PdfColor.FromHex("#6C7A72");
        static readonly PdfColor Line      = PdfColor.FromHex("#D9E3DC");
        static readonly PdfColor RowAlt    = PdfColor.FromHex("#F6F9F7");
        static readonly PdfColor Paper     = Colors.White;

        public static IDocument Build(ManualInvoice data, byte[]? logoBytes)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            return QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginVertical(28);
                    page.MarginHorizontal(36);
                    page.DefaultTextStyle(x => x.FontSize(9).FontColor(Ink));

                    page.Content().Column(col =>
                    {
                        Header(col, data, logoBytes);
                        CustomerDetails(col, data);
                        ItemsTable(col, data);
                        TotalsAndSignatures(col, data);
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

        public static void Generate(string path, ManualInvoice data, byte[]? logoBytes) =>
            Build(data, logoBytes).GeneratePdf(path);

        // ── Header ────────────────────────────────────────────────────────────

        private static void Header(ColumnDescriptor col, ManualInvoice data, byte[]? logo)
        {
            col.Item().Row(row =>
            {
                if (logo != null)
                    row.ConstantItem(106).Height(70).Image(logo);

                row.ConstantItem(14);

                row.RelativeItem().AlignMiddle().Column(brand =>
                {
                    brand.Item().Text(t => t.Span(ShopName).FontSize(22).Bold().FontColor(BrandDark));
                    brand.Item().PaddingTop(2).Text(ShopTagline).FontSize(10).FontColor(Subtle);
                    brand.Item().PaddingTop(2).Text($"Contact: {OwnerPhone} ({OwnerName})").FontSize(9).FontColor(Subtle);
                });

                row.ConstantItem(180).AlignMiddle().Background(BrandSoft).Padding(10).Column(meta =>
                {
                    meta.Item().Text(t => t.Span("INVOICE / BILL").FontSize(11).Bold().FontColor(BrandDark));
                    MetaRow(meta, "Invoice #", data.Id == 0 ? "Draft" : $"INV-{data.Id:D4}");
                    MetaRow(meta, "Date",      data.InvoiceDate.ToString("dd MMM yyyy"));
                });
            });

            col.Item().PaddingTop(12).LineHorizontal(1.2f).LineColor(Brand);
        }

        private static void MetaRow(ColumnDescriptor meta, string label, string value)
        {
            meta.Item().PaddingTop(2).Row(r =>
            {
                r.ConstantItem(55).Text(label).FontSize(8.5f).FontColor(Subtle);
                r.RelativeItem().AlignRight().Text(value).FontSize(8.5f).Bold().FontColor(Ink);
            });
        }

        // ── Customer Details ──────────────────────────────────────────────────

        private static void CustomerDetails(ColumnDescriptor col, ManualInvoice data)
        {
            col.Item().PaddingVertical(14).Row(row =>
            {
                row.RelativeItem().Border(0.8f).BorderColor(Line).Background(RowAlt).Padding(10).Column(c =>
                {
                    c.Item().Text("CUSTOMER INFORMATION").FontSize(7.5f).Bold().FontColor(Subtle);
                    c.Item().PaddingTop(3).Text(string.IsNullOrWhiteSpace(data.CustomerName) ? "—" : data.CustomerName)
                     .FontSize(11).Bold().FontColor(BrandDark);
                    if (!string.IsNullOrWhiteSpace(data.PhoneNumber))
                        c.Item().PaddingTop(2).Text($"Phone: {data.PhoneNumber}").FontSize(9).FontColor(Ink);
                });
            });
        }

        // ── Items Table ───────────────────────────────────────────────────────

        private static void ItemsTable(ColumnDescriptor col, ManualInvoice data)
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(36);   // #
                    c.RelativeColumn(3);    // Description
                    c.RelativeColumn(2);    // Details
                    c.RelativeColumn(2);    // Rate
                    c.RelativeColumn(2);    // Total Amount
                });

                // Header
                table.Header(h =>
                {
                    HeaderCell(h, "#", true);
                    HeaderCell(h, "SERVICE / ITEM DESCRIPTION", false);
                    HeaderCell(h, "QUANTITY / LENGTH", false);
                    HeaderCell(h, "RATE", false);
                    HeaderCell(h, "AMOUNT (PKR)", true);
                });

                int rowNum = 1;

                // Row 1: Tape Edge Banding
                BodyRow(table, rowNum++, "Tape Edge Banding",
                        $"{data.TotalTapesFeet:0.##} ft",
                        $"Rs {data.TapeRate:N2} / ft",
                        $"Rs {data.TapeAmount:N2}", false);

                // Row 2: Sheet Cutting
                BodyRow(table, rowNum++, "Sheet Cutting",
                        $"{data.SheetCuts} cuts",
                        $"Rs {data.RatePerSheetCut:N2} / cut",
                        $"Rs {data.CuttingAmount:N2}", true);

                // Row 3: Carriage (if applicable)
                if (data.CarriageCharges > 0)
                {
                    BodyRow(table, rowNum++, "Carriage / Transportation",
                            "Transport charges",
                            "—",
                            $"Rs {data.CarriageCharges:N2}", false);
                }
            });
        }

        private static void HeaderCell(TableCellDescriptor header, string caption, bool rightAlign)
        {
            var cell = header.Cell().Background(BrandDark).PaddingVertical(8).PaddingHorizontal(8);
            if (rightAlign)
                cell.AlignRight().Text(caption).FontSize(8.5f).Bold().FontColor(Paper);
            else
                cell.Text(caption).FontSize(8.5f).Bold().FontColor(Paper);
        }

        private static void BodyRow(TableDescriptor table, int sr, string title, string details, string rate, string amount, bool alt)
        {
            var bg = alt ? RowAlt : Paper;

            table.Cell().Background(bg).BorderBottom(0.6f).BorderColor(Line).PaddingVertical(9).PaddingHorizontal(8)
                 .AlignCenter().Text(sr.ToString()).FontSize(9).FontColor(Subtle);

            table.Cell().Background(bg).BorderBottom(0.6f).BorderColor(Line).PaddingVertical(9).PaddingHorizontal(8)
                 .Text(title).FontSize(9.5f).Bold().FontColor(Ink);

            table.Cell().Background(bg).BorderBottom(0.6f).BorderColor(Line).PaddingVertical(9).PaddingHorizontal(8)
                 .Text(details).FontSize(9).FontColor(Ink);

            table.Cell().Background(bg).BorderBottom(0.6f).BorderColor(Line).PaddingVertical(9).PaddingHorizontal(8)
                 .Text(rate).FontSize(9).FontColor(Ink);

            table.Cell().Background(bg).BorderBottom(0.6f).BorderColor(Line).PaddingVertical(9).PaddingHorizontal(8)
                 .AlignRight().Text(amount).FontSize(9.5f).Bold().FontColor(BrandDark);
        }

        // ── Totals & Signatures ───────────────────────────────────────────────

        private static void TotalsAndSignatures(ColumnDescriptor col, ManualInvoice data)
        {
            col.Item().PaddingTop(16).Row(row =>
            {
                // Signatures / notes on the left
                row.RelativeItem(5).Column(left =>
                {
                    if (!string.IsNullOrWhiteSpace(data.Notes))
                    {
                        left.Item().Border(0.6f).BorderColor(Line).Background(RowAlt).Padding(8).Column(noteBox =>
                        {
                            noteBox.Item().Text("NOTES / REMARKS").FontSize(7.5f).Bold().FontColor(Subtle);
                            noteBox.Item().PaddingTop(2).Text(data.Notes).FontSize(8.5f).FontColor(Ink);
                        });
                        left.Item().PaddingTop(18);
                    }
                    else
                    {
                        left.Item().PaddingTop(36);
                    }

                    left.Item().Row(signs =>
                    {
                        signs.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.8f).LineColor(Subtle);
                            c.Item().PaddingTop(4).Text("Customer Signature").FontSize(8f).FontColor(Subtle);
                        });

                        signs.ConstantItem(24);

                        signs.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.8f).LineColor(Subtle);
                            c.Item().PaddingTop(4).Text($"For {ShopName}").FontSize(8f).Bold().FontColor(Subtle);
                            c.Item().Text($"{OwnerName} ({OwnerPhone})").FontSize(7.5f).FontColor(Subtle);
                        });
                    });
                });

                row.ConstantItem(20);

                // Charges breakdown box on the right
                row.RelativeItem(4).Border(1f).BorderColor(Brand).Column(right =>
                {
                    right.Item().Background(BrandDark).PaddingVertical(6).PaddingHorizontal(10)
                         .Text("BILL SUMMARY").FontSize(9).Bold().FontColor(Paper);

                    SummaryRow(right, "Tape Amount", $"Rs {data.TapeAmount:N2}");
                    SummaryRow(right, "Sheet Cutting Amount", $"Rs {data.CuttingAmount:N2}");
                    if (data.CarriageCharges > 0)
                        SummaryRow(right, "Carriage / Transportation", $"Rs {data.CarriageCharges:N2}");

                    right.Item().Background(Brand).PaddingVertical(8).PaddingHorizontal(10).Row(r =>
                    {
                        r.RelativeItem().Text("GRAND TOTAL").FontSize(10.5f).Bold().FontColor(Paper);
                        r.RelativeItem().AlignRight().Text($"Rs {data.GrandTotal:N2}").FontSize(11.5f).Bold().FontColor(Paper);
                    });
                });
            });
        }

        private static void SummaryRow(ColumnDescriptor col, string label, string amount)
        {
            col.Item().BorderBottom(0.6f).BorderColor(Line).PaddingVertical(6).PaddingHorizontal(10).Row(r =>
            {
                r.RelativeItem().Text(label).FontSize(8.5f).FontColor(Ink);
                r.RelativeItem().AlignRight().Text(amount).FontSize(9f).Bold().FontColor(Ink);
            });
        }
    }
}
