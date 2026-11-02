using System.Drawing;
using System.Drawing.Imaging;
using GreenWoodWorkAging.Models;
using GreenWoodWorkAging.Services;
using Xunit;

namespace GreenWoodWorkAging.Tests
{
    /// <summary>
    /// Draws the sheet-stock window and the bill's input card to files so their layout can be
    /// checked by eye without starting the app. Pictures only — no behaviour is asserted here.
    /// </summary>
    [Collection("SheetStock")]
    public class SheetStockFormRenderTests
    {
        static SheetStockFormRenderTests()
        {
            // Before anything opens the database: these windows are drawn against a throwaway file.
            Environment.SetEnvironmentVariable("GREENWOOD_DATA_DIR", Scratch);
        }

        private static readonly string Scratch =
            Path.Combine(Path.GetTempPath(), "gwa-sheet", DateTime.Now.ToString("HHmmss"));

        [Fact]
        public void TheStockWindow_CanBeDrawn()
        {
            Directory.CreateDirectory(Scratch);

            var db = new DatabaseService();
            int billId = db.CreateBill("Ali Raza", "03001234567", DateTime.Today);

            (string material, int given, int used)[] lines =
            {
                ("KMI 123",     10, 7),
                ("Super Glass",  5, 5),
                ("PVC",          3, 1),
                ("Textile",      2, 0)
            };

            // One customer, one entry, four materials — the shape the window has to show.
            var today = new SheetStockVisit
            {
                BillId = billId, CustomerName = "Ali Raza", PhoneNumber = "03001234567", Date = DateTime.Today
            };
            foreach (var (material, given, used) in lines)
                today.Lines.Add(new SheetStockEntry { Material = material, Received = given, Used = used });
            db.SaveSheetStockVisit(today);

            var earlier = new SheetStockVisit
            {
                BillId = billId, CustomerName = "Imran Khan", PhoneNumber = "03217654321",
                Date = DateTime.Today.AddDays(-6)
            };
            earlier.Lines.Add(new SheetStockEntry { Material = "KMI 123", Received = 12, Used = 4 });
            earlier.Lines.Add(new SheetStockEntry { Material = "Super Glass", Received = 6, Used = 6 });
            db.SaveSheetStockVisit(earlier);

            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using var form = new SheetStockForm(db, "Ali Raza", "03001234567", billId);
                    form.Size = new Size(1240, 680);

                    // A form that has never been shown paints nothing but its own background.
                    form.Show();
                    for (int i = 0; i < 20; i++) Application.DoEvents();

                    using var bmp = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                    bmp.Save(Path.Combine(Scratch, "stock-window.png"), ImageFormat.Png);

                    form.Close();
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "Drawing the window never finished.");

            Assert.Null(failure);
            Assert.True(File.Exists(Path.Combine(Scratch, "stock-window.png")));
        }

        /// <summary>The main bill window, so the sheet line and the new grid column can be seen.</summary>
        [Fact]
        public void TheBillWindow_CanBeDrawn()
        {
            Directory.CreateDirectory(Scratch);

            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using var form = new CarpenterMeasurementCalculatorForm();
                    form.Size = new Size(1280, 860);

                    form.Show();
                    for (int i = 0; i < 20; i++) Application.DoEvents();

                    using var bmp = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                    bmp.Save(Path.Combine(Scratch, "bill-window.png"), ImageFormat.Png);

                    form.Close();
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(90)), "Drawing the bill window never finished.");

            Assert.Null(failure);
            Assert.True(File.Exists(Path.Combine(Scratch, "bill-window.png")));
        }
    }
}
