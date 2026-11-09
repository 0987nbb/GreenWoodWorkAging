namespace GreenWoodWorkAging.Services
{
    /// <summary>
    /// Validates all user inputs for the carpenter measurement calculator.
    /// Supports the combined "inches.sooter" format — e.g. "79.7" means 79 inches + 7 sooter —
    /// and the sady (half-sooter) codes a customer's tape gives, where "8.19" means
    /// 8 inches + 1½ sooter. Returns structured validation results — never throws
    /// exceptions for bad user input.
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
        /// Parsed result from a combined "inches.sooter" field. <see cref="Half"/> is the sady —
        /// half a sooter — so 8.19 reads back as 8 inches, 1 sooter and a half.
        /// </summary>
        public record ParsedMeasurement(int Inches, int Sooter, int Half = 0);

        // ── The one reading rule ─────────────────────────────────────────────

        /// <summary>
        /// What a typed measurement actually means, in sixteenths of an inch. This is the only
        /// place that decision is made, so the Add box, the image reader and the size-list parser
        /// cannot drift apart on a number the shop is billed for.
        ///
        ///   "80"     → 80 inches
        ///   "8.4"    → 8 in 4 sooter      (one digit is the sooter count, as it always was)
        ///   "8.8"    → 9 in               (8 sooter is a full inch, so it carries)
        ///   "8.12"   → 8 in 1 sooter      (two digits are the decimal inch: .12 × 16 ≈ 2)
        ///   "8.19"   → 8 in 1½ sooter     (.19 × 16 ≈ 3 sixteenths — a sady)
        /// </summary>
        public static bool TryReadSixteenths(string? text, out int sixteenths)
        {
            sixteenths = 0;
            string trimmed = (text ?? "").Trim();
            if (trimmed.Length == 0) return false;

            int dot = trimmed.IndexOf('.');
            if (dot < 0)
            {
                if (!int.TryParse(trimmed, out int inches) || inches < MinInches) return false;
                sixteenths = inches * Models.Sooter.PerInch;
                return true;
            }

            string inchesPart = trimmed[..dot];
            string digits     = trimmed[(dot + 1)..];

            if (!int.TryParse(inchesPart, out int whole) || whole < MinInches) return false;
            if (digits.Length == 0)
            {
                sixteenths = whole * Models.Sooter.PerInch;
                return true;
            }

            if (!int.TryParse(digits, out int fraction)) return false;
            if (digits.Length > 4) return false;   // more precision than a tape holds is a mis-read

            // One digit is the shop's sooter count; two or more are the decimal inch it was
            // read off a tape as, which lands on the nearest half-sooter.
            int rest = digits.Length == 1
                ? fraction * Models.Sooter.PerSooter
                : (int)Math.Round(fraction * (double)Models.Sooter.PerInch / PowerOfTen(digits.Length));

            sixteenths = whole * Models.Sooter.PerInch + rest;
            return true;
        }

        private static int PowerOfTen(int digits)
        {
            int result = 1;
            for (int i = 0; i < digits; i++) result *= 10;
            return result;
        }

        // ── Combined "inches.sooter" parser & validator ──────────────────────

        /// <summary>
        /// Parses and validates a single measurement field in "inches.sooter" format.
        /// Examples: "79.7" → 79in 7sooter, "80" or "80.0" → 80in 0sooter, "8.19" → 8in 1½sooter.
        /// Eight sooter carry into the inch, so the largest fraction is 7½.
        /// </summary>
        public (ValidationResult result, ParsedMeasurement? parsed)
            ParseAndValidateMeasurement(string text, string fieldName)
        {
            var trimmed = text?.Trim() ?? "";

            if (string.IsNullOrEmpty(trimmed))
                return (Fail($"{fieldName} cannot be empty. Enter as inches.sooter, e.g. 79.7"), null);

            if (!TryReadSixteenths(trimmed, out int sixteenths))
                return (Fail($"{fieldName} is not valid. Use format inches.sooter, e.g. 79.7"), null);

            var (inches, sooter, half) = Models.Sooter.Split(sixteenths);

            if (inches < MinInches || inches > MaxInches)
                return (Fail($"{fieldName} inches must be between {MinInches} and {MaxInches}."), null);

            if (sooter < MinSooter || sooter > MaxSooter)
                return (Fail($"{fieldName} sooter must be 0–7 (1 inch = 8 sooter). Got: {sooter}"), null);

            return (Ok(), new ParsedMeasurement(inches, sooter, half));
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
            if (hParsed!.Inches == 0 && hParsed.Sooter == 0 && hParsed.Half == 0 &&
                wParsed!.Inches == 0 && wParsed.Sooter == 0 && wParsed.Half == 0)
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
