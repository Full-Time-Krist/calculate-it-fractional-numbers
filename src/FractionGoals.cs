using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace CalculateIt.FractionalNumbers
{
    public sealed class FractionGoalState : MonoBehaviour
    {
        public string ExactTarget = "";
        public string NextTarget = "";
        public bool NextReady;
        public static FractionGoalState For(StartLvl level) => level.GetComponent<FractionGoalState>() ?? level.gameObject.AddComponent<FractionGoalState>();
    }

    public static class FractionGoals
    {
        public static bool Enabled => GamePrefs.GetInt("Type") == FractionCalculator.Type && GamePrefs.GetInt("Tutorial") != 1 && GamePrefs.GetInt("challenge") == 0;
        public static decimal Target(StartLvl level)
        {
            var state = level.GetComponent<FractionGoalState>();
            return Enabled && state && decimal.TryParse(state.ExactTarget, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : level.number;
        }
        static decimal Generate(StartLvl level, string native)
        {
            // Preserve explicit boss goals and the native Metronome's integer-only rule.
            if ((native == "0" && level.IsBossRound) || GameObject.Find("passive_metronome"))
                return decimal.Parse(native, CultureInfo.InvariantCulture);
            return FractionMath.CalculatorTarget(UnityEngine.Random.Range);
        }
        public static void SetText(Text text, string value)
        {
            if (Enabled && !RunSave.Restoring && !RunSave.TryGetPendingSeed(out _))
            {
                var level = UnityEngine.Object.FindFirstObjectByType<StartLvl>();
                if (level && text == level.need)
                {
                    var state = FractionGoalState.For(level);
                    decimal target = state.NextReady ? FractionMath.Parse(state.NextTarget) : Generate(level, value);
                    state.NextReady = false;
                    state.ExactTarget = FractionMath.Format(target);
                    // The native int is only a comparison proxy. Exact score comparisons use Target().
                    level.number = (int)decimal.Truncate(target);
                    value = state.ExactTarget;
                    text.resizeTextForBestFit = true; text.resizeTextMinSize = 18; text.resizeTextMaxSize = Math.Max(18, text.fontSize);
                }
                else if (level && level.future && text.transform.IsChildOf(level.future.transform) && (text.name == "op_text_l" || text.name == "op_text"))
                {
                    var state = FractionGoalState.For(level);
                    if (!state.NextReady)
                    {
                        state.NextTarget = FractionMath.Format(Generate(level, level.number.ToString(CultureInfo.InvariantCulture)));
                        state.NextReady = true;
                    }
                    value = (text.name == "op_text_l" ? "Next number = " : "Будет число = ") + state.NextTarget;
                }
            }
            text.text = value;
        }
    }

    [HarmonyPatch]
    internal static class FractionGoalTextPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            return typeof(StartLvl).GetMethods(flags).Where(m=>m.GetMethodBody()!=null).Cast<MethodBase>()
                .Concat(typeof(StartLvl).GetNestedTypes(flags).SelectMany(t=>t.GetMethods(flags)).Where(m=>m.Name=="MoveNext" && m.GetMethodBody()!=null));
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var setter = AccessTools.PropertySetter(typeof(Text), "text");
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(setter))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(FractionGoals), nameof(FractionGoals.SetText));
                }
                yield return instruction;
            }
        }
    }
}
