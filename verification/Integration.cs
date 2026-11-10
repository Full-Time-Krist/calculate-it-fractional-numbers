using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BepInEx;
using CalculateIt.FractionalNumbers;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[BepInPlugin("calculateit.fractions.verification", "Fractions verification", "0.3.0")]
[BepInDependency(Plugin.Id)]
public sealed partial class Integration : BaseUnityPlugin
{
    private string result;
    private bool failed;
    private StartLvl level;
    private MainCalc calc;
    private FractionState state;
    private Dictionary<string, int> savedCharges;
    private void Awake()
    {
        result = Environment.GetEnvironmentVariable("CALCULATEIT_VERIFY_OUTPUT");
        if (string.IsNullOrEmpty(result)) return;
        Directory.CreateDirectory(result);
        File.WriteAllText(Path.Combine(result, "result.txt"), "START\n");
        Application.runInBackground = true;
        StartCoroutine(Check());
    }
    private void Step(string name, Action action)
    {
        try { action(); File.AppendAllText(Path.Combine(result, "result.txt"), "PASS " + name + "\n"); }
        catch (Exception ex) { failed = true; File.AppendAllText(Path.Combine(result, "result.txt"), "FAIL " + name + ": " + ex + "\n"); }
    }
    private void Assert(bool test, string message) { if (!test) throw new Exception(message); }
    private void Ready()
    {
        calc.score_is = calc.score_need; calc.can_send = false; level.notwork = false; Time.timeScale = 1;
    }
    private void Value(decimal value)
    {
        Ready(); state.Active = true; state.Store(new FractionCalculation { Value = value });
    }
    private IEnumerator Check()
    {
        if (Environment.GetEnvironmentVariable("CALCULATEIT_VERIFY_COLLECTION") == "1") {
            yield return CheckCollection(); yield break;
        }
        if (Environment.GetEnvironmentVariable("CALCULATEIT_VERIFY_CALCULATOR") == "1") {
            yield return CheckCalculator(); yield break;
        }
        yield return null;
        GamePrefs.SetInt("Tutorial", 0); GamePrefs.SetInt("Type", 0); GamePrefs.SetInt("diff_type", 1); GamePrefs.SetInt("challenge", 0);
        SceneManager.LoadScene("main");
        yield return new WaitForSecondsRealtime(8);
        Step("registry", () => {
            level = UnityEngine.Object.FindFirstObjectByType<StartLvl>();
            calc = level.calculator.GetComponent<MainCalc>(); state = FractionState.For(calc);
            calc.gameObject.SetActive(true); calc.enabled = true; Ready();
            Assert(Registry.Templates.Length == Definitions.All.Length, "Button templates missing");
            Assert(level.buttons_tr.Length == Registry.BaseIndex + Definitions.All.Length, "Shop pool not extended");
            var originalKeys = new[] { "number_fraction_01", "number_fraction_02", "number_fraction_05", "operator_fraction_sqrt", "operator_fraction_round", "operator_fraction_floor", "operator_fraction_ceil" };
            for (int i = 0; i < originalKeys.Length; i++) Assert(Definitions.All[i].Key == originalKeys[i], "0.1.0 button index changed");
            Assert(Definitions.All.Select(d=>d.Key).Distinct().Count()==Definitions.All.Length, "Duplicate button keys");
            foreach (var template in Registry.Templates.Take(12)) {
                level.MiniCreate(template);
            }
            var created = level.calc_transform.GetComponentsInChildren<FractionButton>();
            Assert(created.Length == 12, "Native placement did not create the original twelve buttons");
            foreach (var marker in created) marker.GetComponent<CountUses>().uses = 20;
            calc.InitButts();
        });
        if (failed) { Application.Quit(); yield break; }
        yield return null;
        Step("native and new roots", () => {
            Ready(); state.Active = false; calc.total = 8; calc.output.text = "8"; calc.Sqrt();
            Assert(calc.total == 3, "Original root behavior changed");
            Value(2);
            var root = level.calc_transform.GetComponentsInChildren<FractionButton>().Single(b => b.Index == 3);
            root.GetComponent<Blockirator>()._blocked = false; root.GetComponent<Blockirator>().inf_blocked = false;
            int uses = root.GetComponent<CountUses>().uses;
            root.GetComponent<Button>().onClick.Invoke();
            Assert(state.ExactValue == "1.414214", "Fractional root: " + state.ExactValue);
            Assert(root.GetComponent<CountUses>().uses == uses - 1, "Charge not consumed exactly once");
        });
        Step("native arithmetic patches", () => {
            Value(.1m); calc.Sum(); state.Input(.2m); calc.Multiply();
            Assert(state.ExactValue == "0.3", "Decimal addition inaccurate");
            calc.AddNumber(1); calc.AddNumber(0); state.Unary(v => v);
            Assert(state.ExactValue == "3", "Native digits/multiply lost decimals: " + state.ExactValue);
            Value(-2.7m); state.Unary(decimal.Floor); Assert(state.ExactValue == "-3", "Negative floor");
            Value(-2.7m); state.Unary(decimal.Ceiling); Assert(state.ExactValue == "-2", "Negative ceil");
            Value(.2m); calc.Devide(); state.Input(0); var before = state.ExactValue;
            Assert(!state.Change(m => m.Resolve()), "Zero division accepted");
            Assert(state.ExactValue == before && state.Pending == 4, "Invalid operation modified state");
        });
        Step("five new buttons and exact charge use", () => {
            var cases = new[] {
                (index:7, input:99m, expected:.25m), (index:8, input:99m, expected:.01m),
                (index:9, input:-2.7m, expected:-2m), (index:10, input:-2.7m, expected:-.7m),
                (index:10, input:3m, expected:0m), (index:11, input:1.235m, expected:1.24m),
                (index:11, input:-1.235m, expected:-1.24m)
            };
            foreach (var sample in cases) {
                Value(sample.input);
                var button = level.calc_transform.GetComponentsInChildren<FractionButton>().Single(b => b.Index == sample.index);
                button.GetComponent<Blockirator>()._blocked = false; button.GetComponent<Blockirator>().inf_blocked = false;
                int uses = button.GetComponent<CountUses>().uses;
                button.GetComponent<Button>().onClick.Invoke();
                Assert(state.Model.Value == sample.expected, "Wrong result for " + Definitions.All[sample.index].Key + ": " + state.ExactValue);
                Assert(button.GetComponent<CountUses>().uses == uses - 1, "Charge count wrong for " + Definitions.All[sample.index].Key);
                Assert(button.GetComponent<Infos>().id == Definitions.FirstId + sample.index, "Button ID changed");
            }
        });
        Step("native pending operation accepts a fractional number", () => {
            Ready(); state.Active = false; calc.ClearPendingOperation(); calc.total = 2; calc.output.text = "2"; calc.Sum();
            var button = level.calc_transform.GetComponentsInChildren<FractionButton>().Single(b => b.Index == 0);
            button.GetComponent<Blockirator>()._blocked = false; button.GetComponent<Blockirator>().inf_blocked = false;
            button.GetComponent<Button>().onClick.Invoke();
            Assert(state.ExactValue == "0.1" && state.ExactOperand == "2" && state.Pending == 1, "Native pending operand lost during activation");
            state.Unary(v => v); Assert(state.ExactValue == "2.1", "Mixed native/fraction result wrong");
        });
        Step("all expanded button families", () => {
            for (int index=12; index<Definitions.All.Length; index++) {
                var d=Definitions.All[index];
                var obj=UnityEngine.Object.Instantiate(Registry.Templates[index],level.calc_transform.transform);
                obj.name=d.Key;
                var use=obj.GetComponent<CountUses>(); use.uses=10;
                obj.GetComponent<Blockirator>()._blocked=false; obj.GetComponent<Blockirator>().inf_blocked=false;
                decimal input=12.29m;
                Value(input);
                obj.GetComponent<Blockirator>()._blocked=false;
                obj.GetComponent<Button>().onClick.Invoke();
                Assert(use.uses==9,"New button did not consume exactly one charge: "+d.Key);
                if (d.Number) Assert(state.Model.Value==d.Value,"New numeric button failed: "+d.Key);
                else if (d.Key.Contains("_add_")) Assert(state.Model.Value==input+(index-18)/2/10m+.1m,"Add button entered a number instead of adding");
                else if (d.Key.Contains("_subtract_")) Assert(state.Model.Value==input-(index-18)/2/10m-.1m,"Subtract button entered a number instead of subtracting");
                else if (d.BinaryOperation!=0) {
                    Assert(state.PendingButtonKey==d.Key && state.Pending==d.BinaryOperation,"Binary source identity missing");
                    state.Input(2.2m); state.Unary(v=>v);
                    decimal expected=d.BinaryOperation==4?5.586364m:1.29m;
                    Assert(state.Model.Value==expected,"Fractional binary result wrong: "+d.Key);
                    Value(5.5m); obj.GetComponent<Blockirator>()._blocked=false; obj.GetComponent<Button>().onClick.Invoke();
                    int oldUses=use.uses; calc.Cancel();
                    Assert(state.Pending==0 && use.uses==oldUses+1,"Cancel refunded the wrong binary button: "+d.Key);
                }
                else switch(d.Key) {
                    case "operator_fraction_half": Assert(state.Model.Value==6.145m,"Divide by two"); break;
                    case "operator_fraction_quarter": Assert(state.Model.Value==3.0725m,"Divide by four"); break;
                    case "operator_fraction_swap": Assert(state.Model.Value==29.12m,"Swap parts"); break;
                    case "operator_fraction_random": Assert(decimal.Truncate(state.Model.Value)==12m && FractionMath.FractionDigits(state.Model.Value)<=2,"Random changed integer part"); break;
                    case "operator_fraction_last_plus": Assert(state.Model.Value==12.3m,"Last digit carry/zero trimming"); break;
                    case "operator_fraction_last_minus": Assert(state.Model.Value==12.28m,"Last digit decrement"); break;
                    case "operator_fraction_last_remove": Assert(state.Model.Value==12.2m,"Remove last digit"); break;
                    default: throw new Exception("New button has no integration assertion");
                }
                UnityEngine.Object.DestroyImmediate(obj);
            }
            calc.InitButts();
            foreach(var key in new[]{"operator_fraction_divide","operator_fraction_remainder","operator_fraction_last_plus","operator_fraction_swap"})
                level.MiniCreate(Registry.Templates[Array.FindIndex(Definitions.All,d=>d.Key==key)]);
        });
        Step("cancellation respects glass operation lock", () => {
            Value(.1m); calc.Sum();
            var glass = HarmonyLib.AccessTools.Field(typeof(MainCalc), "pendingOperationGlass");
            glass.SetValue(calc, true); calc.Cancel(); Assert(state.Pending == 1, "Locked glass operation was cancelled");
            glass.SetValue(calc, false); calc.Cancel(); Assert(state.Pending == 0 && state.ExactValue == "0.1", "Cancellation lost exact value");
        });
        Step("shop rendering", () => {
            foreach (var offer in UnityEngine.Object.FindObjectsByType<Randomisator>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(r => r.gameObject.scene.name == "main").Take(1)) {
                offer.type = Registry.BaseIndex + 5; Registry.RenderOffer(offer);
                Assert(offer.symbol.text == "floor", "Custom shop label absent");
                Assert(offer.GetComponent<Infos>().id == Definitions.FirstId + 5, "Custom shop description absent");
            }
        });
        Step("natural shop offers and recharge purchase", () => {
            var offer = UnityEngine.Object.FindObjectsByType<Randomisator>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(r => r.gameObject.scene.name == "main" && !r.inform && r.id >= 3 && r.id <= 5);
            int custom = 0;
            for (int i = 0; i < 100; i++) { offer.OnEnable(); if (offer.type >= Registry.BaseIndex) custom++; }
            Assert(custom > 0, "Custom buttons never selected in the normal shop");
            offer.type = Registry.BaseIndex + 5; offer.glassed = false; Registry.RenderOffer(offer);
            var owned = level.calc_transform.GetComponentsInChildren<FractionButton>().Single(b => b.Index == 5).GetComponent<CountUses>();
            int uses = owned.uses; level.coins = 100; level.Buy(3, offer.gameObject);
            Assert(owned.uses > uses && level.coins == 97, "Native purchase/recharge failed");
        });
        Step("fractional submission", () => {
            Value(1.2m); level.number = 1; level.must_butted = true;
            Assert(state.Submit(), "Submit rejected");
            Assert(Math.Abs(calc.score_need - 1.2) < .0000001, "Score was truncated");
        });
        yield return new WaitForSecondsRealtime(2);
        Step("fractional score cannot win an integer goal", () => {
            Assert(Math.Abs(calc.score_is - 1.2) < .0000001 && calc.can_send, "Fractional score animation did not finish");
            Assert(!level.notwork, "1.2 incorrectly completed a goal of 1");
            Assert(calc.points.text == "1.2", "Fractional scoreboard lost precision");
            level.number = 24;
        });
        Value(2); state.Unary(FractionMath.Sqrt);
        if (level.alert) level.alert.SetActive(false);
        foreach (var obj in new[] { level.core, level.unlock, level.add_info, level.blockirator, level.passiveshop, level.failed, level.choose }) if (obj) obj.SetActive(false);
        ScreenCapture.CaptureScreenshot(Path.Combine(result, "fractions-game.png"));
        yield return new WaitForSecondsRealtime(1);
        Step("save decimal and buttons", () => {
            Value(.1m); state.Begin(4,"operator_fraction_divide"); state.Input(.2m);
            savedCharges = level.calc_transform.GetComponentsInChildren<FractionButton>().ToDictionary(b => Definitions.All[b.Index].Key, b => b.GetComponent<CountUses>().uses);
            // Reproduce the statistics-array length stored by version 0.1.0.
            Array.Resize(ref calc.GetComponent<DifferentOperatorsCount>().operators_used, Definitions.FirstId + 7);
            RunSave.Instance.MarkActive(); Assert(RunSave.Instance.Save(), "Native save failed");
            var json = File.ReadAllText(RunSave.SavePath);
            Assert(json.Contains("FractionState") && json.Contains("number_fraction_01"), "Custom state/templates missing from save");
            Assert(RunSave.Continue(), "Saved run rejected");
        });
        yield return new WaitForSecondsRealtime(5);
        Step("restore exact pending calculation", () => {
            level = UnityEngine.Object.FindFirstObjectByType<StartLvl>(); calc = level.calculator.GetComponent<MainCalc>(); state = FractionState.For(calc);
            Assert(!RunSave.Restoring, "Save restoration did not complete");
            Assert(state.Active && state.ExactValue == "0.2" && state.ExactOperand == "0.1" && state.Pending == 4 && state.PendingButtonKey=="operator_fraction_divide", "Decimal/pending source state lost");
            Assert(level.calc_transform.GetComponentsInChildren<FractionButton>().Length == savedCharges.Count, "Custom buttons did not restore");
            foreach (var button in level.calc_transform.GetComponentsInChildren<FractionButton>())
                Assert(button.GetComponent<CountUses>().uses == savedCharges[Definitions.All[button.Index].Key], "Saved charge count lost");
            var tracker = calc.GetComponent<DifferentOperatorsCount>();
            Assert(tracker.operators_used.Length >= Definitions.FirstId + Definitions.All.Length, "Legacy statistics array was not expanded after restore");
            tracker.SetOp(Definitions.FirstId + Definitions.All.Length-1);
            Assert(tracker.operators_used[Definitions.FirstId + Definitions.All.Length-1], "New button statistics cannot be recorded after restoring a legacy array");
            Ready(); state.Unary(v => v); Assert(state.ExactValue == "0.5", "Restored operation result wrong");
        });
        Step("native passive acquisition and fractional victory", () => {
            var pool = level.passiveshop.GetComponent<PassivePool>();
            Assert(FractionPassives.Templates.Length==4, "Passive registry missing");
            var offer = pool.passives[0];
            for(int i=0;i<4;i++) {
                offer.id=FractionPassives.BaseIndex+i;
                HarmonyLib.AccessTools.Method(typeof(RandomisatorPassives),"OnEnable").Invoke(offer,null);
                Assert(offer.GetComponent<Infos>().description.Length>10,"Passive description missing");
                pool.CreatePassive(offer.gameObject);
                Assert(FractionPassives.Owned(i),"Native passive acquisition failed");
            }
            Value(12.345m); level.number=12; level.round=2; level.turns=100;
            level.notwork=false; level.must_butted=true; calc.can_send=true;
            calc.points.text="12.345";
            var passive=FractionPassives.State(level); passive.EnsureRound(level); passive.LuckyDigit=5;
            int before=level.calc_transform.GetComponentsInChildren<FractionButton>().Sum(b=>b.GetComponent<CountUses>().uses);
            level.coins=100;
            HarmonyLib.AccessTools.Method(typeof(StartLvl),"Update").Invoke(level,null);
            level.StopAllCoroutines();
            Assert(level.notwork,"Almost whole did not accept a fractional result");
            Assert(level.coins==108,"Expected 3 digit coins + 5 lucky coins, got "+level.coins);
            Assert(level.calc_transform.GetComponentsInChildren<FractionButton>().Sum(b=>b.GetComponent<CountUses>().uses)==before+1,"Precision reserve did not add exactly one charge");
            FractionPassives.Accepted(calc);
            Assert(level.coins==108,"Victory rewarded twice");
            Assert(passive.RewardedRound==2,"Reward stamp missing");
            ScreenCapture.CaptureScreenshot(Path.Combine(result,"passives-game.png"));
            Assert(RunSave.Instance.Save(),"Passive save failed");
            Assert(RunSave.Continue(),"Passive save rejected");
        });
        yield return new WaitForSecondsRealtime(5);
        Step("passive save restoration", () => {
            level=UnityEngine.Object.FindFirstObjectByType<StartLvl>(); calc=level.calculator.GetComponent<MainCalc>();
            var passive=FractionPassives.State(level);
            Assert(Enumerable.Range(0,4).All(FractionPassives.Owned),"Owned passives lost on load");
            Assert(passive.LuckyRound==2 && passive.LuckyDigit==5 && passive.RewardedRound==2,"Lucky digit/reward stamp lost on load");
            int coins=level.coins; FractionPassives.Accepted(calc); Assert(level.coins==coins,"Reload duplicated reward");
            level.StopAllCoroutines(); level.enabled=false;
            level.round=3; passive.EnsureRound(level); passive.LuckyDigit=5;
            calc.can_send=true; level.number=12; calc.points.text="12";
            int uses=level.calc_transform.GetComponentsInChildren<FractionButton>().Sum(b=>b.GetComponent<CountUses>().uses);
            FractionPassives.Accepted(calc); Assert(level.coins==coins,"Whole answer paid fractional reward");
            Assert(level.calc_transform.GetComponentsInChildren<FractionButton>().Sum(b=>b.GetComponent<CountUses>().uses)==uses,"Whole answer recharged a button");
            level.round=4; passive.EnsureRound(level); passive.LuckyDigit=5; calc.points.text="12.3400";
            FractionPassives.Accepted(calc); Assert(level.coins==coins+2,"Trailing zeros/wrong lucky digit reward wrong");
            Assert(GoalPatchProxy(-12.8m,-12),"Negative truncation should be toward zero");
        });
        File.AppendAllText(Path.Combine(result, "result.txt"), failed ? "FAILED\n" : "ALL PASS\n");
        Application.Quit();
    }
    bool GoalPatchProxy(decimal value,int target) {
        var level=UnityEngine.Object.FindFirstObjectByType<StartLvl>(); level.number=target;
        var type=typeof(FractionState).Assembly.GetType("CalculateIt.FractionalNumbers.GoalPatch");
        return (int)HarmonyLib.AccessTools.Method(type,"GoalScore").Invoke(null,new object[]{FractionMath.Format(value),level})==target;
    }
}
