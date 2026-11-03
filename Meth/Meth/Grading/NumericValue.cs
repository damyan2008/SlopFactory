using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using Meth.Models;

namespace Meth.Grading
{
    /// <summary>
    /// An exact rational number parsed with the numeric grammar of
    /// docs/question-grading-contract.md §2.2. Always stored in lowest terms with a positive
    /// denominator, so two values are equal exactly when their fields are equal.
    /// Never uses floating point.
    /// </summary>
    public readonly record struct NumericValue : IComparable<NumericValue>
    {
        // \z (not $) so that a trailing newline is not accepted. [0-9] (not \d) so only ASCII digits count.
        private static readonly Regex DecimalPattern =
            new(@"\A([+-]?)([0-9]+)(?:[.,]([0-9]+))?\z", RegexOptions.CultureInvariant);

        private static readonly Regex FractionPattern =
            new(@"\A([+-]?)([0-9]+)\s*/\s*([0-9]+)\z", RegexOptions.CultureInvariant);

        public BigInteger Numerator { get; }
        public BigInteger Denominator { get; }

        private NumericValue(BigInteger numerator, BigInteger denominator)
        {
            if (denominator.IsZero)
            {
                throw new DivideByZeroException("Denominator cannot be zero.");
            }

            if (denominator.Sign < 0)
            {
                numerator = -numerator;
                denominator = -denominator;
            }

            var gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
            if (!gcd.IsOne)
            {
                numerator /= gcd;
                denominator /= gcd;
            }

            Numerator = numerator;
            Denominator = denominator;
        }

        public bool IsNegative => Numerator.Sign < 0;

        public static NumericValue FromRatio(BigInteger numerator, BigInteger denominator) =>
            new(numerator, denominator);

        /// <summary>|this − other|</summary>
        public NumericValue AbsoluteDifference(NumericValue other) =>
            new(BigInteger.Abs(Numerator * other.Denominator - other.Numerator * Denominator),
                Denominator * other.Denominator);

        /// <summary>Contract §4.6: |answer − key| ≤ tolerance (inclusive, absolute).</summary>
        public bool IsWithin(NumericValue key, NumericValue tolerance) =>
            AbsoluteDifference(key).CompareTo(tolerance) <= 0;

        public int CompareTo(NumericValue other) =>
            (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);

        public override string ToString() =>
            Denominator.IsOne
                ? Numerator.ToString(CultureInfo.InvariantCulture)
                : $"{Numerator.ToString(CultureInfo.InvariantCulture)}/{Denominator.ToString(CultureInfo.InvariantCulture)}";

        /// <summary>
        /// Parses an answer or answer key. Accepts integers, decimals with "." or ",", and fractions
        /// a/b (whitespace allowed only around "/"). On failure <paramref name="errorCode"/> is
        /// <see cref="AnswerErrorCodes.NumericFormat"/> or <see cref="AnswerErrorCodes.NumericZeroDenominator"/>.
        /// Blank input is reported as <see cref="AnswerErrorCodes.NumericFormat"/>; check
        /// <see cref="AnswerNormalization.IsBlank"/> first when blank should mean "unanswered".
        /// </summary>
        public static bool TryParse(string? raw, out NumericValue value, out string? errorCode)
        {
            value = default;
            errorCode = AnswerErrorCodes.NumericFormat;

            if (raw is null)
            {
                return false;
            }

            var s = AnswerNormalization.Basic(raw).Trim();
            if (s.Length == 0 || s.Length > QuestionContract.NumericInputMaxLength)
            {
                return false;
            }

            var match = FractionPattern.Match(s);
            if (match.Success)
            {
                var denominator = ParseDigits(match.Groups[3].Value);
                if (denominator.IsZero)
                {
                    errorCode = AnswerErrorCodes.NumericZeroDenominator;
                    return false;
                }

                var numerator = ParseDigits(match.Groups[2].Value);
                if (match.Groups[1].Value == "-")
                {
                    numerator = -numerator;
                }

                value = new NumericValue(numerator, denominator);
                errorCode = null;
                return true;
            }

            match = DecimalPattern.Match(s);
            if (match.Success)
            {
                var fraction = match.Groups[3].Success ? match.Groups[3].Value : string.Empty;
                var numerator = ParseDigits(match.Groups[2].Value + fraction);
                if (match.Groups[1].Value == "-")
                {
                    numerator = -numerator;
                }

                value = new NumericValue(numerator, BigInteger.Pow(10, fraction.Length));
                errorCode = null;
                return true;
            }

            return false;
        }

        private static BigInteger ParseDigits(string digits) =>
            BigInteger.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
    }
}
