using System;
using System.Collections.Generic;

namespace CalculateIt.FractionalNumbers
{
    public sealed class Definition
    {
        public string Key, Label, Russian, English, RussianDescription, EnglishDescription;
        public bool Number;
        public decimal Value;
        public Func<decimal, decimal> Operation;
        public int BinaryOperation;
    }
    public static class Definitions
    {
        public const int FirstId = 500;
        public static readonly Definition[] All = Create();
        private static Definition[] Create()
        {
            var list = new List<Definition> {
            new Definition { Key="number_fraction_01", Label="0.1", Russian="Одна десятая", English="One tenth", Number=true, Value=.1m },
            new Definition { Key="number_fraction_02", Label="0.2", Russian="Две десятых", English="Two tenths", Number=true, Value=.2m },
            new Definition { Key="number_fraction_05", Label="0.5", Russian="Половина", English="One half", Number=true, Value=.5m },
            new Definition { Key="operator_fraction_sqrt", Label="√f", Russian="Дробный корень", English="Fractional root", RussianDescription="Квадратный корень с дробной частью. √f(2) = 1.414214. Оригинальный корень не изменяется.", EnglishDescription="Square root retaining decimals. √f(2) = 1.414214. The original root is unchanged." },
            new Definition { Key="operator_fraction_round", Label="round", Russian="Округление", English="Round", RussianDescription="До ближайшего целого. Половины от нуля: 2.5 → 3; −2.5 → −3.", EnglishDescription="Nearest integer. Halves away from zero: 2.5 → 3; −2.5 → −3." },
            new Definition { Key="operator_fraction_floor", Label="floor", Russian="Вниз", English="Floor", RussianDescription="До меньшего целого: 2.7 → 2; −2.7 → −3.", EnglishDescription="Round down: 2.7 → 2; −2.7 → −3." },
            new Definition { Key="operator_fraction_ceil", Label="ceil", Russian="Вверх", English="Ceiling", RussianDescription="До большего целого: 2.1 → 3; −2.7 → −2.", EnglishDescription="Round up: 2.1 → 3; −2.7 → −2." },
            // Append only: indices and keys of existing buttons are part of saved runs.
            new Definition { Key="number_fraction_025", Label="0.25", Russian="Четверть", English="One quarter", Number=true, Value=.25m },
            new Definition { Key="number_fraction_001", Label="0.01", Russian="Одна сотая", English="One hundredth", Number=true, Value=.01m },
            new Definition { Key="operator_fraction_trunc", Label="trunc", Russian="Целая часть", English="Truncate", Operation=FractionMath.Trunc, RussianDescription="Отбрасывает дробную часть к нулю: 2.7 → 2; −2.7 → −2.", EnglishDescription="Discard the fractional part toward zero: 2.7 → 2; −2.7 → −2." },
            new Definition { Key="operator_fraction_frac", Label="frac", Russian="Дробная часть", English="Fractional part", Operation=FractionMath.Frac, RussianDescription="Оставляет дробную часть со знаком числа: 2.7 → 0.7; −2.7 → −0.7.", EnglishDescription="Keep the signed fractional part: 2.7 → 0.7; −2.7 → −0.7." },
            new Definition { Key="operator_fraction_round2", Label="rnd.2", Russian="До сотых", English="Round to hundredths", Operation=FractionMath.Round2, RussianDescription="Округляет до двух знаков после точки. Половины от нуля: 1.235 → 1.24; −1.235 → −1.24.", EnglishDescription="Round to two decimal places. Halves away from zero: 1.235 → 1.24; −1.235 → −1.24." }
            };
            for (int digit = 1; digit <= 9; digit++)
            {
                if (digit == 1 || digit == 2 || digit == 5) continue;
                string label = "0." + digit;
                list.Add(new Definition { Key="number_fraction_0"+digit, Label=label, Russian="Число "+label, English="Number "+label, Number=true, Value=digit/10m });
            }
            for (int digit = 1; digit <= 9; digit++)
            {
                decimal amount = digit / 10m;
                foreach (int sign in new[] { 1, -1 })
                {
                    decimal delta = sign * amount;
                    string label = (sign > 0 ? "+" : "−") + "0." + digit;
                    list.Add(new Definition { Key="operator_fraction_"+(sign>0?"add_":"subtract_")+digit,
                        Label=label, Russian=(sign>0?"Прибавить ":"Вычесть ")+"0."+digit, English=(sign>0?"Add ":"Subtract ")+"0."+digit,
                        Operation=v=>v+delta, RussianDescription="Изменяет текущее число на "+label+", а не вводит новый операнд.", EnglishDescription="Change the current value by "+label+" without entering a new operand." });
                }
            }
            list.Add(new Definition { Key="operator_fraction_divide", Label="/f", Russian="Дробное деление", English="Decimal division", BinaryOperation=4, RussianDescription="Выберите делитель. Деление сохраняет дробную часть: 5 /f 2 = 2.5.", EnglishDescription="Choose a divisor. Division retains decimals: 5 /f 2 = 2.5." });
            list.Add(new Definition { Key="operator_fraction_remainder", Label="mod.f", Russian="Дробный остаток", English="Decimal remainder", BinaryOperation=6, RussianDescription="Выберите делитель. Сохраняет дробный остаток: 5.5 mod.f 2.2 = 1.1.", EnglishDescription="Choose a divisor. Retain the decimal remainder: 5.5 mod.f 2.2 = 1.1." });
            list.Add(new Definition { Key="operator_fraction_half", Label="/2", Russian="Разделить на 2", English="Divide by 2", Operation=v=>v/2, RussianDescription="Делит текущее число на 2 с дробным результатом.", EnglishDescription="Divide the current value by 2, retaining decimals." });
            list.Add(new Definition { Key="operator_fraction_quarter", Label="/4", Russian="Разделить на 4", English="Divide by 4", Operation=v=>v/4, RussianDescription="Делит текущее число на 4 с дробным результатом.", EnglishDescription="Divide the current value by 4, retaining decimals." });
            list.Add(new Definition { Key="operator_fraction_swap", Label="swap.f", Russian="Поменять части", English="Swap parts", Operation=FractionMath.SwapParts, RussianDescription="Меняет целую и дробную части местами: 12.345 → 345.12. Знак сохраняется.", EnglishDescription="Swap integer and fractional parts: 12.345 → 345.12. Keep the sign." });
            list.Add(new Definition { Key="operator_fraction_random", Label="rand.f", Russian="Случайная дробь", English="Random fraction", Operation=v=>FractionMath.RandomFraction(v,UnityEngine.Random.Range), RussianDescription="Меняет только дробную часть, сохраняя её видимую длину. Для целого числа добавляет одну случайную дробную цифру.", EnglishDescription="Randomize only the fraction, keeping its visible length. Integers get one random fractional digit." });
            list.Add(new Definition { Key="operator_fraction_last_plus", Label="f+1", Russian="Последняя цифра +1", English="Last fractional digit +1", Operation=v=>FractionMath.LastFractionDigit(v,1), RussianDescription="Добавляет единицу к последнему дробному разряду: 1.29 → 1.3; 1.2 → 1.3. Для отрицательных чисел меняет модуль.", EnglishDescription="Increment the last fractional digit: 1.29 → 1.3; 1.2 → 1.3. Edit the magnitude for negative numbers." });
            list.Add(new Definition { Key="operator_fraction_last_minus", Label="f−1", Russian="Последняя цифра −1", English="Last fractional digit −1", Operation=v=>FractionMath.LastFractionDigit(v,-1), RussianDescription="Убавляет единицу в последнем дробном разряде: 1.21 → 1.2. Для отрицательных чисел меняет модуль.", EnglishDescription="Decrement the last fractional digit: 1.21 → 1.2. Edit the magnitude for negative numbers." });
            list.Add(new Definition { Key="operator_fraction_last_remove", Label="cut.f", Russian="Убрать дробную цифру", English="Remove fractional digit", Operation=FractionMath.RemoveLastFractionDigit, RussianDescription="Удаляет последнюю видимую цифру дробной части: 1.29 → 1.2; 1.2 → 1.", EnglishDescription="Remove the last visible fractional digit: 1.29 → 1.2; 1.2 → 1." });
            return list.ToArray();
        }
        public static bool IsId(int id) => id >= FirstId && id < FirstId + All.Length;
        public static void Describe(Infos info, int index)
        {
            var d = All[index];
            info.id = FirstId + index;
            info.rname = d.Russian;
            info.rdescription = d.Number ? "Вводит " + d.Label + " как отдельное число. Заменяет вводимый операнд." : d.RussianDescription;
            bool russian = GamePrefs.GetString("lang") == "rus";
            info.name = russian ? info.rname : d.English;
            info.description = russian ? info.rdescription : d.Number ? "Enters " + d.Label + " as one number. Replaces the input operand." : d.EnglishDescription;
        }
    }
}
