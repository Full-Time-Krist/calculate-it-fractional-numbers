using System;
using System.Globalization;

namespace CalculateIt.FractionalNumbers
{
    public sealed class FractionDigitsRequiredException : InvalidOperationException { }
    public static class FractionMath
    {
        public const decimal Limit = 999999m;
        public static decimal CalculatorTarget(Func<int, int, int> next)
        {
            int bucket = next(0, 100);
            // All entries can be reached by dividing two starter tenths (or entering a starter value).
            decimal[] small = { .1m, .2m, .3m, .4m, .5m, .6m, .7m, .8m, .9m, .25m, .125m, .375m, .625m, .875m, .333333m, .666667m, .142857m, .285714m, .428571m, .571429m };
            decimal[] mixed = { 1.125m, 1.25m, 1.5m, 1.75m, 2.25m, 2.5m, 3.5m, 4.5m, 1.333333m, 1.666667m, 2.333333m, 2.666667m };
            if (bucket < 80) return small[next(0, small.Length)];
            if (bucket < 95) return mixed[next(0, mixed.Length)];
            return next(1, 10);
        }
        public static decimal Normalize(decimal value) => Math.Round(Math.Max(-Limit, Math.Min(Limit, value)), 6, MidpointRounding.AwayFromZero);
        public static string Format(decimal value) => Normalize(value).ToString("0.######", CultureInfo.InvariantCulture);
        public static decimal Parse(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);
        public static decimal Trunc(decimal value) => decimal.Truncate(value);
        public static decimal Frac(decimal value) => value - decimal.Truncate(value);
        public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
        public static int FractionDigits(decimal value)
        {
            var text = Format(Math.Abs(value));
            var dot = text.IndexOf('.');
            return dot < 0 ? 0 : text.Length - dot - 1;
        }
        private static decimal Power10(int digits)
        {
            decimal result = 1; while (digits-- > 0) result *= 10; return result;
        }
        public static decimal LastFractionDigit(decimal value, int change)
        {
            int digits = FractionDigits(value);
            if (digits == 0) throw new FractionDigitsRequiredException();
            return Normalize(Math.Sign(value) * (Math.Abs(value) + change / Power10(digits)));
        }
        public static decimal RemoveLastFractionDigit(decimal value)
        {
            int digits = FractionDigits(value);
            if (digits == 0) throw new FractionDigitsRequiredException();
            var scale = Power10(digits - 1);
            return decimal.Truncate(value * scale) / scale;
        }
        public static decimal SwapParts(decimal value)
        {
            var text = Format(Math.Abs(value));
            var parts = text.Split('.');
            var fraction = parts.Length == 2 ? parts[1] : "0";
            return Normalize((value < 0 ? -1 : 1) * Parse(fraction + "." + parts[0]));
        }
        public static decimal RandomFraction(decimal value, Func<int, int, int> next)
        {
            int digits = Math.Max(1, FractionDigits(value));
            int denominator = (int)Power10(digits);
            return Normalize((value < 0 ? -1 : 1) * (decimal.Truncate(Math.Abs(value)) + (decimal)next(0, denominator) / denominator));
        }
        public static decimal Sqrt(decimal value)
        {
            if (value < 0) throw new ArithmeticException("Negative square root");
            return Normalize((decimal)Math.Sqrt((double)value));
        }
        public static decimal Binary(decimal left, decimal right, int operation)
        {
            switch (operation)
            {
                case 1: return Normalize(left + right);
                case 2: return Normalize(left - right);
                case 3: return Normalize(left * right);
                case 4: return Normalize(left / right);
                case 6: return Normalize(left % right);
                case 10: return Normalize(left * right / 100m);
                default: throw new InvalidOperationException("Unsupported operation");
            }
        }
    }

    // Independent of Unity; the same model is exercised by the console tests.
    public sealed class FractionCalculation
    {
        public decimal Value;
        public decimal Operand;
        public int Pending;
        public bool ReplaceInput;

        public void Input(decimal value) { Value = FractionMath.Normalize(value); ReplaceInput = false; }
        public void Digit(int digit)
        {
            if (digit < 0 || digit > 9) throw new ArgumentOutOfRangeException(nameof(digit));
            if (ReplaceInput) { Input(digit); return; }
            var text = FractionMath.Format(Value);
            if (text.Replace("-", "").Replace(".", "").Length >= 12) return;
            Input(FractionMath.Parse(text == "0" ? digit.ToString(CultureInfo.InvariantCulture) : text + digit));
        }
        public void Begin(int operation)
        {
            if (Pending != 0 && !ReplaceInput) Resolve();
            Operand = Value; Pending = operation; ReplaceInput = true;
        }
        public void Resolve()
        {
            if (Pending == 0) return;
            if (ReplaceInput) throw new InvalidOperationException("Enter the second operand first");
            // Calculate before changing state: invalid operations are transactional.
            var result = FractionMath.Binary(Operand, Value, Pending);
            Value = result; Pending = 0; ReplaceInput = false;
        }
        public void Unary(Func<decimal, decimal> operation)
        {
            var copy = new FractionCalculation { Value = Value, Operand = Operand, Pending = Pending, ReplaceInput = ReplaceInput };
            copy.Resolve();
            var result = FractionMath.Normalize(operation(copy.Value));
            Value = result; Pending = 0; ReplaceInput = false;
        }
    }
}
