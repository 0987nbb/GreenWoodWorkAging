using System.Drawing;
using System.Drawing.Imaging;
using GreenWoodWorkAging.Models;
using Xunit;

namespace GreenWoodWorkAging.Tests
{
    /// <summary>
    /// Draws the import review window to a file so its layout can be checked by eye without
    /// starting the app. It is only a picture of the form — no behaviour is asserted here.
    /// </summary>
    public class ImageImportFormRenderTests
    {
        [Fact]
        public void TheReviewWindow_CanBeDrawn()
        {
            string directory = Path.Combine(Path.GetTempPath(), "gwa-import");
            Directory.CreateDirectory(directory);

            // The window is judged by eye, so give it the same kind of photo the shop would pick.
            string photo = ImageSizeImporterTests.WritePhoto(directory, "window-photo.jpg",
                new[] { "89 x 24 x 2", "80 x 18", "60 x 30 x 3" }, tiltDegrees: 2f);

            var results = new List<ImageReadResult>
            {
                new()
                {
                    FileName = "WhatsApp Image 2026-10-30.jpg",
                    FilePath = photo,
                    Sizes =
                    {
                        new ImportedSize { HeightInches = 89, WidthInches = 24, Quantity = 2, SourceLine = "89 x 24 x 2" },
                        new ImportedSize { HeightInches = 79, HeightSooter = 7, WidthInches = 24, WidthSooter = 3, SourceLine = "79.7 x 24.3" }
                    }
                }
            };

            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using var form = new ImageImportForm(results);
                    form.Size = new Size(1120, 660);

                    // A form that has never been shown paints nothing but its own background.
                    form.Show();
                    for (int i = 0; i < 20; i++) Application.DoEvents();

                    using var bmp = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                    bmp.Save(Path.Combine(directory, "import-window.png"), ImageFormat.Png);

                    form.Close();
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "Drawing the window never finished.");

            Assert.Null(failure);
            Assert.True(File.Exists(Path.Combine(directory, "import-window.png")));
        }
    }
}
