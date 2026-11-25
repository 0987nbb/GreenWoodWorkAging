using GreenWoodWorkAging.Models;

namespace GreenWoodWorkAging.Services
{
    /// <summary>
    /// Validates piece positioning, blade kerf separation, boundary constraints,
    /// coordinates snapping, and recomputes scrap rectangles for sheet cutting layouts.
    /// </summary>
    public static class SheetValidationService
    {
        public record ValidationResult(bool IsValid, string Reason);

        /// <summary>
        /// Validates whether a candidate placement fits on the target sheet without exceeding
        /// boundaries and without overlapping or violating the blade thickness separation with any other piece.
        /// </summary>
        public static ValidationResult ValidatePlacement(SheetLayout sheet, CutPlacement candidate, string? movingPieceId, int bladeThickness)
        {
            if (candidate.WidthSixteenths <= 0 || candidate.HeightSixteenths <= 0)
                return new ValidationResult(false, "Invalid piece dimensions.");

            if (candidate.X < 0)
                return new ValidationResult(false, "Piece extends past the left edge of the sheet.");

            if (candidate.Y < 0)
                return new ValidationResult(false, "Piece extends past the top edge of the sheet.");

            if (candidate.Right > sheet.SheetWidthSixteenths)
                return new ValidationResult(false, $"Piece extends past the right edge (Right: {candidate.Right}, Max: {sheet.SheetWidthSixteenths}).");

            if (candidate.Bottom > sheet.SheetHeightSixteenths)
                return new ValidationResult(false, $"Piece extends past the bottom edge (Bottom: {candidate.Bottom}, Max: {sheet.SheetHeightSixteenths}).");

            double suter = bladeThickness / (double)Sooter.PerSooter;

            foreach (var other in sheet.Placements)
            {
                if (!string.IsNullOrEmpty(movingPieceId) && other.Id == movingPieceId)
                    continue;

                // Two pieces are separated if they don't overlap in either X or Y by at least bladeThickness.
                bool separatedX = candidate.X >= other.Right + bladeThickness || other.X >= candidate.Right + bladeThickness;
                bool separatedY = candidate.Y >= other.Bottom + bladeThickness || other.Y >= candidate.Bottom + bladeThickness;

                if (!separatedX && !separatedY)
                {
                    return new ValidationResult(false,
                        $"Overlaps piece #{other.SerialNumber} or violates blade thickness ({suter:0.##} Suter).");
                }
            }

            return new ValidationResult(true, "");
        }

