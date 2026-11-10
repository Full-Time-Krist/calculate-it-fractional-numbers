using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Globalization;
using System.Reflection.Emit;
using System.Linq;
using CalculateIt.ModLoader;

namespace CalculateIt.FractionalNumbers
{
    [BepInPlugin(Id, "Fractional Numbers", "0.7.1")]
    [BepInDependency("calculateit.modloader", "1.3.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "calculateit.fractionalnumbers";
        private Harmony harmony;
        private void Awake()
        {
            ModCollections.Register(Id, Definitions.All.Select(d=>new CollectionEntry {
                Id=d.Key, Kind=CollectionKind.Button, Symbol=d.Label,
                RussianName=d.Russian, EnglishName=d.English,
                RussianDescription=d.Number?"Вводит "+d.Label+" как отдельное число. Заменяет вводимый операнд.":d.RussianDescription,
                EnglishDescription=d.Number?"Enters "+d.Label+" as one number. Replaces the input operand.":d.EnglishDescription
            }).Concat(FractionPassives.Collection()));
            ((HashSet<string>)AccessTools.Field(typeof(RunSave), "StateTypes").GetValue(null)).Add(nameof(FractionState));
            ((HashSet<string>)AccessTools.Field(typeof(RunSave), "StateTypes").GetValue(null)).Add(nameof(FractionPassiveState));
            ((HashSet<string>)AccessTools.Field(typeof(RunSave), "StateTypes").GetValue(null)).Add(nameof(FractionGoalState));
            harmony = new Harmony(Id);
            harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo("Fractional Numbers initialized: " + Definitions.All.Length + " native buttons, decimal arithmetic");
        }
    }

    [HarmonyPatch(typeof(RunSave), "Awake")]
    internal static class SaveSetup
    {
        static void Prefix(RunSave __instance)
        {
            if (__instance.gameObject.scene.name != "main") return;
            var level = __instance.GetComponent<StartLvl>();
            FractionPassives.State(level);
            FractionGoalState.For(level);
            FractionState.For(level.calculator.GetComponent<MainCalc>());
        }
        static void Postfix(RunSave __instance)
        {
            if (__instance.gameObject.scene.name == "main") {
                Registry.Setup(__instance.GetComponent<StartLvl>(), __instance);
                FractionPassives.Setup(__instance.GetComponent<StartLvl>(), __instance);
            }
        }
    }
    [HarmonyPatch(typeof(MainCalc), "RestoreInputAfterLoad")]
    internal static class RestoreTrackerPatch
    {
        // A 0.1.0 save restores a shorter statistics array after Registry.Setup ran.
        static void Prefix(MainCalc __instance) { Registry.EnsureTracker(__instance); }
    }
    [HarmonyPatch]
    internal static class ArithmeticPatches
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var name in new[] { "Sum", "Minus", "Multiply", "Devide", "Mod", "Percent", "AddNumber", "Sqr", "Sqrt", "Round", "PlusNumber", "MultNumber", "DivNumber", "X_ops_Y" })
                yield return AccessTools.Method(typeof(MainCalc), name);
        }
        static bool Prefix(MainCalc __instance, MethodBase __originalMethod, object[] __args)
        {
            var c = __instance;
            var state = FractionState.For(c);
            if (!state.Active) return true;
            if (!c.CanAcceptInput) return false;
            state.RejectedFrame = -1;
            int n = __args.Length == 0 ? 0 : (int)__args[0];
            switch (__originalMethod.Name)
            {
                case "Sum": state.Begin(1); break;
                case "Minus": state.Begin(2); break;
                case "Multiply": state.Begin(3); break;
                case "Devide": state.Begin(4); break;
                case "Mod": state.Begin(6); break;
                case "Percent": state.Begin(10); break;
                case "AddNumber": if (state.Change(m => m.Digit(n))) { c.used_number = true; state.SetBlocks(false); } break;
                case "Sqr": state.Unary(v => v * v); break;
                // Preserve the original root's actual float rounding behavior.
                case "Sqrt": state.Unary(v => (decimal)Mathf.Round(Mathf.Sqrt((float)v))); break;
                case "Round": state.Unary(v => MainCalc.RoundToNearestTen((long)v)); break;
                case "PlusNumber": state.Unary(v => Math.Max(0, v + n)); break;
                case "MultNumber": state.Unary(v => Math.Max(0, v * n)); break;
                case "DivNumber": state.Unary(v => v / n); break;
                case "X_ops_Y": state.Unary(v => Math.Max(0, n - v)); break;
            }
            return false;
        }
    }
    [HarmonyPatch(typeof(MainCalc), "SendCurrentNumber")]
    internal static class SubmitPatch
    {
        static bool Prefix(MainCalc __instance)
        {
            var state = FractionState.For(__instance);
            if (!state.Active) return true;
            if (__instance.enabled) state.Submit();
            return false;
        }
    }
    [HarmonyPatch(typeof(MainCalc), "Count")]
    internal static class CountPatch
    {
        static bool Prefix(MainCalc __instance, ref IEnumerator __result)
        {
            var state = FractionState.For(__instance);
            if (!state.Active) return true;
            __result = state.Count(); return false;
        }
    }
    [HarmonyPatch]
    internal static class ResetPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(MainCalc), "ResetStates");
            yield return AccessTools.Method(typeof(MainCalc), "AddNumberSend");
        }
        static void Prefix(MainCalc __instance) { FractionState.For(__instance).SyncInteger(); }
        static void Postfix(MainCalc __instance) { FractionState.For(__instance).SyncInteger(); }
    }
    [HarmonyPatch(typeof(MainCalc), "Cancel")]
    internal static class CancelPatch
    {
        static bool Prefix(MainCalc __instance)
        {
            var s = FractionState.For(__instance);
            if (!s.Active || string.IsNullOrEmpty(s.PendingButtonKey)) return true;
            s.CancelCustom(); return false;
        }
        static void Postfix(MainCalc __instance)
        {
            var s = FractionState.For(__instance);
            if (!s.Active || __instance.HasPendingOperation) return;
            s.Pending = 0; s.ReplaceInput = false; s.PendingButtonKey = ""; s.Draw();
        }
    }
    [HarmonyPatch]
    internal static class IntegerOnlyPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var name in new[] { "Reverse", "Flip", "Prime", "Swap", "Median", "Complement", "Remorph", "Absorb", "ShiftNumber", "ShiftNumberleft", "Convert_to", "ConvertTo", "AddToEnd", "AddToBegin", "AddSome", "Secret", "Sup", "Change", "Last", "AddNumberMoney" })
                yield return AccessTools.Method(typeof(MainCalc), name);
        }
        static bool Prefix(MainCalc __instance)
        {
            var state = FractionState.For(__instance);
            if (!state.Active) return true;
            var model = state.Model;
            if (model.Pending == 0 && decimal.Truncate(model.Value) == model.Value) { state.SyncInteger(); return true; }
            state.Notify("Для этой кнопки сначала округлите число", "Round the number before using this button");
            state.RejectedFrame = Time.frameCount;
            return false;
        }
    }
    [HarmonyPatch(typeof(Infos), "Start")]
    internal static class InfoStartPatch
    {
        static bool Prefix(Infos __instance) => InfoUpdatePatch.Render(__instance);
    }
    [HarmonyPatch(typeof(Infos), "Update")]
    internal static class InfoUpdatePatch
    {
        internal static bool Render(Infos info)
        {
            if (FractionPassives.Describe(info)) return false;
            if (!Definitions.IsId(info.id)) return true;
            Definitions.Describe(info, info.id - Definitions.FirstId); return false;
        }
        static bool Prefix(Infos __instance) => Render(__instance);
    }
    [HarmonyPatch(typeof(Randomisator), "NextRandom")]
    internal static class ShopRangePatch
    {
        static void Prefix(Randomisator __instance, int min, ref int max)
        {
            if (__instance.gameObject.scene.name == "main" && min == 0 && max == 48 && Registry.BaseIndex >= 48 && GamePrefs.GetInt("Tutorial") != 1) max = Registry.BaseIndex + Definitions.All.Length;
        }
    }
    [HarmonyPatch(typeof(Randomisator), "OnEnable")]
    internal static class ShopViewPatch
    {
        static void Postfix(Randomisator __instance) { Registry.RenderOffer(__instance); }
    }
    [HarmonyPatch(typeof(Randomisator), "RestoreAfterLoad")]
    internal static class ShopRestorePatch
    {
        static void Postfix(Randomisator __instance) { Registry.RenderOffer(__instance); }
    }
    [HarmonyPatch(typeof(CountUses), "SubUse")]
    internal static class ChargePatch
    {
        static bool Prefix()
        {
            var calc = UnityEngine.Object.FindFirstObjectByType<MainCalc>();
            return !calc || FractionState.For(calc).RejectedFrame != Time.frameCount;
        }
    }
    [HarmonyPatch(typeof(StartLvl), "Update")]
    internal static class GoalPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var parse = AccessTools.Method(typeof(Convert), nameof(Convert.ToInt32), new[] { typeof(string) });
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(parse))
                {
                    // The original level controller parses the scoreboard as an int.
                    // Feed only its win-condition logic a proxy consistent with the exact score.
                    instruction.opcode = OpCodes.Ldarg_0;
                    instruction.operand = null;
                    yield return instruction;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GoalPatch), nameof(GoalScore)));
                }
                else if (instruction.Calls(AccessTools.Method(typeof(MainCalc), "ClearPendingOperation")))
                {
                    var originalCall = new CodeInstruction(instruction.opcode, instruction.operand);
                    instruction.opcode = OpCodes.Dup; instruction.operand = null;
                    yield return instruction;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FractionPassives), nameof(FractionPassives.Accepted)));
                    yield return originalCall;
                }
                else yield return instruction;
            }
        }
        public static int GoalScore(string text, StartLvl level)
        {
            if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return level.number + StartLvl.MoreLessTolerance + 1;
            if (FractionGoals.Enabled && HasExactGoal(level))
            {
                decimal target = FractionGoals.Target(level);
                if (value == target || (FractionPassives.Owned(1) && decimal.Truncate(value) == target)) return level.number;
                decimal delta = value - target;
                if (Math.Abs(delta) <= 1 && GameObject.Find("passive_near1")) return level.number + Math.Sign(delta);
                return level.number + StartLvl.MoreLessTolerance + 1;
            }
            if (value == decimal.Truncate(value)) return (int)value;
            if (FractionPassives.Owned(1) && decimal.Truncate(value) == level.number) return level.number;
            var distance = value - level.number;
            if (Math.Abs(distance) <= 1 && GameObject.Find("passive_near1")) return level.number + Math.Sign(distance);
            if (GamePrefs.GetInt("challenge") == 1 && Math.Abs(distance) <= StartLvl.MoreLessTolerance)
                return level.number + Math.Sign(distance) * (int)Math.Ceiling(Math.Abs(distance));
            // Never let 1.2 pass a goal of 1 by truncating it.
            return level.number + StartLvl.MoreLessTolerance + 1;
        }
        private static bool HasExactGoal(StartLvl level) => !string.IsNullOrEmpty(FractionGoalState.For(level).ExactTarget);
    }
}
