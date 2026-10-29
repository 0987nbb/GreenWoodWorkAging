namespace GreenWoodWorkAging.Services
{
    /// <summary>
    /// Validates all user inputs for the carpenter measurement calculator.
    /// Supports the combined "inches.sooter" format — e.g. "79.7" means 79 inches + 7 sooter.
    /// Returns structured validation results — never throws exceptions for bad user input.
    /// </summary>
    public class MeasurementInputValidator
    {
        // ── Constants ───────────────────────────────────────────────────────

        public const int MinInches  = 0;
        public const int MaxInches  = 9999;
        public const int MinSooter  = 0;
        public const int MaxSooter  = 7;
        public const int MinQuantity = 1;
        public const int MaxQuantity = 9999;

        // ── Validation result ────────────────────────────────────────────────

        public record ValidationResult(bool IsValid, string ErrorMessage = "");

        /// <summary>
        /// Parsed result from a combined "inches.sooter" field.
        /// </summary>
        public record ParsedMeasurement(int Inches, int Sooter);

        // ── Combined "inches.sooter" parser & validator ──────────────────────

        /// <summary>
        /// Parses and validates a single measurement field in "inches.sooter" format.
        /// Examples: "79.7" → 79in 7sooter, "80" or "80.0" → 80in 0sooter, "17.4" → 17in 4sooter.
        /// The sooter part (after the dot) must be 0–7.
        /// </summary>
        public (ValidationResult result, ParsedMeasurement? parsed)
            ParseAndValidateMeasurement(string text, string fieldName)
        {
            var trimmed = text?.Trim() ?? "";

            if (string.IsNullOrEmpty(trimmed))
                return (Fail($"{fieldName} cannot be empty. Enter as inches.sooter, e.g. 79.7"), null);

            int inches;
            int sooter;

            int dotIndex = trimmed.IndexOf('.');
            if (dotIndex < 0)
            {
                // No dot → pure inches, sooter = 0
                if (!int.TryParse(trimmed, out inches))
                    return (Fail($"{fieldName} is not valid. Use format inches.sooter, e.g. 79.7"), null);
                sooter = 0;
            }
            else
            {
                string inchesPart = trimmed[..dotIndex];
                string sooterPart = trimmed[(dotIndex + 1)..];

                if (!int.TryParse(inchesPart, out inches))
                    return (Fail($"{fieldName}: inches part \"{inchesPart}\" must be a whole number."), null);

                if (string.IsNullOrEmpty(sooterPart))
                    sooter = 0;
                else if (!int.TryParse(sooterPart, out sooter))
                    return (Fail($"{fieldName}: sooter part \"{sooterPart}\" must be a digit 0–7."), null);
            }

            if (inches < MinInches || inches > MaxInches)
                return (Fail($"{fieldName} inches must be between {MinInches} and {MaxInches}."), null);

            if (sooter < MinSooter || sooter > MaxSooter)
                return (Fail($"{fieldName} sooter must be 0–7 (1 inch = 8 sooter). Got: {sooter}"), null);

            return (Ok(), new ParsedMeasurement(inches, sooter));
        }

        // ── Quantity validator ───────────────────────────────────────────────

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

        // ── Combined validation ──────────────────────────────────────────────

        /// <summary>
        /// Validates all three fields (height, width, quantity) together.
        /// Returns the parsed measurements if valid.
        /// </summary>
        public (ValidationResult result, ParsedMeasurement? height, ParsedMeasurement? width)
            ValidateAll(string heightText, string widthText, string quantityText)
        {
            var (hResult, hParsed) = ParseAndValidateMeasurement(heightText, "Height");
            if (!hResult.IsValid) return (hResult, null, null);

            var (wResult, wParsed) = ParseAndValidateMeasurement(widthText, "Width");
            if (!wResult.IsValid) return (wResult, null, null);

            var qResult = ValidateQuantity(quantityText);
            if (!qResult.IsValid) return (qResult, null, null);

            // At least one dimension must be > 0
            if (hParsed!.Inches == 0 && hParsed.Sooter == 0 &&
                wParsed!.Inches == 0 && wParsed.Sooter == 0)
                return (Fail("At least one dimension (height or width) must be greater than zero."), null, null);

            return (Ok(), hParsed, wParsed);
        }

        // ── Legacy individual validators (kept for backward compat / tests) ──

        public ValidationResult ValidateInches(string text, string fieldName)
        {
            if (!int.TryParse(text?.Trim(), out int value))
                return Fail($"{fieldName} inches must be a whole number.");
            if (value < MinInches || value > MaxInches)
                return Fail($"{fieldName} inches must be between {MinInches} and {MaxInches}.");
            return Ok();
        }

        public ValidationResult ValidateSooter(string text, string fieldName)
        {
            if (!int.TryParse(text?.Trim(), out int value))
                return Fail($"{fieldName} sooter must be a whole number (0–7).");
            if (value < MinSooter || value > MaxSooter)
                return Fail($"{fieldName} sooter must be between {MinSooter} and {MaxSooter} (1 inch = 8 sooter).");
            return Ok();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static ValidationResult Ok()                    => new(true);
        private static ValidationResult Fail(string message)   => new(false, message);
    }
}