        /// <summary>
        /// Checks if a candidate rectangle has any overlap or blade kerf conflict with pieces on the sheet.
        /// </summary>
        public static bool HasConflict(SheetLayout sheet, int x, int y, int w, int h, int bladeThickness, string? ignorePieceId = null)
        {
            int right = x + w;
            int bottom = y + h;

            if (x < 0 || y < 0 || right > sheet.SheetWidthSixteenths || bottom > sheet.SheetHeightSixteenths)
                return true;

            foreach (var other in sheet.Placements)
            {
                if (!string.IsNullOrEmpty(ignorePieceId) && other.Id == ignorePieceId)
                    continue;

                bool separatedX = x >= other.Right + bladeThickness || other.X >= right + bladeThickness;
                bool separatedY = y >= other.Bottom + bladeThickness || other.Y >= bottom + bladeThickness;

                if (!separatedX && !separatedY)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Snaps candidate coordinates to sheet edges and neighboring piece edges within the given snap tolerance.
        /// </summary>
        public static (int X, int Y) SnapPosition(SheetLayout sheet, int rawX, int rawY, int pieceW, int pieceH,
                                                 string? movingPieceId, int bladeThickness, int snapTolerance = 16)
        {
            int snappedX = rawX;
            int snappedY = rawY;

            // X candidate snap points
            var xTargets = new List<int>
            {
                0,
                sheet.SheetWidthSixteenths - pieceW
            };

            // Y candidate snap points
            var yTargets = new List<int>
            {
                0,
                sheet.SheetHeightSixteenths - pieceH
            };

            foreach (var other in sheet.Placements)
            {
                if (!string.IsNullOrEmpty(movingPieceId) && other.Id == movingPieceId)
                    continue;

                xTargets.Add(other.Right + bladeThickness);
                xTargets.Add(other.X - pieceW - bladeThickness);
                xTargets.Add(other.X);

                yTargets.Add(other.Bottom + bladeThickness);
                yTargets.Add(other.Y - pieceH - bladeThickness);
                yTargets.Add(other.Y);
            }

            int bestXDist = snapTolerance + 1;
            foreach (int tx in xTargets)
            {
                int dist = Math.Abs(rawX - tx);
                if (dist < bestXDist)
                {
                    bestXDist = dist;
                    snappedX = tx;
                }
            }

            int bestYDist = snapTolerance + 1;
            foreach (int ty in yTargets)
            {
                int dist = Math.Abs(rawY - ty);
                if (dist < bestYDist)
                {
                    bestYDist = dist;
                    snappedY = ty;
                }
            }

            return (snappedX, snappedY);
        }

        /// <summary>
        /// Finds the first available spot on the sheet that can accommodate the piece with the given blade thickness.
        /// </summary>
        public static (int X, int Y)? FindAvailableSpot(SheetLayout sheet, int pieceW, int pieceH, int bladeThickness)
        {
            var candidates = new List<(int X, int Y)> { (0, 0) };

            foreach (var p in sheet.Placements)
            {
                candidates.Add((p.Right + bladeThickness, p.Y));
                candidates.Add((p.X, p.Bottom + bladeThickness));
                candidates.Add((p.Right + bladeThickness, 0));
                candidates.Add((0, p.Bottom + bladeThickness));

                foreach (var other in sheet.Placements)
                {
                    candidates.Add((p.Right + bladeThickness, other.Bottom + bladeThickness));
                    candidates.Add((other.Right + bladeThickness, p.Bottom + bladeThickness));
                }
            }

            var sorted = candidates
                .Where(c => c.X >= 0 && c.Y >= 0 && c.X + pieceW <= sheet.SheetWidthSixteenths && c.Y + pieceH <= sheet.SheetHeightSixteenths)
                .Distinct()
                .OrderBy(c => c.Y)
                .ThenBy(c => c.X);

            foreach (var (cx, cy) in sorted)
            {
                if (!HasConflict(sheet, cx, cy, pieceW, pieceH, bladeThickness))
                    return (cx, cy);
            }

            return null;
        }

        /// <summary>
        /// Recomputes the scrap rectangles for a sheet given its placements and blade kerf.
        /// Keeps large continuous rectangles by merging flush adjacent scrap.
        /// </summary>
        public static void RecomputeScrap(SheetLayout sheet, int bladeThickness)
        {
            sheet.Scrap.Clear();

            if (sheet.SheetWidthSixteenths <= 0 || sheet.SheetHeightSixteenths <= 0)
                return;

            if (sheet.Placements.Count == 0)
            {
                sheet.Scrap.Add(new ScrapRect
                {
                    X = 0,
                    Y = 0,
                    WidthSixteenths = sheet.SheetWidthSixteenths,
                    HeightSixteenths = sheet.SheetHeightSixteenths
                });
                return;
            }

            var freeRects = new List<ScrapRect>
            {
                new ScrapRect
                {
                    X = 0,
                    Y = 0,
                    WidthSixteenths = sheet.SheetWidthSixteenths,
                    HeightSixteenths = sheet.SheetHeightSixteenths
                }
            };

            foreach (var p in sheet.Placements)
            {
                // Account for blade cut on pieces that don't touch the outer sheet boundaries
                int cutW = p.WidthSixteenths + (p.Right < sheet.SheetWidthSixteenths ? bladeThickness : 0);
                int cutH = p.HeightSixteenths + (p.Bottom < sheet.SheetHeightSixteenths ? bladeThickness : 0);

                var next = new List<ScrapRect>();
                foreach (var f in freeRects)
                {
                    // Check intersection with placed piece area
                    int fRight = f.X + f.WidthSixteenths;
                    int fBottom = f.Y + f.HeightSixteenths;
                    int pRight = p.X + cutW;
                    int pBottom = p.Y + cutH;

                    bool noOverlap = fRight <= p.X || pRight <= f.X || fBottom <= p.Y || pBottom <= f.Y;
                    if (noOverlap)
                    {
                        next.Add(f);
                        continue;
                    }

                    // Split around the piece
                    // Top strip
                    if (p.Y > f.Y && p.Y < fBottom)
                    {
                        next.Add(new ScrapRect { X = f.X, Y = f.Y, WidthSixteenths = f.WidthSixteenths, HeightSixteenths = p.Y - f.Y });
                    }
                    // Bottom strip
                    if (pBottom < fBottom && pBottom > f.Y)
                    {
                        next.Add(new ScrapRect { X = f.X, Y = pBottom, WidthSixteenths = f.WidthSixteenths, HeightSixteenths = fBottom - pBottom });
                    }
                    // Left strip
                    int midY = Math.Max(f.Y, p.Y);
                    int midBottom = Math.Min(fBottom, pBottom);
                    int midH = midBottom - midY;
                    if (midH > 0)
                    {
                        if (p.X > f.X && p.X < fRight)
                        {
                            next.Add(new ScrapRect { X = f.X, Y = midY, WidthSixteenths = p.X - f.X, HeightSixteenths = midH });
                        }
                        // Right strip
                        if (pRight < fRight && pRight > f.X)
                        {
                            next.Add(new ScrapRect { X = pRight, Y = midY, WidthSixteenths = fRight - pRight, HeightSixteenths = midH });
                        }
                    }
                }

                freeRects = next;
            }

            // Merge flush adjacent rectangles
            MergeFlushScrap(freeRects);

            sheet.Scrap.AddRange(freeRects.Where(r => r.Area > 0).OrderByDescending(r => r.Area));
        }

        private static void MergeFlushScrap(List<ScrapRect> rects)
        {
            bool merged;
            do
            {
                merged = false;
                for (int i = 0; i < rects.Count && !merged; i++)
                {
                    for (int j = i + 1; j < rects.Count; j++)
                    {
                        var a = rects[i];
                        var b = rects[j];

                        // Share horizontal edge and vertical span identical
                        if (a.Y == b.Y && a.HeightSixteenths == b.HeightSixteenths)
                        {
                            if (a.X + a.WidthSixteenths == b.X)
                            {
                                rects[i] = new ScrapRect { X = a.X, Y = a.Y, WidthSixteenths = a.WidthSixteenths + b.WidthSixteenths, HeightSixteenths = a.HeightSixteenths };
                                rects.RemoveAt(j);
                                merged = true;
                                break;
                            }
                            if (b.X + b.WidthSixteenths == a.X)
                            {
                                rects[i] = new ScrapRect { X = b.X, Y = b.Y, WidthSixteenths = a.WidthSixteenths + b.WidthSixteenths, HeightSixteenths = a.HeightSixteenths };
                                rects.RemoveAt(j);
                                merged = true;
                                break;
                            }
                        }

                        // Share vertical edge and horizontal span identical
                        if (a.X == b.X && a.WidthSixteenths == b.WidthSixteenths)
                        {
                            if (a.Y + a.HeightSixteenths == b.Y)
                            {
                                rects[i] = new ScrapRect { X = a.X, Y = a.Y, WidthSixteenths = a.WidthSixteenths, HeightSixteenths = a.HeightSixteenths + b.HeightSixteenths };
                                rects.RemoveAt(j);
                                merged = true;
                                break;
                            }
                            if (b.Y + b.HeightSixteenths == a.Y)
                            {
                                rects[i] = new ScrapRect { X = a.X, Y = b.Y, WidthSixteenths = a.WidthSixteenths, HeightSixteenths = a.HeightSixteenths + b.HeightSixteenths };
                                rects.RemoveAt(j);
                                merged = true;
                                break;
                            }
                        }
                    }
                }
            } while (merged);
        }
    }
}
