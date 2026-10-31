using System.Text.RegularExpressions;
using GreenWoodWorkAging.Models;

namespace GreenWoodWorkAging.Services
{
    /// <summary>
    /// Turns the plain text read off a written or printed size list into measurement rows.
    /// Everything works in eighth-inch (sooter) units so a reading like "79.7" means exactly
    /// what the manual entry box means: 79 inches + 7 sooter.
    ///
    /// Accepted per line/segment, in the shop's usual order:
    ///   89 x 24            → height, width, qty 1
    ///   89 x 24 x 2        → height, width, qty 2
    ///   240 2/8 x 24       → 240 in 2 sooter, 24
    ///   1. 89 x 24 x 2     → the leading list index is dropped
    /// </summary>
    public static class SizeListParser
    {
        /// <summary>Smallest size worth importing, in inches — anything less is stray OCR noise.</summary>
        public const int MinDimensionInches = 1;

        /// <summary>Largest size worth importing, in inches — beyond this a reading is a misread.</summary>
        public const int MaxDimensionInches = 250;

        public const int MinQuantity = 1;
        public const int MaxQuantity = 999;

        private static readonly Regex NumberToken =
            new(@"\d+(?:[.,]\d+)?(?:/\d+)?", RegexOptions.Compiled);

        /// <summary>A leading "1. ", "2) " or "12] " is a list index, not a measurement.</summary>
        private static readonly Regex ListIndex =
            new(@"^\s*\d{1,3}[.)\]]\s+", RegexOptions.Compiled);

        public record ParseResult(IReadOnlyList<ImportedSize> Sizes, IReadOnlyList<string> UnreadLines);

        // ── Public entry point ───────────────────────────────────────────────

        public static ParseResult Parse(string? text)
        {
            var sizes  = new List<ImportedSize>();
            var unread = new List<string>();

            foreach (var rawLine in (text ?? "").Split('\n'))
            {
                foreach (var segment in SplitSegments(rawLine))
                {
                    ImportedSize? parsed = ParseSegment(segment);
                    if (parsed != null)
                        sizes.Add(parsed);
                    else if (NumberToken.IsMatch(segment))
                        unread.Add(segment);
                }
            }

            return new ParseResult(sizes, unread);
        }

        // ── Line handling ────────────────────────────────────────────────────

        /// <summary>
        /// One OCR line can hold several sizes ("89x24; 80x18"). Splits on the separators a
        /// shop uses, leaving ":" and "x" alone so labelled lines keep their numbers together.
        /// </summary>
        private static IEnumerable<string> SplitSegments(string line)
        {
            line = line.TrimEnd('\r', ' ', '\t');
            if (string.IsNullOrWhiteSpace(line))
                return Array.Empty<string>();

            return line
                .Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0);
        }

        private static ImportedSize? ParseSegment(string segment)
        {
            string body = ListIndex.Replace(segment, "", 1);

            var values = ReadValues(body);
            if (values.Count == 0) return null;

            int height, width, quantity;

            if (values.Count == 2)
            {
                height = values[0].Eighths;
                width  = values[1].Eighths;
                quantity = 1;
            }
            else if (values.Count == 3)
            {
                height = values[0].Eighths;
                width  = values[1].Eighths;
                if (!values[2].IsPlainWhole) return null;
                quantity = values[2].Eighths / 8;
                if (quantity < MinQuantity || quantity > MaxQuantity) return null;
            }
            else
            {
                // A single number, or four-plus numbers where the line cannot be trusted
                // to say height × width × qty. Reported to the user instead of guessed.
                return null;
            }

            if (!IsUsableDimension(height) || !IsUsableDimension(width)) return null;

            return new ImportedSize
            {
                HeightInches = height / 8, HeightSooter = height % 8,
                WidthInches  = width  / 8, WidthSooter  = width  % 8,
                Quantity     = quantity,
                SourceLine   = segment
            };
        }

        private static bool IsUsableDimension(int eighths)
            => eighths >= MinDimensionInches * 8 && eighths <= MaxDimensionInches * 8;

        // ── Number reading ───────────────────────────────────────────────────

        private struct Value
        {
            /// <summary>Length in eighth-inch (sooter) units.</summary>
            public int Eighths;

            /// <summary>True for a bare "2" — only such a token can be read as the quantity.</summary>
            public bool IsPlainWhole;

            /// <summary>True for "2/8" — such a token belongs to the whole number in front of it.</summary>
            public bool IsFractionOnly;
        }

        /// <summary>
        /// Reads every number on the line and folds a bare fraction into the whole number in
        /// front of it, so "240 2/8" becomes one measurement instead of two.
        /// </summary>
        private static List<Value> ReadValues(string text)
        {
            var values = new List<Value>();

            foreach (Match match in NumberToken.Matches(text))
            {
                Value? value = ToValue(match.Value);
                if (value == null) continue;

                if (value.Value.IsFractionOnly && values.Count > 0)
                {
                    int previous = values.Count - 1;
                    Value merged = values[previous];
                    merged.Eighths      += value.Value.Eighths;
                    merged.IsPlainWhole  = false;
                    values[previous] = merged;
                    continue;
                }

                values.Add(value.Value);
            }

            return values;
        }

        private static Value? ToValue(string token)
        {
            int slash = token.IndexOf('/');
            if (slash >= 0)
            {
                if (!int.TryParse(token[..slash], out int numerator) ||
                    !int.TryParse(token[(slash + 1)..], out int denominator))
                    return null;
                if (denominator == 0) return null;

                // Only the eighth-based fractions a tape or a written list actually uses.
                if (denominator != 2 && denominator != 4 && denominator != 8 &&
                    denominator != 16 && denominator != 32)
                    return null;

                return new Value
                {
                    Eighths = (int)Math.Round(numerator * 8.0 / denominator),
                    IsFractionOnly = true
                };
            }

            int dot = token.IndexOfAny(new[] { '.', ',' });
            if (dot < 0)
            {
                if (!int.TryParse(token, out int inches)) return null;
                return new Value { Eighths = inches * 8, IsPlainWhole = true };
            }

            string wholePart = token[..dot];
            string afterDot  = token[(dot + 1)..];

            if (!int.TryParse(wholePart, out int whole)) return null;
            if (!int.TryParse(afterDot, out int fractionDigits)) return null;

            // One digit after the point is the app's sooter convention: "79.7" = 79 in 7 sooter.
            if (afterDot.Length == 1 && fractionDigits <= 7)
                return new Value { Eighths = whole * 8 + fractionDigits };

            // Anything else is written in decimal inches: "79.75" → 79 in 6 sooter.
            decimal asInches = whole + (decimal)fractionDigits / PowerOfTen(afterDot.Length);
            return new Value { Eighths = (int)Math.Round(asInches * 8m) };
        }

        private static int PowerOfTen(int digits)
        {
            int result = 1;
            for (int i = 0; i < digits; i++) result *= 10;
            return result;
        }
    }
}
