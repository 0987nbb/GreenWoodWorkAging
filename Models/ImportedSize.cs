namespace GreenWoodWorkAging.Models
{
    /// <summary>
    /// One size that was read out of an image, before it becomes a grid row.
    /// </summary>
    public class ImportedSize
    {
        public int HeightInches { get; set; }
        public int HeightSooter { get; set; }
        public int WidthInches  { get; set; }
        public int WidthSooter  { get; set; }
        public int Quantity     { get; set; } = 1;

        /// <summary>The line the numbers were taken from, kept so the reading can be checked.</summary>
        public string SourceLine { get; set; } = "";

        public int HeightEighths => HeightInches * 8 + HeightSooter;
        public int WidthEighths  => WidthInches  * 8 + WidthSooter;
    }

    /// <summary>What one image gave up: the sizes, the lines that made no sense, any error.</summary>
    public class ImageReadResult
    {
        public string FileName { get; set; } = "";

        /// <summary>Where the picture is on disk, so the review window can show it again.</summary>
        public string FilePath { get; set; } = "";

        public string RawText { get; set; } = "";
        public List<ImportedSize> Sizes { get; } = new();

        /// <summary>Lines that held numbers but not a usable height × width pair.</summary>
        public List<string> UnreadLines { get; } = new();

        /// <summary>Set when the image could not be read at all.</summary>
        public string? Error { get; set; }
    }
}
