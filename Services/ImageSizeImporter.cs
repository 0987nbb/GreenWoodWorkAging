using GreenWoodWorkAging.Models;
using Tesseract;

namespace GreenWoodWorkAging.Services
{
    /// <summary>
    /// Reads sizes out of pictures — a photo of a handwritten list, a screenshot of a message,
    /// a scan of a note. Tesseract turns the image into text and <see cref="SizeListParser"/>
    /// turns that text into rows. Nothing here touches the calculator or the bill logic.
    /// </summary>
    public class ImageSizeImporter : IDisposable
    {
        private static readonly string[] SupportedExtensions =
            { ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff" };

        private TesseractEngine? _engine;
        private readonly string _tessDataDirectory;

        public ImageSizeImporter(string? tessDataDirectory = null)
        {
            _tessDataDirectory = tessDataDirectory ?? Path.Combine(AppContext.BaseDirectory, "tessdata");
        }

        /// <summary>The file picker filter for the pictures this reader can handle.</summary>
        public static string OpenFileDialogFilter =>
            $"Images ({string.Join(", ", SupportedExtensions)})|*.{string.Join(";*.", SupportedExtensions)}|All files (*.*)|*.*";

        public static bool IsSupportedImage(string filePath) =>
            SupportedExtensions.Contains(Path.GetExtension(filePath).ToLowerInvariant());

        // ── Reading ──────────────────────────────────────────────────────────

        /// <summary>Reads any number of images, in the order they were picked.</summary>
        public List<ImageReadResult> Import(IEnumerable<string> filePaths)
        {
            var results = new List<ImageReadResult>();
            foreach (var path in filePaths)
                results.Add(ImportOne(path));
            return results;
        }

        public ImageReadResult ImportOne(string filePath)
        {
            var result = new ImageReadResult
            {
                FileName = Path.GetFileName(filePath),
                FilePath = filePath
            };

            string text;
            try
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("The file could not be found.", filePath);
                if (!IsSupportedImage(filePath))
                    throw new NotSupportedException("Unsupported picture format.");

                text = ReadText(filePath);
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                return result;
            }

            var parsed = SizeListParser.Parse(text);
            result.RawText = text;
            result.Sizes.AddRange(parsed.Sizes);
            result.UnreadLines.AddRange(parsed.UnreadLines);
            return result;
        }

        /// <summary>
        /// Reads the cleaned photo, which is what a phone camera gives us; if the cleaner cannot
        /// handle the file, the picture is still worth a plain read.
        /// </summary>
        private string ReadText(string filePath)
        {
            string? cleaned = TryClean(filePath);
            try
            {
                return Recognise(cleaned ?? filePath);
            }
            finally
            {
                // A leftover temp picture is not worth failing an import over.
                if (cleaned != null)
                    try { File.Delete(cleaned); } catch (Exception) { }
            }
        }

        private string Recognise(string imagePath)
        {
            using var pix = Pix.LoadFromFile(imagePath);
            using var page = Engine.Process(pix, "sizes", PageSegMode.Auto);
            return page.GetText() ?? "";
        }

        private static string? TryClean(string filePath)
        {
            try
            {
                using var prepared = ImagePreprocessor.Prepare(filePath);
                string directory = Path.Combine(Path.GetTempPath(), "GreenWoodWorkAging");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, $"{Guid.NewGuid():N}.png");
                prepared.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                return path;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private TesseractEngine Engine
        {
            get
            {
                if (_engine == null)
                {
                    string trainedData = Path.Combine(_tessDataDirectory, "eng.traineddata");
                    if (!File.Exists(trainedData))
                        throw new InvalidOperationException(
                            $"The text reader's English language file is missing: {trainedData}");

                    _engine = new TesseractEngine(_tessDataDirectory, "eng", EngineMode.Default);
                    _engine.SetVariable("preserve_interword_spaces", "1");
                }
                return _engine;
            }
        }

        public void Dispose()
        {
            _engine?.Dispose();
            _engine = null;
        }
    }
}
