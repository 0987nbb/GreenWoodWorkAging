namespace GreenWoodWorkAging.Models
{
    /// <summary>
    /// The shop's length unit, in one place.
    ///
    /// A sooter is an eighth of an inch and a sady is half a sooter, so every length in the
    /// program is held as a whole number of sixteenths of an inch: 1 inch = 8 sooter = 16 units.
    /// Sizes written before sady existed are all even units, so they read, bill and cut exactly
    /// as they always did — the extra precision only ever adds, it never re-states an old number.
    /// </summary>
    public static class Sooter
    {
        /// <summary>Units in one inch.</summary>
        public const int PerInch = 16;

        /// <summary>Units in one sooter — a sady is one of these.</summary>
        public const int PerSooter = 2;

        /// <summary>Units in one foot, so a bill's money maths needs no other constant.</summary>
        public const int PerFoot = PerInch * 12;

        /// <summary>
        /// The whole inches, the sooter count (0–7) and the sady (0 or 1) a length is shown as.
        /// Anything that reaches a full inch is carried into it, so 8 sooter reads as 1 inch.
        /// </summary>
        public static (int inches, int sooter, int half) Split(int units)
        {
            int inches = units / PerInch;
            int rest   = units % PerInch;
            return (inches, rest / PerSooter, rest % PerSooter);
        }

        /// <summary>The length a typed inch + sooter + sady pair stands for.</summary>
        public static int Combine(int inches, int sooter, int half = 0)
            => inches * PerInch + sooter * PerSooter + half;
    }
}
