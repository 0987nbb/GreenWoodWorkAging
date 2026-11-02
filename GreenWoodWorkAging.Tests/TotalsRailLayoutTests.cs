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
