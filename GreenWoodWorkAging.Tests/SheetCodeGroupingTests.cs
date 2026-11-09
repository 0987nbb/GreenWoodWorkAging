using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using GreenWoodWorkAging.Models;
using Xunit;

namespace GreenWoodWorkAging.Tests
{
    /// <summary>
    /// Sheets are separated the way they are cut: a new sheet name opens a new block, with a band
    /// above it carrying that name, and the name on every line of the block. Nothing else about the
    /// list — the serials, the Edit and Delete buttons, what they act on — may move.
    /// </summary>
    [Collection("SheetStock")]
    public class SheetCodeGroupingTests
    {
        private static readonly Type Form = typeof(CarpenterMeasurementCalculatorForm);
        private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        private static MeasurementEntry Row(int serial, string sheet, int h, int w, int qty = 1, int half = 0) => new()
        {
            SerialNumber = serial,
            HeightInches = h, WidthInches = w, Quantity = qty,
            HeightHalf   = half,
            SheetCode    = sheet
        };

        private sealed class PaintedGrid
        {
            public List<string[]> Rows { get; } = new();
            public List<int> RowMap { get; } = new();
            public string HeightBoxAfterEdit { get; set; } = "";

            /// <summary>The height of each gap row, and whether any of them carries a live button.</summary>
            public List<int> BandHeights { get; } = new();
            public List<int> NormalHeights { get; } = new();
            public bool BandHasButtons { get; set; }
        }

