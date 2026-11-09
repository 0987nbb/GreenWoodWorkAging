using System.Drawing;
using System.Linq;
using System.Reflection;
using Xunit;

namespace GreenWoodWorkAging.Tests
{
    /// <summary>
    /// The totals tiles live either beside the grid or under it, and the space they leave behind
    /// has to go to the grid — otherwise a wide window shows four rows and a grey strip.
    /// </summary>
    [Collection("SheetStock")]
    public class TotalsRailLayoutTests
    {
        private static readonly Type Form = typeof(CarpenterMeasurementCalculatorForm);
        private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [Fact]
        public void TheTilesBesideTheGrid_FreeTheBandUnderIt()
        {
            Exception? failure = null;
            SizeF bandAfter = SizeF.Empty;
            int railTiles = 0;

            var thread = new Thread(() =>
            {
                try
                {
                    using var form = new CarpenterMeasurementCalculatorForm();
                    form.WindowState = FormWindowState.Maximized;
                    form.Show();
                    for (int i = 0; i < 40; i++) Application.DoEvents();

                    var grid = AllControls(form).OfType<DataGridView>().Single();
                    var card = (TableLayoutPanel)grid.Parent!;

                    // This screen is too small for both a wide grid and a tall card, so the fixed
                    // rows are trimmed and the columns narrowed: the same spare width the shop's
                    // big monitor leaves to the right of the grid.
                    var outer = (TableLayoutPanel)card.Parent!.Parent!;
                    for (int i = 0; i < outer.RowStyles.Count; i++)
                        if (outer.RowStyles[i].SizeType == SizeType.Absolute && i != 2)
                            outer.RowStyles[i].Height = 16;
                    foreach (DataGridViewColumn c in grid.Columns)
                    {
                        if (c.Width <= 40) continue;
                        c.MinimumWidth = 20;
                        c.Width = c.Width / 2;
                    }
                    outer.PerformLayout();
                    card.PerformLayout();
                    for (int i = 0; i < 60; i++) Application.DoEvents();

                    var band = (Control)Form.GetField("_tilesBand", Private)!.GetValue(form)!;
                    bandAfter = band.Size;
                    railTiles = ((Control)Form.GetField("_tilesRail", Private)!.GetValue(form)!).Controls.Count;
                    form.Close();
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(90)), "The layout never settled.");

            Assert.Null(failure);
            Assert.Equal(4, railTiles);                 // the tiles moved beside the grid
            Assert.True(bandAfter.Height <= 1f,         // and the band under it gave its rows back
                        $"The totals band is still {bandAfter.Height:F0} tall.");
        }

        /// <summary>
        /// A laptop at the smallest window the app allows used to show the list's header and
        /// nothing under it: the four cards around it asked for every pixel. The cards can now
        /// scroll out of the way, but the measurements always keep room to work in.
        /// </summary>
        [Fact]
        public void TheShortWindow_StillShowsSeveralSizes()
        {
            Exception? failure = null;
            int visibleRows = 0;
            bool chargesReachableByScrolling = false;

            var thread = new Thread(() =>
            {
                try
                {
                    using var form = new CarpenterMeasurementCalculatorForm();
                    form.WindowState = FormWindowState.Normal;
                    form.Size = new Size(1280, 780);       // the MinimumSize in the code
                    form.Show();
                    for (int i = 0; i < 40; i++) Application.DoEvents();

                    var list = (List<GreenWoodWorkAging.Models.MeasurementEntry>)
                        Form.GetField("_measurements", Private)!.GetValue(form)!;
                    for (int i = 1; i <= 12; i++)
                        list.Add(new GreenWoodWorkAging.Models.MeasurementEntry
                        {
                            SerialNumber = i, HeightInches = 30 + i, WidthInches = 20, Quantity = 1
                        });
                    Form.GetMethod("RefreshGrid", Private)!.Invoke(form, null);
                    for (int i = 0; i < 40; i++) Application.DoEvents();

                    var grid = AllControls(form).OfType<DataGridView>().Single();
                    visibleRows = grid.DisplayedRowCount(false);

                    var body = (Panel)Form.GetField("_bodyPanel", Private)!.GetValue(form)!;
                    chargesReachableByScrolling =
                        // A scroll bar whose range covers the whole stack of cards, and a list that
                        // keeps its own room at the top of it.
                        body.AutoScroll &&
                        body.AutoScrollMinSize.Height > body.ClientSize.Height &&
                        grid.Height >= 200;
                    form.Close();
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(90)), "The layout never settled.");

            Assert.Null(failure);
            Assert.True(visibleRows >= 5,
                        $"a full bill showed only {visibleRows} of its 12 sizes at the smallest window");
            Assert.True(chargesReachableByScrolling,
                        "scrolling the short window never brings the bill charges card into view");
        }

        private static IEnumerable<Control> AllControls(Control root)
        {
            foreach (Control c in root.Controls)
            {
                yield return c;
                foreach (var deep in AllControls(c)) yield return deep;
            }
        }
    }
}
