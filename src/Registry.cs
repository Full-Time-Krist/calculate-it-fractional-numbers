using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace CalculateIt.FractionalNumbers
{
    public static class Registry
    {
        public static int BaseIndex = -1;
        public static GameObject[] Templates = Array.Empty<GameObject>();
        private static GameObject holder;
        public static void Setup(StartLvl level, RunSave save)
        {
            if (level.buttons_tr.Any(t => t && t.GetComponent<FractionButton>())) return;
            if (holder) UnityEngine.Object.Destroy(holder);
            holder = new GameObject("FractionalNumbers.Templates");
            holder.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(holder);
            BaseIndex = level.buttons_tr.Length;
            var saveField = AccessTools.Field(typeof(RunSave), "templates");
            var saveTemplates = (GameObject[])saveField.GetValue(save);
            var built = new List<GameObject>();
            var donor = level.buttons_tr.First(t => t && t.GetComponent<CountUses>() && t.Find("Text"));
            for (int i = 0; i < Definitions.All.Length; i++)
            {
                var d = Definitions.All[i];
                var source = d.Number ? level.all_numbers[1] : donor.gameObject;
                var clone = UnityEngine.Object.Instantiate(source, holder.transform, false);
                clone.name = d.Key;
                clone.tag = d.Number ? "number_tag" : "operator_tag";
                // Active self is preserved when a purchased instance leaves the inactive holder.
                clone.SetActive(true);
                foreach (var script in clone.GetComponents<MonoBehaviour>())
                    if (script && new[] { "NumberReader", "ButtonSetup", "AppendButtonText", "OffsetButtonText", "UsesNumber", "PrimeControll", "LastControll" }.Contains(script.GetType().Name)) script.enabled = false;
                var info = clone.GetComponent<Infos>() ?? clone.AddComponent<Infos>();
                Definitions.Describe(info, i);
                var marker = clone.AddComponent<FractionButton>(); marker.Index = i;
                var stamp = clone.GetComponent<RunSaveTemplate>() ?? clone.AddComponent<RunSaveTemplate>();
                stamp.id = saveTemplates.Length + i;
                clone.GetComponent<CountUses>().buttonKey = d.Key;
                marker.Paint();
                built.Add(clone);
            }
            Templates = built.ToArray();
            level.buttons_tr = level.buttons_tr.Concat(Templates.Select(t => t.transform)).ToArray();
            saveField.SetValue(save, saveTemplates.Concat(Templates).ToArray());
            EnsureTracker(level.calculator.GetComponent<MainCalc>());
        }
        public static void EnsureTracker(MainCalc calculator)
        {
            var tracker = calculator.GetComponent<DifferentOperatorsCount>();
            if (tracker && tracker.operators_used.Length < Definitions.FirstId + Definitions.All.Length)
                Array.Resize(ref tracker.operators_used, Definitions.FirstId + Definitions.All.Length);
        }
        public static void RenderOffer(Randomisator offer)
        {
            int index = offer.type - BaseIndex;
            if (offer.gameObject.scene.name != "main" || BaseIndex < 0 || index < 0 || index >= Definitions.All.Length) return;
            var d = Definitions.All[index];
            offer.gameObject.tag = "shop_op_tag";
            if (offer.icon) offer.icon.gameObject.SetActive(false);
            if (offer.small_ico) offer.small_ico.SetActive(false);
            if (offer.symbol) { offer.symbol.enabled = true; offer.symbol.text = d.Label; offer.symbol.fontSize = d.Number ? 42 : 32; }
            if (offer.item) offer.item.color = offer.glassed ? offer.colores[4] : new Color(.62f, .72f, .95f);
            Definitions.Describe(offer.GetComponent<Infos>(), index);
            offer.GetComponent<Infos>().enabled = true;
        }
    }

    public sealed class FractionButton : MonoBehaviour
    {
        public int Index;
        private void Awake()
        {
            var clicks = new Button.ButtonClickedEvent();
            clicks.AddListener(Click);
            AccessTools.Field(typeof(CountUses), "normalClick").SetValue(GetComponent<CountUses>(), clicks);
            Paint();
        }
        public void Paint()
        {
            var d = Definitions.All[Index];
            var text = transform.Find("Text");
            if (text && text.TryGetComponent<Text>(out var label))
            {
                text.gameObject.SetActive(true); label.enabled = true; label.text = d.Label;
                label.resizeTextForBestFit = true; label.resizeTextMinSize = 18; label.resizeTextMaxSize = d.Number ? 62 : 42;
            }
            var icon = transform.Find("icon"); if (icon) icon.gameObject.SetActive(false);
            Definitions.Describe(GetComponent<Infos>(), Index);
            var image = GetComponent<Image>(); if (image && !GetComponent<CountUses>().glassed) image.color = d.Number ? new Color(.84f, .87f, 1f) : new Color(.69f, .74f, 1f);
        }
        private void Start() { Paint(); }
        private void Click()
        {
            var calc = UnityEngine.Object.FindFirstObjectByType<MainCalc>();
            if (!calc || !calc.CanAcceptInput) return;
            var state = FractionState.For(calc);
            state.RejectedFrame = -1;
            if (calc.HasPendingOperation && !FractionState.Supported((int)calc.currentoperand))
            { state.Notify("Сначала завершите текущую операцию", "Finish the current operation first"); return; }
            var d = Definitions.All[Index];
            bool success = true;
            if (d.Number) state.Input(d.Value);
            else if (d.BinaryOperation != 0) { state.Activate(); success = state.Begin(d.BinaryOperation, d.Key); }
            else if (d.Operation != null) success = state.Unary(d.Operation);
            else switch (Index)
            {
                case 3: success = state.Unary(FractionMath.Sqrt); break;
                case 4: success = state.Unary(v => Math.Round(v, 0, MidpointRounding.AwayFromZero)); break;
                case 5: success = state.Unary(decimal.Floor); break;
                case 6: success = state.Unary(decimal.Ceiling); break;
                default: success = false; state.Notify("Операция кнопки не найдена", "Button operation not found"); break;
            }
            if (!success) return;
            var audio = GameObject.Find("Pop"); if (audio && audio.TryGetComponent<AudioSource>(out var source)) source.Play();
            GetComponent<CountUses>().SubUse();
        }
    }
}