        /// <summary>
        /// Fills the live list, rebuilds the grid and hands back what the shop actually sees:
        /// every cell on screen, the map saying which rows are real measurements, and what the
        /// height box holds once the given size is clicked with Edit.
        /// </summary>
        private static PaintedGrid Painted(int editIndex, params MeasurementEntry[] rows)
        {
            var painted = new PaintedGrid();
            Exception? failure = null;

            var thread = new Thread(() =>
            {
                try
                {
                    using var form = new CarpenterMeasurementCalculatorForm();
                    var list = (List<MeasurementEntry>)Form.GetField("_measurements", Private)!.GetValue(form)!;
                    foreach (var r in rows) list.Add(r);

                    Form.GetMethod("RefreshGrid", Private)!.Invoke(form, null);
                    for (int i = 0; i < 20; i++) Application.DoEvents();

                    var dgv = (DataGridView)Form.GetField("dgv", Private)!.GetValue(form)!;
                    foreach (DataGridViewRow row in dgv.Rows)
                        painted.Rows.Add(Enumerable.Range(0, dgv.Columns.Count)
                                                   .Select(c => row.Cells[c].Value?.ToString() ?? "")
                                                   .ToArray());
                    painted.RowMap.AddRange((List<int>)Form.GetField("_gridRowToMeasurement", Private)!.GetValue(form)!);

                    for (int i = 0; i < dgv.Rows.Count; i++)
                    {
                        var row = dgv.Rows[i];
                        if (painted.RowMap[i] != -1) { painted.NormalHeights.Add(row.Height); continue; }

                        painted.BandHeights.Add(row.Height);
                        if (row.Cells.OfType<DataGridViewButtonCell>()
                                     .Any(c => c.UseColumnTextForButtonValue))
                            painted.BandHasButtons = true;
                    }

                    if (editIndex >= 0)
                    {
                        Form.GetMethod("LoadForEdit", Private)!.Invoke(form, new object[] { editIndex });
                        painted.HeightBoxAfterEdit =
                            ((TextBox)Form.GetField("txtHeight", Private)!.GetValue(form)!).Text;
                    }
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "The grid never settled.");

            Assert.Null(failure);
            return painted;
        }

        /// <summary>A band carries at most the name of the block below it — no serial, no size, no buttons.</summary>
        private static bool IsBand(string[] cells)
            => cells[0].Length == 0 && cells.Skip(2).All(c => c.Length == 0);

        [Fact]
        public void TwoSheets_ShowOneEmptyBandBetweenThem()
        {
            var grid = Painted(-1,
                Row(1, "KMI 123",     80, 18), Row(2, "KMI 123", 40, 12),
                Row(3, "Super Glass", 30, 10), Row(4, "Super Glass", 24, 8),
                Row(5, "KMI 123",     20, 6));

            Assert.Equal(5 + 2, grid.Rows.Count);      // five sizes, a band each time the sheet changed
            Assert.Equal(2, grid.RowMap.Count(i => i == -1));
        }

        [Fact]
        public void TheSheetNameIsOnEveryLineAndOnTheBandAboveItsBlock()
        {
            var grid = Painted(-1,
                Row(1, "KMI 123",     80, 18), Row(2, "KMI 123", 40, 12),
                Row(3, "Super Glass", 30, 10));

            Assert.Equal("KMI 123", grid.Rows[0][1]);
            Assert.Equal("KMI 123", grid.Rows[1][1]);   // every line of a block says its sheet
            Assert.True(IsBand(grid.Rows[2]), "the gap between two sheets shows: [" +
                                              string.Join("|", grid.Rows[2]) + "]");
            Assert.Equal("Super Glass", grid.Rows[2][1]);   // the band names what starts under it
            Assert.Equal("Super Glass", grid.Rows[3][1]);
        }

        [Fact]
        public void SizesWithNoSheetName_ListJustAsTheyAlwaysDid()
        {
            var grid = Painted(-1, Row(1, "", 80, 18), Row(2, "", 40, 12), Row(3, "", 30, 10));

            Assert.Equal(3, grid.Rows.Count);
            Assert.DoesNotContain(-1, grid.RowMap);
            Assert.Equal(new[] { "1", "2", "3" }, grid.Rows.Select(r => r[0]).ToArray());
        }

        [Fact]
        public void TheSizeAfterABand_EditsItsOwnRow()
        {
            // The band sits between the sheets, so the fourth grid row is measurement index 2.
            var grid = Painted(2,
                Row(1, "KMI 123",     80, 18), Row(2, "KMI 123", 40, 12),
                Row(3, "Super Glass", 30, 10), Row(4, "Super Glass", 24, 8));

            Assert.Equal(2, grid.RowMap[3]);            // the fourth row on screen is the third size
            Assert.Equal("30", grid.HeightBoxAfterEdit);
        }

        [Fact]
        public void ASadyTypedForOneSize_IsShownTheWayItWasTyped()
        {
            var plain = Row(1, "KMI 123", 8, 8);
            var sady  = Row(2, "KMI 123", 8, 8, half: 1);

            var grid = Painted(-1, plain, sady);
            Assert.Equal("8",    grid.Rows[0][2]);
            Assert.Equal("8.06", grid.Rows[1][2]);
        }

        /// <summary>
        /// The gap is a gap: a thin band of the card's own colour, with no Edit or Delete
        /// button a shop could click and get nothing from.
        /// </summary>
        [Fact]
        public void TheGapRow_IsThinAndCarriesNoButtons()
        {
            var grid = Painted(-1, Row(1, "KMI 123", 89, 24), Row(2, "Super Glass", 30, 10));

            var band = Assert.Single(grid.BandHeights);
            Assert.True(band < grid.NormalHeights.Min(),
                        $"the gap ({band} px) should read as a gap beside a {grid.NormalHeights.Min()} px size");
            Assert.False(grid.BandHasButtons);
        }

        /// <summary>The list as the shop sees it, drawn to a file so the gap can be looked at.</summary>
        [Fact]
        public void TheSeparatedList_CanBeDrawn()
        {
            string dir = Path.Combine(Path.GetTempPath(), "gwa-separated-grid");
            Directory.CreateDirectory(dir);

            Exception? failure = null;
            int painted = 0;

            var thread = new Thread(() =>
            {
                try
                {
                    using var form = new CarpenterMeasurementCalculatorForm();
                    form.Size = new Size(1280, 860);

                    // Shown first: opening the window starts a fresh bill, and the list it hands
                    // out then is the one the grid is built from.
                    form.Show();
                    for (int i = 0; i < 20; i++) Application.DoEvents();

                    var list = (List<MeasurementEntry>)Form.GetField("_measurements", Private)!.GetValue(form)!;
                    list.Add(Row(1, "KMI 123",     89, 24));
                    list.Add(Row(2, "KMI 123",     80, 18));
                    list.Add(Row(3, "Super Glass", 30, 10));
                    list.Add(Row(4, "Super Glass", 24, 8));

                    Form.GetMethod("RefreshGrid", Private)!.Invoke(form, null);
                    for (int i = 0; i < 20; i++) Application.DoEvents();

                    var dgv = (DataGridView)Form.GetField("dgv", Private)!.GetValue(form)!;
                    painted = dgv.Rows.Count;

                    using var bmp = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                    bmp.Save(Path.Combine(dir, "separated-grid.png"), ImageFormat.Png);

                    form.Close();
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(90)), "Drawing the bill window never finished.");

            Assert.Null(failure);
            Assert.Equal(5, painted);   // four sizes and the gap before the second sheet
            Assert.True(File.Exists(Path.Combine(dir, "separated-grid.png")));
        }
    }
}
