namespace GreenWoodWorkAging.Services
{
    /// <summary>
    /// Validates all user inputs for the carpenter measurement calculator.
    /// Returns structured validation results — never throws exceptions for bad user input.
    /// </summary>
    public class MeasurementInputValidator
    {
        // ── Constants ───────────────────────────────────────────────────────

        public const int MinInches = 0;
        public const int MaxInches = 9999;
        public const int MinSooter = 0;
        public const int MaxSooter = 7;
        public const int MinQuantity = 1;
        public const int MaxQuantity = 9999;

        // ── Validation result ────────────────────────────────────────────────

        public record ValidationResult(bool IsValid, string ErrorMessage = "");

        // ── Public validators ────────────────────────────────────────────────

        /// <summary>
        /// Validates an inches text field (e.g., height or width inches part).
        /// </summary>
        public ValidationResult ValidateInches(string text, string fieldName)
        {
            if (!int.TryParse(text?.Trim(), out int value))
                return Fail($"{fieldName} inches must be a whole number.");

            if (value < MinInches || value > MaxInches)
                return Fail($"{fieldName} inches must be between {MinInches} and {MaxInches}.");

            return Ok();
        }

        /// <summary>
        /// Validates a sooter text field. Valid values: 0 to 7.
        /// </summary>
        public ValidationResult ValidateSooter(string text, string fieldName)
        {
            if (!int.TryParse(text?.Trim(), out int value))
                return Fail($"{fieldName} sooter must be a whole number (0–7).");

            if (value < MinSooter || value > MaxSooter)
                return Fail($"{fieldName} sooter must be between {MinSooter} and {MaxSooter} (1 inch = 8 sooter).");

            return Ok();
        }

        /// <summary>
        /// Validates a quantity field. Must be a positive integer.
        /// </summary>
        public ValidationResult ValidateQuantity(string text)
        {
            if (!int.TryParse(text?.Trim(), out int value))
                return Fail("Quantity must be a positive whole number.");

            if (value < MinQuantity || value > MaxQuantity)
                return Fail($"Quantity must be between {MinQuantity} and {MaxQuantity}.");

            return Ok();
        }

        /// <summary>
        /// Validates all four measurement fields together and returns the first error found.
        /// Returns null when all inputs are valid.
        /// </summary>
        public ValidationResult ValidateAll(
            string heightInchesText, string heightSooterText,
            string widthInchesText, string widthSooterText,
            string quantityText)
        {
            var checks = new[]
            {
                ValidateInches(heightInchesText, "Height"),
                ValidateSooter(heightSooterText, "Height"),
                ValidateInches(widthInchesText, "Width"),
                ValidateSooter(widthSooterText, "Width"),
                ValidateQuantity(quantityText)
            };

            foreach (var result in checks)
                if (!result.IsValid)
                    return result;

            // At least one dimension must be > 0
            int.TryParse(heightInchesText, out int hi);
            int.TryParse(heightSooterText, out int hs);
            int.TryParse(widthInchesText, out int wi);
            int.TryParse(widthSooterText, out int ws);

            if (hi == 0 && hs == 0 && wi == 0 && ws == 0)
                return Fail("At least one dimension (height or width) must be greater than zero.");

            return Ok();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static ValidationResult Ok() => new(true);
        private static ValidationResult Fail(string message) => new(false, message);
    }
}
