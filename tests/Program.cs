using System;
using System.Globalization;
using CalculateIt.FractionalNumbers;

int assertions = 0;
void Equal<T>(T expected, T actual)
{
    assertions++;
    if (!Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}");
}
var c = new FractionCalculation { Value = .1m };
c.Begin(1); c.Input(.2m); c.Resolve(); Equal(.3m, c.Value);
c.Begin(3); c.Input(10m); c.Resolve(); Equal(3m, c.Value);
c.Begin(4); c.Input(2m); c.Resolve(); Equal(1.5m, c.Value);
c.Begin(2); c.Input(4.2m); c.Resolve(); Equal(-2.7m, c.Value);
c.Unary(decimal.Floor); Equal(-3m, c.Value);
c.Input(-2.7m); c.Unary(decimal.Ceiling); Equal(-2m, c.Value);
c.Input(-2.5m); c.Unary(v => Math.Round(v, 0, MidpointRounding.AwayFromZero)); Equal(-3m, c.Value);
c.Input(2.5m); c.Unary(v => Math.Round(v, 0, MidpointRounding.AwayFromZero)); Equal(3m, c.Value);
c.Input(2m); c.Unary(FractionMath.Sqrt); Equal(1.414214m, c.Value);
Equal(3m, FractionMath.Sqrt(9m));
Equal("0.3", FractionMath.Format(.1m + .2m));
Equal("1.5", FractionMath.Format(1.500m));
Equal("0", FractionMath.Format(-.0000001m));
c.Input(.1m); c.Digit(2); Equal(.12m, c.Value);
c.Begin(1); c.Digit(2); Equal(2m, c.Value);
c.Begin(3); Equal(2.12m, c.Value); c.Input(2); c.Resolve(); Equal(4.24m, c.Value);
c.Begin(1); c.Begin(2); c.Input(.24m); c.Resolve(); Equal(4m, c.Value);
c.Begin(4); c.Input(0); var before = (c.Value, c.Operand, c.Pending, c.ReplaceInput);
try { c.Resolve(); throw new Exception("Zero divisor accepted"); } catch (DivideByZeroException) { assertions++; }
Equal(before, (c.Value, c.Operand, c.Pending, c.ReplaceInput));
c.Input(2); c.Resolve(); Equal(2m, c.Value);
c.Input(-1); try { c.Unary(FractionMath.Sqrt); throw new Exception("Negative root accepted"); } catch (ArithmeticException) { assertions++; }
Equal(-1m, c.Value);
c.Input(.1m); c.Begin(1); c.Input(.2m); c.Unary(decimal.Ceiling); Equal(1m, c.Value); Equal(0, c.Pending);
c.Input(3); c.Begin(4); c.Input(0); before = (c.Value, c.Operand, c.Pending, c.ReplaceInput);
try { c.Unary(decimal.Ceiling); throw new Exception("Invalid pending operation accepted"); } catch (DivideByZeroException) { assertions++; }
Equal(before, (c.Value, c.Operand, c.Pending, c.ReplaceInput));
Equal(999999m, FractionMath.Binary(999999, 999999, 3));
Equal(-999999m, FractionMath.Normalize(-1000000));
Equal(.333333m, FractionMath.Binary(1, 3, 4));
Equal(5m, FractionMath.Binary(50, 10, 10));
Equal(.1m, FractionMath.Binary(.5m, .2m, 6));
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
Equal("1.25", FractionMath.Format(1.25m)); Equal(1.25m, FractionMath.Parse("1.25"));
Equal(2m, FractionMath.Trunc(2.7m)); Equal(-2m, FractionMath.Trunc(-2.7m));
Equal(0m, FractionMath.Trunc(-.7m));
Equal(.7m, FractionMath.Frac(2.7m)); Equal(-.7m, FractionMath.Frac(-2.7m));
Equal(0m, FractionMath.Frac(-2m)); Equal(.000001m, FractionMath.Frac(999998.000001m));
Equal(1.24m, FractionMath.Round2(1.235m)); Equal(-1.24m, FractionMath.Round2(-1.235m));
Equal(1.23m, FractionMath.Round2(1.234999m)); Equal(.01m, FractionMath.Round2(.005m));
Equal(-.01m, FractionMath.Round2(-.005m)); Equal(0m, FractionMath.Round2(-.004999m));
Equal(999999m, FractionMath.Round2(999998.999999m));
c = new FractionCalculation();
c.Input(.25m); c.Begin(1); c.Input(.01m); c.Resolve(); Equal(.26m, c.Value);
c.Begin(3); c.Input(100m); c.Unary(FractionMath.Trunc); Equal(26m, c.Value);
c.Input(-2.75m); c.Unary(FractionMath.Frac); Equal(-.75m, c.Value);
c.Input(.25m); c.Begin(1); c.Input(.01m); c.Unary(FractionMath.Round2); Equal(.26m, c.Value);
c.Input(-2.7m); c.Unary(FractionMath.Trunc); Equal(-2m, c.Value);
Equal("0", FractionMath.Format(FractionMath.Round2(-.000001m)));
Equal(1.3m, FractionMath.LastFractionDigit(1.29m, 1));
Equal(1.3m, FractionMath.LastFractionDigit(1.2m, 1));
Equal(2m, FractionMath.LastFractionDigit(1.99m, 1));
Equal(1.2m, FractionMath.LastFractionDigit(1.21m, -1));
Equal(-1.3m, FractionMath.LastFractionDigit(-1.29m, 1));
Equal(-1.28m, FractionMath.LastFractionDigit(-1.29m, -1));
Equal(0m, FractionMath.LastFractionDigit(.01m, -1));
Equal(1.2m, FractionMath.RemoveLastFractionDigit(1.29m));
Equal(1m, FractionMath.RemoveLastFractionDigit(1.2m));
Equal(-1.2m, FractionMath.RemoveLastFractionDigit(-1.29m));
Equal(1m, FractionMath.RemoveLastFractionDigit(1.01m));
Equal(345.12m, FractionMath.SwapParts(12.345m));
Equal(3.12m, FractionMath.SwapParts(12.03m));
Equal(-3.12m, FractionMath.SwapParts(-12.03m));
Equal(.12m, FractionMath.SwapParts(12m));
Equal(12m, FractionMath.SwapParts(.0012m));
Equal(0m, FractionMath.SwapParts(0m));
Equal(12.999m, FractionMath.RandomFraction(12.345m, (min,max) => { Equal(0,min); Equal(1000,max); return max-1; }));
Equal(-12.999m, FractionMath.RandomFraction(-12.345m, (min,max) => max-1));
Equal(12m, FractionMath.RandomFraction(12.345m, (min,max) => min));
Equal(12.9m, FractionMath.RandomFraction(12m, (min,max) => { Equal(10,max); return max-1; }));
Equal(1.1m, FractionMath.Binary(5.5m, 2.2m, 6));
Equal(-1.1m, FractionMath.Binary(-5.5m, 2.2m, 6));
for (int digit=1; digit<=9; digit++) {
    c = new FractionCalculation { Value=10m }; decimal amount=digit/10m;
    c.Unary(v=>v+amount); Equal(10m+amount,c.Value);
    c.Unary(v=>v-amount); Equal(10m,c.Value);
}
c = new FractionCalculation { Value=2m };
try { c.Unary(v=>FractionMath.LastFractionDigit(v,1)); throw new Exception("Missing fractional digits accepted"); } catch (FractionDigitsRequiredException) { assertions++; }
Equal(2m,c.Value);
try { c.Unary(FractionMath.RemoveLastFractionDigit); throw new Exception("Missing fractional digits accepted"); } catch (FractionDigitsRequiredException) { assertions++; }
Equal(2m,c.Value);
int smallCount=0, mixedCount=0, wholeCount=0;
for(int bucket=0;bucket<100;bucket++) {
    int call=0;
    decimal goal=FractionMath.CalculatorTarget((min,max)=>call++==0?bucket:min);
    if(goal>0 && goal<1) smallCount++;
    else if(goal!=decimal.Truncate(goal)) mixedCount++;
    else wholeCount++;
}
Equal(80,smallCount); Equal(15,mixedCount); Equal(5,wholeCount);
foreach(int bucket in new[]{0,80}) {
    int length=bucket==0?20:12;
    for(int index=0;index<length;index++) {
        int call=0;
        decimal goal=FractionMath.CalculatorTarget((min,max)=>call++==0?bucket:index);
        bool reachable=goal==.25m || (goal>0 && goal<1 && goal*10==decimal.Truncate(goal*10));
        for(int numerator=1;numerator<=9;numerator++)
            for(int denominator=1;denominator<=9;denominator++)
                reachable |= FractionMath.Normalize((decimal)numerator/denominator)==goal;
        Equal(true,reachable);
    }
}
Console.WriteLine($"PASS: {assertions} arithmetic, target-generation and state-transition assertions");
