using System;
using System.Collections;
using System.IO;
using System.Linq;
using CalculateIt.FractionalNumbers;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed partial class Integration
{
    private string savedGoal;
    private IEnumerator CheckCalculator()
    {
        yield return new WaitForSecondsRealtime(8);
        Step("native calculator card and selection", () => {
            var menu=UnityEngine.Object.FindFirstObjectByType<MenuManagment>();
            var select=Resources.FindObjectsOfTypeAll<Button>().Single(b=>b.gameObject.scene.name=="menu" && b.name=="select_fraction");
            GameObject.Find("Canvas (1)").transform.Find("ShakerItems/buttons/StartGame").GetComponent<Button>().onClick.Invoke();
            var scroll=select.GetComponentInParent<ScrollRect>(true);
            scroll.gameObject.SetActive(true); scroll.horizontalNormalizedPosition=1;
            var card=select.transform.parent.Find("FractionCalculator");
            Assert(card && card.GetComponentsInChildren<Place>(true).Count(p=>p.gameObject.activeSelf)==16,"Native preview missing");
            Assert(card.GetComponentsInChildren<Text>(true).Any(t=>t.text=="0.25"),"Fractional preview missing");
            int count=select.transform.parent.childCount;
            FractionCalculator.Menu(menu); Assert(select.transform.parent.childCount==count,"Menu registration duplicated card");
        });
        yield return new WaitForSecondsRealtime(1);
        ScreenCapture.CaptureScreenshot(Path.Combine(result,"fraction-calculator-menu.png"));
        yield return new WaitForSecondsRealtime(1);
        Step("native selection opens difficulty", () => {
            var menu=UnityEngine.Object.FindFirstObjectByType<MenuManagment>();
            Resources.FindObjectsOfTypeAll<Button>().Single(b=>b.gameObject.scene.name=="menu" && b.name=="select_fraction").onClick.Invoke();
            Assert(GamePrefs.GetInt("Type")==FractionCalculator.Type && menu.diff.activeSelf,"Native selection did not open difficulty screen");
        });
        GamePrefs.SetInt("Tutorial",0); GamePrefs.SetInt("Type",FractionCalculator.Type); GamePrefs.SetInt("diff_type",0); GamePrefs.SetInt("challenge",0);
        SceneManager.LoadScene("main");
        yield return new WaitForSecondsRealtime(8);
        Step("fractional calculator starts with sixteen intended buttons", () => {
            level=UnityEngine.Object.FindFirstObjectByType<StartLvl>(); calc=level.calculator.GetComponent<MainCalc>(); state=FractionState.For(calc);
            var creator=UnityEngine.Object.FindFirstObjectByType<CreateCalc>();
            Assert(creator.Calculators.Length==8 && creator.Calculators[0].calcs.Any(p=>p.name=="number1"),"Native calculators changed");
            var places=level.calc_transform.GetComponentsInChildren<Place>();
            Assert(places.Length==16,"Unexpected initial size: "+places.Length);
            Assert(places.Select(p=>p.name).OrderBy(n=>n).SequenceEqual(FractionCalculator.Keys.OrderBy(n=>n)),"Starter buttons differ from definition");
            Assert(places.Count(p=>p.GetComponent<FractionButton>())==15,"Fractional starter buttons missing");
            var expected = new[] { "0.1", "0.2", "0.3", "round", "0.4", "0.5", "0.6", "ceil", "0.7", "0.8", "0.9", "√f", "trunc", "0.25", "send", "/f" };
            var actual = places.OrderBy(p=>p.overall).Select(p=>p.GetComponent<FractionButton>() is FractionButton b ? Definitions.All[b.Index].Label : "send");
            Assert(actual.SequenceEqual(expected),"Requested row/column order is wrong");
            Assert(places.Any(p=>p.name=="operator_send"),"Send button missing");
            foreach (string key in new[] { "operator_fraction_trunc", "operator_fraction_sqrt", "operator_fraction_divide" })
                Assert(places.Single(p=>p.name==key).GetComponent<CountUses>().uses==10,"Wrong starting reserve for "+key);
            Assert(!string.IsNullOrEmpty(FractionGoalState.For(level).ExactTarget),"Exact goal state missing at start");
            Assert(level.need.text==FractionMath.Format(FractionGoals.Target(level)),"Displayed target is not exact");
            var root=places.Single(p=>p.name=="operator_fraction_sqrt");
            Value(2); root.GetComponent<Blockirator>()._blocked=false; root.GetComponent<Button>().onClick.Invoke();
            Assert(state.ExactValue=="1.414214","Fractional root fails in new calculator");
        });
        Step("fractional reroll goals and exact matching", () => {
            int fractions=0, small=0;
            for(int i=0;i<200;i++) {
                level.Reroll(); decimal target=FractionGoals.Target(level);
                Assert(level.need.text==FractionMath.Format(target),"Reroll display truncated");
                if(target!=decimal.Truncate(target)) fractions++;
                if(target>0 && target<1) small++;
            }
            Assert(fractions>150 && small>120,"Goals are not predominantly small fractions");
            FractionGoalState.For(level).ExactTarget="0.5"; level.number=0; level.need.text="0.5";
            var type=typeof(FractionState).Assembly.GetType("CalculateIt.FractionalNumbers.GoalPatch");
            var score=HarmonyLib.AccessTools.Method(type,"GoalScore");
            Assert((int)score.Invoke(null,new object[]{"0",level})!=level.number,"Integer part falsely wins a fractional target");
            Assert((int)score.Invoke(null,new object[]{"0.5",level})==level.number,"Exact fraction does not match goal");
            savedGoal=FractionGoalState.For(level).ExactTarget;
        });
        ScreenCapture.CaptureScreenshot(Path.Combine(result,"fraction-calculator-game.png"));
        yield return new WaitForSecondsRealtime(1);
        Step("save new calculator and pending operation", () => {
            Value(.25m); state.Begin(4,"operator_fraction_divide"); state.Input(.5m);
            RunSave.Instance.MarkActive(); Assert(RunSave.Instance.Save(),"New calculator save failed");
            Assert(RunSave.Continue(),"New calculator save rejected");
        });
        yield return new WaitForSecondsRealtime(5);
        Step("restore new calculator without duplicate starting buttons", () => {
            level=UnityEngine.Object.FindFirstObjectByType<StartLvl>(); calc=level.calculator.GetComponent<MainCalc>(); state=FractionState.For(calc);
            Assert(!RunSave.Restoring && GamePrefs.GetInt("Type")==FractionCalculator.Type,"Calculator type lost on restore");
            Assert(level.calc_transform.GetComponentsInChildren<Place>().Length==16,"Starting buttons duplicated on restore");
            Assert(state.ExactOperand=="0.25" && state.ExactValue=="0.5" && state.Pending==4,"Pending decimal operation lost");
            Assert(FractionGoalState.For(level).ExactTarget==savedGoal && level.need.text==savedGoal,"Exact goal lost or rerolled during load");
            Assert(level.calc_transform.GetComponentsInChildren<FractionButton>().Single(b=>Definitions.All[b.Index].Key=="operator_fraction_sqrt").GetComponent<CountUses>().uses==9,"Load refilled spent starting reserve");
            Ready(); state.Unary(v=>v); Assert(state.ExactValue=="0.5","Restored fractional division failed");
        });
        Step("native send button completes a round", () => {
            Value(.5m); level.number=0; level.must_butted=true;
            level.calc_transform.GetComponentsInChildren<Place>().Single(p=>p.name=="operator_send").GetComponent<Button>().onClick.Invoke();
            Assert(calc.score_need==.5,"Native send button failed");
        });
        yield return new WaitForSecondsRealtime(2);
        Step("new calculator round accepted",()=>Assert(level.notwork,"Goal did not accept sent result"));
        yield return new WaitForSecondsRealtime(4);
        Step("next round consumes the exact previewed target", () => {
            Assert(level.round==2,"Round did not advance");
            var goals=FractionGoalState.For(level);
            FractionGoals.SetText(level.future.transform.Find("op_text_l").GetComponent<Text>(),"Next number = 5");
            string next=goals.NextTarget;
            Assert(goals.NextReady && !string.IsNullOrEmpty(next),"Exact future target missing");
            FractionGoals.SetText(level.future.transform.Find("op_text").GetComponent<Text>(),"Будет число = 5");
            Assert(goals.NextTarget==next,"Localized future label rerolled target");
            level.NextLvl();
            Assert(!goals.NextReady && goals.ExactTarget==next && level.need.text==next,"Next round ignored its previewed fractional target");
        });
        File.AppendAllText(Path.Combine(result,"result.txt"),failed?"FAILED\n":"ALL PASS\n");
        Application.Quit();
    }
}
