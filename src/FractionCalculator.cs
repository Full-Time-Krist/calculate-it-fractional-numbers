using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace CalculateIt.FractionalNumbers
{
    public static class FractionCalculator
    {
        // Native types occupy 0..6. The selection is stored in the native run preferences.
        public const int Type = 7;
        public static readonly string[] Keys = {
            "number_fraction_01", "number_fraction_02", "number_fraction_03", "operator_fraction_round",
            "number_fraction_04", "number_fraction_05", "number_fraction_06", "operator_fraction_ceil",
            "number_fraction_07", "number_fraction_08", "number_fraction_09", "operator_fraction_sqrt",
            "operator_fraction_trunc", "number_fraction_025", "operator_send", "operator_fraction_divide"
        };
        public static void Register(CreateCalc creator)
        {
            if (creator.Calculators.Length > Type) return;
            var normal = creator.Calculators[0];
            var templates = Keys.Select(key => Registry.Templates.Concat(normal.calcs).First(p => p.name == key)).ToArray();
            var calculators = creator.Calculators;
            Array.Resize(ref calculators, Type + 1);
            calculators[Type] = new CreateCalc.Objs { skin = normal.skin, calcs = templates };
            creator.Calculators = calculators;
        }
        public static void Menu(MenuManagment menu)
        {
            var donor = Resources.FindObjectsOfTypeAll<Text>().FirstOrDefault(t => t.gameObject.scene == menu.gameObject.scene && t.text == "Normal Calculator");
            if (!donor) return;
            var nativeCard = donor.transform.parent;
            var parent = nativeCard.parent;
            if (parent.Find("FractionCalculator")) return;
            var card = UnityEngine.Object.Instantiate(nativeCard.gameObject, parent, false);
            card.name = "FractionCalculator";
            var rect = card.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(parent.Cast<Transform>().Where(t => t != card.transform && t.name.StartsWith("Calculator")).Max(t => ((RectTransform)t).anchoredPosition.x) + 385, rect.anchoredPosition.y);
            foreach (var text in card.GetComponentsInChildren<Text>(true))
            {
                if (text.transform.parent != card.transform) continue;
                var lang = text.GetComponent<Languiger>();
                if (lang) { text.text = lang.rus ? "Дробный калькулятор" : "Fractional Calculator"; text.gameObject.SetActive(lang.rus == (GamePrefs.GetString("lang") == "rus")); }
                else if (text.name == "display_number") text.text = "0.125";
            }
            var preview = card.GetComponentsInChildren<Place>(true).Where(p => p.gameObject.activeSelf)
                .OrderByDescending(p=>Mathf.Round(((RectTransform)p.transform).anchoredPosition.y/20))
                .ThenBy(p=>((RectTransform)p.transform).anchoredPosition.x).ToArray();
            if (preview.Length != Keys.Length) throw new InvalidOperationException("Unexpected native calculator preview layout: " + preview.Length);
            for (int i=0;i<preview.Length;i++)
            {
                var obj=preview[i].gameObject;
                foreach(var button in obj.GetComponents<Button>()) button.enabled=false;
                var uses=obj.GetComponent<CountUses>(); if(uses) uses.enabled=false;
                int index=Array.FindIndex(Definitions.All,d=>d.Key==Keys[i]);
                if(index<0) continue;
                var definition=Definitions.All[index];
                if (definition.Key == "operator_fraction_trunc" || definition.Key == "operator_fraction_sqrt" || definition.Key == "operator_fraction_divide")
                    obj.transform.Find("left").GetComponent<Text>().text = "10";
                var icon=obj.transform.Find("icon"); if(icon) icon.gameObject.SetActive(false);
                var label=obj.transform.Find("Text").GetComponent<Text>(); label.gameObject.SetActive(true); label.text=definition.Label;
                label.resizeTextForBestFit=true; label.resizeTextMinSize=18; label.resizeTextMaxSize=definition.Number?62:42;
                obj.GetComponent<Image>().color=definition.Number?new Color(.84f,.87f,1):new Color(.69f,.74f,1);
                var info=obj.GetComponent<Infos>(); if(info) info.enabled=false;
            }
            // Clone the native selection button, including its sounds, press animation and navigation.
            var original=parent.Find("select").GetComponent<Button>();
            var select=UnityEngine.Object.Instantiate(original,parent,false);
            select.name="select_fraction";
            select.GetComponent<RectTransform>().anchoredPosition=original.GetComponent<RectTransform>().anchoredPosition + new Vector2(rect.anchoredPosition.x-((RectTransform)nativeCard).anchoredPosition.x,0);
            var calls=AccessTools.Field(typeof(UnityEngine.Events.UnityEventBase),"m_PersistentCalls").GetValue(select.onClick);
            var entries=(IEnumerable)AccessTools.Field(calls.GetType(),"m_Calls").GetValue(calls);
            bool changed=false;
            foreach(var call in entries)
            {
                if ((string)AccessTools.Field(call.GetType(),"m_MethodName").GetValue(call)!="Choose") continue;
                var args=AccessTools.Field(call.GetType(),"m_Arguments").GetValue(call);
                AccessTools.Field(args.GetType(),"m_IntArgument").SetValue(args,Type); changed=true;
            }
            if (!changed) throw new InvalidOperationException("Native calculator selection callback missing");
            AccessTools.Method(typeof(UnityEngine.Events.UnityEventBase),"DirtyPersistentCalls").Invoke(select.onClick,null);
            var content=(RectTransform)parent.parent;
            content.sizeDelta += new Vector2(385,0);
            ((RectTransform)parent).anchoredPosition -= new Vector2(192.5f,0);
        }
    }
    [HarmonyPatch(typeof(MenuManagment), "Start")]
    internal static class CalculatorMenuPatch
    {
        static void Postfix(MenuManagment __instance)
        {
            if (__instance.gameObject.scene.name == "menu") FractionCalculator.Menu(__instance);
        }
    }
    [HarmonyPatch(typeof(CreateCalc), "Start")]
    internal static class CalculatorCreationPatch
    {
        static void Postfix(CreateCalc __instance)
        {
            if (__instance.gameObject.scene.name != "main" || GamePrefs.GetInt("Type") != FractionCalculator.Type || RunSave.Restoring) return;
            foreach (var marker in __instance.calc_transform.GetComponentsInChildren<FractionButton>())
            {
                string key = Definitions.All[marker.Index].Key;
                if (key == "operator_fraction_trunc" || key == "operator_fraction_divide" || key == "operator_fraction_sqrt")
                    marker.GetComponent<CountUses>().uses = 10;
            }
        }
        static void Prefix(CreateCalc __instance)
        {
            if (__instance.gameObject.scene.name != "main") return;
            var level=UnityEngine.Object.FindFirstObjectByType<StartLvl>();
            Registry.Setup(level,RunSave.Instance);
            FractionCalculator.Register(__instance);
        }
    }
}
