using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;
using MoreMountains.Feedbacks;

namespace CalculateIt.FractionalNumbers
{
    public sealed class FractionState : MonoBehaviour
    {
        // Strings are supported by the game's own save serializer, unlike decimal fields.
        public bool Active;
        public string ExactValue = "0", ExactOperand = "0";
        public int Pending;
        public bool ReplaceInput;
        public string PendingButtonKey = "";
        [NonSerialized] public int RejectedFrame = -1;
        private MainCalc Calc => GetComponent<MainCalc>();
        public FractionCalculation Model => new FractionCalculation {
            Value = FractionMath.Parse(ExactValue), Operand = FractionMath.Parse(ExactOperand),
            Pending = Pending, ReplaceInput = ReplaceInput
        };
        public static FractionState For(MainCalc calc)
        {
            var state = calc.GetComponent<FractionState>();
            return state ? state : calc.gameObject.AddComponent<FractionState>();
        }
        public void Activate()
        {
            if (Active) return;
            PendingButtonKey = "";
            var calc = Calc;
            ExactValue = FractionMath.Format(calc.total);
            ExactOperand = FractionMath.Format(calc.operand == 999999999 ? calc.total : calc.operand);
            Pending = calc.operand != 999999999 && Supported((int)calc.currentoperand) ? (int)calc.currentoperand : 0;
            ReplaceInput = calc.startsum;
            Active = true;
        }
        public static bool Supported(int op) => op == 1 || op == 2 || op == 3 || op == 4 || op == 6 || op == 10;
        public void Store(FractionCalculation model)
        {
            ExactValue = FractionMath.Format(model.Value); ExactOperand = FractionMath.Format(model.Operand);
            Pending = model.Pending; ReplaceInput = model.ReplaceInput;
            if (Pending == 0) PendingButtonKey = "";
            var calc = Calc;
            calc.total = (long)model.Value;
            calc.operand = model.Pending == 0 ? 999999999 : (long)model.Operand;
            calc.currentoperand = model.Pending;
            calc.startsum = model.Pending != 0 && model.ReplaceInput;
            calc.operator_count = 0;
            calc.can_send = false;
            Draw();
        }
        public void Draw()
        {
            if (Active && Calc.output)
            {
                Fit(Calc.output); Calc.output.text = ExactValue;
            }
        }
        private void Fit(Text text)
        {
            if (text.resizeTextForBestFit) return;
            text.resizeTextMaxSize = text.fontSize; text.resizeTextMinSize = Math.Min(20, text.fontSize);
            text.resizeTextForBestFit = true;
        }
        public void SyncInteger()
        {
            Active = false; Pending = 0; ReplaceInput = false; PendingButtonKey = "";
            ExactValue = FractionMath.Format(Calc.total); ExactOperand = "0";
        }
        public bool Change(Action<FractionCalculation> action)
        {
            RejectedFrame = -1;
            try { var model = Model; action(model); Store(model); return true; }
            catch (FractionDigitsRequiredException) { RejectedFrame = Time.frameCount; Notify("У числа нет дробной части", "The number has no fractional part"); }
            catch (ArithmeticException) { RejectedFrame = Time.frameCount; Notify("Недопустимая операция: проверьте корень или делитель", "Invalid operation: check the root or divisor"); }
            catch (InvalidOperationException) { RejectedFrame = Time.frameCount; Notify("Сначала введите второй операнд", "Enter the second operand first"); }
            return false;
        }
        public void Input(decimal value)
        {
            Activate();
            if (!Change(m => m.Input(value))) return;
            Calc.used_number = true;
            if (Calc.symbol) Calc.symbol.SetActive(false);
            if (Calc.symbol_help) Calc.symbol_help.SetActive(false);
            SetBlocks(false);
        }
        public bool Begin(int operation, string sourceKey = null)
        {
            var before = Model;
            if (!Change(m => m.Begin(operation))) return false;
            PendingButtonKey = sourceKey ?? "";
            if (before.Pending != 0 && !before.ReplaceInput) MarkOperation(before.Pending);
            var buttonName = operation == 1 ? "operator+" : operation == 2 ? "operator-" : operation == 3 ? "operator*" : operation == 4 ? "operator/" : operation == 6 ? "operator_mod" : "operator%";
            if (!string.IsNullOrEmpty(sourceKey)) buttonName = sourceKey;
            AccessTools.Method(typeof(MainCalc), "ShowCancelHint").Invoke(Calc, new object[] { buttonName });
            if (Calc.symbol)
            {
                Calc.symbol.SetActive(true);
                Calc.symbol.GetComponent<Text>().text = operation == 1 ? "+" : operation == 2 ? "−" : operation == 3 ? "×" : operation == 4 ? "/" : operation == 6 ? "mod" : "%";
            }
            SetBlocks(true);
            return true;
        }
        public void CancelCustom()
        {
            var calc = Calc;
            if (!calc.CanAcceptInput || !calc.HasPendingOperation || (bool)AccessTools.Field(typeof(MainCalc), "pendingOperationGlass").GetValue(calc)) return;
            var level = calc.game.GetComponent<StartLvl>();
            var key = PendingButtonKey;
            var button = level.calc_transform.GetComponentsInChildren<CountUses>(true).FirstOrDefault(b => b.gameObject.name == key);
            if (button) button.AddUses(level.rule == 6 ? 2 : 1);
            level.CancelChallengeButtonUse(key);
            if (level.rule == 11 && level.must_butt && level.must_butt.name == key) level.must_butted = false;
            calc.ClearPendingOperation();
            Pending = 0; ReplaceInput = false; PendingButtonKey = ""; ExactOperand = "0";
            calc.can_send = false; SetBlocks(false);
            if (calc.sender) calc.sender.GetComponent<Blockirator>()._blocked = true;
            Draw();
            var audio = GameObject.Find("UIO"); if (audio && calc.cancel) audio.GetComponent<AudioSource>().PlayOneShot(calc.cancel);
        }
        public bool Unary(Func<decimal, decimal> action)
        {
            Activate();
            var before = Model;
            if (!Change(m => m.Unary(action))) return false;
            if (before.Pending != 0) MarkOperation(before.Pending);
            if (Calc.symbol) Calc.symbol.SetActive(false);
            if (Calc.symbol_help) Calc.symbol_help.SetActive(false);
            SetBlocks(false);
            return true;
        }
        public void SetBlocks(bool waitingForNumber)
        {
            var calc = Calc;
            calc.InitButts();
            foreach (var obj in calc.a_nums.Where(o => o)) if (obj.TryGetComponent<Blockirator>(out var b)) b._blocked = !waitingForNumber && Pending == 0;
            foreach (var obj in calc.a_buttons.Where(o => o)) if (obj.TryGetComponent<Blockirator>(out var b)) b._blocked = waitingForNumber;
            // Keep rounding available after a second operand and keep submit locked before it.
            if (calc.sender && calc.sender.TryGetComponent<Blockirator>(out var sender)) sender._blocked = waitingForNumber;
        }
        public void Notify(string ru, string en)
        {
            var level = Calc.game ? Calc.game.GetComponent<StartLvl>() : null;
            if (level && level.alert)
            {
                level.alert.GetComponent<Text>().text = GamePrefs.GetString("lang") == "rus" ? ru : en;
                var animation = level.alert.GetComponent<Animation>();
                if (animation) animation.Play();
            }
        }
        public bool Submit()
        {
            var calc = Calc;
            var before = Model;
            if (!Change(m => m.Resolve())) return false;
            if (before.Pending != 0) MarkOperation(before.Pending);
            var model = Model;
            var level = calc.game.GetComponent<StartLvl>();
            if (level.rule == 12) { model.Value = FractionMath.Normalize(model.Value + 1); Store(model); }
            if (GameObject.Find("passive_metronome") && model.Value % 2 == 0 && (model.Value != 0 || GamePrefs.GetInt("challenge") != 2))
            { model.Value = FractionMath.Normalize(model.Value + 1); Store(model); }
            if (model.Value % 7 == 0 && GameObject.Find("passive_lucky")) level.ChangeCoins(1);
            if (model.Value % 10 == 0 && GameObject.Find("passive_perfect")) level.ChangeCoins(1);
            if (model.Value % 2 == 1 && GameObject.Find("passive_odd")) level.ChangeCoins(1);
            if (model.Value == 777 && GameObject.Find("passive777")) level.ChangeCoins(10);
            calc.score_need = (double)model.Value;
            calc.score_speed = (calc.score_need - calc.score_is) / 100d;
            SetBlocks(false);
            if (calc.sender) calc.sender.GetComponent<Blockirator>()._blocked = true;
            level.AddTurn();
            if (calc.camera && calc.camera.TryGetComponent<MMF_Player>(out var feedback)) feedback.PlayFeedbacks();
            calc.StartCoroutine(Count());
            return true;
        }
        private void MarkOperation(int operation)
        {
            int index = operation == 1 ? 0 : operation == 3 ? 1 : operation == 2 ? 2 : operation == 4 ? 3 : -1;
            var counter = Calc.GetComponent<OperationsCounter>();
            if (counter && index >= 0 && index < counter.operations.Length) counter.operations[index].used = true;
        }
        public IEnumerator Count()
        {
            var calc = Calc;
            var from = calc.score_is;
            var target = calc.score_need;
            if (calc.points) Fit(calc.points);
            for (int step = 1; step <= 30; step++)
            {
                calc.score_is = from + (target - from) * step / 30d;
                if (calc.points) calc.points.text = FractionMath.Format((decimal)calc.score_is);
                yield return new WaitForSeconds(.01f);
            }
            calc.score_is = target;
            if (calc.points) calc.points.text = FractionMath.Format((decimal)target);
            calc.can_send = !calc.HasPendingOperation;
            var level = calc.game.GetComponent<StartLvl>();
            double goal = (double)FractionGoals.Target(level);
            if (level.renumber && Math.Abs(target - goal) > .0000005 && (!(Math.Abs(target - goal) <= 1) || !GameObject.Find("passive_near1"))) level.Reroll();
        }
    }
}
