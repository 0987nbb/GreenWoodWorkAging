using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using GreenWoodWorkAging.Services;
using Xunit;

namespace GreenWoodWorkAging
{
    /// <summary>
    /// The question asked between the bill and the plan: which sheets are on the floor, how big
    /// are they, how many. What the shop leaves blank must not change the plan it always had.
    /// </summary>
    [Collection("SheetStock")]
    public class SheetStockPickFormTests
    {
        private static readonly Type Form = typeof(SheetStockPickForm);

        private static readonly string Scratch =
            Path.Combine(Path.GetTempPath(), "gwa-stock-question", DateTime.Now.ToString("HHmmss"));

        private static DataGridView GridOf(SheetStockPickForm form) =>
            (DataGridView)Form.GetField("_grid", BindingFlags.NonPublic | BindingFlags.Instance)!
                              .GetValue(form)!;

        private static void Accept(SheetStockPickForm form, Label note) =>
            Form.GetMethod("TryAccept", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(form, new object[] { note });

        /// <summary>Types one line of the answer the way the shop would, then reads it back.</summary>
        private static (List<SheetCuttingOptimizer.StockSheet> Stock, string Note, DialogResult Result)
            Answered(string[][] lines)
        {
            var stock = new List<SheetCuttingOptimizer.StockSheet>();
            string noteText = "";
            DialogResult result = DialogResult.None;
            Exception? failure = null;

            var thread = new Thread(() =>
            {
                try
                {
                    using var form = new SheetStockPickForm(new[] { "KMI 123" });
                    var grid = GridOf(form);

                    // The shop's answer replaces the pre-filled line, row by row.
                    grid.Rows.Clear();
                    foreach (var line in lines) grid.Rows.Add(line);

                    var note = new Label();
                    Accept(form, note);

                    stock.AddRange(form.Stock);
                    noteText = note.Text;
                    result   = form.DialogResult;
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "The stock question never settled.");

            Assert.Null(failure);
            return (stock, noteText, result);
        }

        [Fact]
        public void TheQuestionOpensWithOneLinePerSheetOnTheBill()
        {
            Exception? failure = null;
            var seen = new List<string?[]>();

            var thread = new Thread(() =>
            {
                try
                {
                    using var form = new SheetStockPickForm(new[] { "KMI 123", "Super Glass", "kmi 123" });
                    form.Show();
                    for (int i = 0; i < 20; i++) Application.DoEvents();

                    foreach (DataGridViewRow row in GridOf(form).Rows)
                    {
                        if (row.IsNewRow) continue;
                        seen.Add(new[] { row.Cells[0].Value?.ToString(), row.Cells[1].Value?.ToString(),
                                         row.Cells[2].Value?.ToString(), row.Cells[3].Value?.ToString() });
                    }

                    Directory.CreateDirectory(Scratch);
                    using var bmp = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                    bmp.Save(Path.Combine(Scratch, "stock-question.png"), ImageFormat.Png);
                    form.Close();
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "The stock question never settled.");

            Assert.Null(failure);

            // The same sheet named twice is one line, already sized as plywood.
            Assert.Equal(2, seen.Count);
            Assert.Equal("KMI 123",      seen[0][0]);
            Assert.Equal("Super Glass",  seen[1][0]);
            Assert.Equal("48",           seen[0][1]);
            Assert.Equal("96",           seen[0][2]);
            Assert.Equal("",             seen[0][3]);   // no count yet — the shop counts it
        }

        [Fact]
        public void TheSizesAndTheCountEntered_AreWhatThePlanIsCutFrom()
        {
            var (stock, note, result) = Answered(new[]
            {
                new[] { "KMI 123", "48", "96", "5" },
                new[] { "PVC",     "48", "48", "" }
            });

            Assert.Equal(DialogResult.OK, result);
            Assert.Equal("", note);
            Assert.Equal(2, stock.Count);

            Assert.Equal("KMI 123", stock[0].Name);
            Assert.Equal(48, stock[0].WidthInches);
            Assert.Equal(96, stock[0].HeightInches);
            Assert.Equal(5,  stock[0].Quantity);

            Assert.Equal(48, stock[1].WidthInches);
            Assert.Equal(0,  stock[1].Quantity);   // a blank count is "not counted", not "none"
        }

        [Fact]
        public void ALineWithoutSizes_IsPointedAt_NotGuessed()
        {
            var (stock, note, result) = Answered(new[] { new[] { "KMI 123", "", "96", "4" } });

            Assert.Equal(DialogResult.None, result);
            Assert.Empty(stock);
            Assert.Equal("Width and length are needed on row 1.", note);
        }

        [Fact]
        public void NothingEntered_PlansTheWayTheBillAlwaysDid()
        {
            var (stock, note, result) = Answered(new[] { new[] { "", "", "", "" } });

            Assert.Equal(DialogResult.OK, result);
            Assert.Equal("", note);
            Assert.Empty(stock);
        }
}
}
