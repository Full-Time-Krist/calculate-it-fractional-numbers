using System;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace CalculateIt.FractionalNumbers
{
    public sealed class FractionPassiveState : MonoBehaviour
    {
        public int LuckyRound = -1;
        public int LuckyDigit;
        public int RewardedRound = -1;
        public void EnsureRound(StartLvl level)
        {
            if (RunSave.Restoring || LuckyRound == level.round) return;
            LuckyRound = level.round;
            LuckyDigit = UnityEngine.Random.Range(1, 10);
        }
    }

    public static class FractionPassives
    {
        public static int BaseIndex = -1;
        public static GameObject[] Templates = Array.Empty<GameObject>();
        private static GameObject holder;
        public static readonly string[] Keys = { "passive_fraction_change", "passive_fraction_whole", "passive_fraction_lucky", "passive_fraction_reserve" };
        static readonly string[] Ru = { "Мелочь", "Почти целое", "Счастливый разряд", "Запас точности" };
        static readonly string[] En = { "Small change", "Almost whole", "Lucky digit", "Precision reserve" };
        static readonly string[] Labels = { ".+$", "≈", ".7", "+1f" };
        public static CalculateIt.ModLoader.CollectionEntry[] Collection()
        {
            string[] ru = { "За каждую видимую цифру после запятой в принятом ответе: +1 монета. Нули в конце не считаются.", "Ответ засчитывается, если его целая часть равна цели. Отбрасывание дроби — к нулю.", "В начале раунда выбирается цифра от 1 до 9. Если последняя дробная цифра принятого ответа совпадает: +5 монет.", "За победу с дробным ответом: +1 заряд случайной установленной кнопке мода дробных чисел." };
            string[] en = { "+1 coin per visible fractional digit in the accepted answer. Trailing zeros do not count.", "Win when the integer part matches the target. Truncate toward zero.", "Each round selects a digit from 1 to 9. +5 coins if the accepted answer's last fractional digit matches.", "A fractional victory adds 1 charge to a random installed Fractional Numbers button." };
            return Enumerable.Range(0,4).Select(i=>new CalculateIt.ModLoader.CollectionEntry {
                Id=Keys[i], Kind=CalculateIt.ModLoader.CollectionKind.Item, Symbol=Labels[i],
                RussianName=Ru[i], EnglishName=En[i], RussianDescription=ru[i], EnglishDescription=en[i]
            }).ToArray();
        }
        public static FractionPassiveState State(StartLvl level) => level.GetComponent<FractionPassiveState>() ?? level.gameObject.AddComponent<FractionPassiveState>();
        public static bool Owned(int index) => GameObject.Find(Keys[index]);
        public static void Setup(StartLvl level, RunSave save)
        {
            var pool = level.passiveshop.GetComponent<PassivePool>();
            if (!pool || pool.passives_objs.Any(p => p && p.name == Keys[0])) return;
            if (holder) UnityEngine.Object.Destroy(holder);
            holder = new GameObject("FractionalNumbers.PassiveTemplates");
            holder.SetActive(false); UnityEngine.Object.DontDestroyOnLoad(holder);
            BaseIndex = pool.passives_objs.Count;
            var field = AccessTools.Field(typeof(RunSave), "templates");
            var saved = (GameObject[])field.GetValue(save);
            Templates = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                var clone = UnityEngine.Object.Instantiate(pool.passives_objs[0], holder.transform, false);
                clone.name = Keys[i]; clone.SetActive(true);
                var info = clone.GetComponent<Infos>(); info.passive = true; info.id = BaseIndex + i + 1;
                var stamp = clone.GetComponent<RunSaveTemplate>() ?? clone.AddComponent<RunSaveTemplate>();
                stamp.id = saved.Length + i;
                var images = clone.GetComponentsInChildren<Image>(true);
                foreach (var image in images.Skip(1)) image.enabled = false;
                Paint(images.FirstOrDefault(), i);
                Describe(info);
                Templates[i] = clone; pool.passives_objs.Add(clone);
            }
            foreach (var offer in pool.passives)
                offer.variations = offer.variations.Concat(Enumerable.Range(0,4).Select(i => new RandomisatorPassives.Pass { sprite = offer.variations[0].sprite, color = new Color(.68f,.76f,1f) })).ToArray();
            field.SetValue(save, saved.Concat(Templates).ToArray());
        }
        static void Paint(Image image, int index)
        {
            if (!image) return;
            var existing = image.transform.Find("FractionBadge");
            Text text;
            if (existing) text = existing.GetComponent<Text>();
            else
            {
                var badge = new GameObject("FractionBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                badge.transform.SetParent(image.transform, false);
                var rect = badge.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                text = badge.GetComponent<Text>();
                text.font = Resources.FindObjectsOfTypeAll<Text>().First(t => t.font).font;
                text.alignment = TextAnchor.MiddleCenter; text.resizeTextForBestFit = true; text.resizeTextMinSize = 10; text.resizeTextMaxSize = 30; text.raycastTarget = false;
            }
            image.sprite = null;
            image.color = new Color(.68f,.76f,1f);
            text.color = new Color(.12f,.16f,.28f); text.text = Labels[index];
        }
        public static bool Describe(Infos info)
        {
            int i = info.id - BaseIndex - 1;
            if (!info.passive || BaseIndex < 0 || i < 0 || i >= 4) return false;
            var level = UnityEngine.Object.FindFirstObjectByType<StartLvl>();
            int digit = level ? State(level).LuckyDigit : 0;
            string[] ru = { "За каждую видимую цифру после запятой в принятом ответе: +1 монета. Нули в конце не считаются.", "Ответ засчитывается, если его целая часть равна цели. Отбрасывание дроби — к нулю.", "Счастливая цифра: " + digit + ". Если последняя дробная цифра принятого ответа совпадает: +5 монет.", "За победу с дробным ответом: +1 заряд случайной установленной кнопке мода дробных чисел." };
            string[] en = { "+1 coin per visible fractional digit in the accepted answer. Trailing zeros do not count.", "Win when the integer part matches the target. Truncate toward zero.", "Lucky digit: " + digit + ". +5 coins if the accepted answer's last fractional digit matches.", "A fractional victory adds 1 charge to a random installed Fractional Numbers button." };
            info.rname = Ru[i]; info.rdescription = ru[i];
            bool russian = GamePrefs.GetString("lang") == "rus";
            info.name = russian ? Ru[i] : En[i]; info.description = russian ? ru[i] : en[i];
            return true;
        }
        public static void Offer(RandomisatorPassives offer)
        {
            int i = offer.id - BaseIndex;
            var badge = offer.childim ? offer.childim.transform.Find("FractionBadge") : null;
            if (badge) badge.gameObject.SetActive(i >= 0 && i < 4);
            if (BaseIndex < 0 || i < 0 || i >= 4) return;
            Paint(offer.childim, i); Describe(offer.GetComponent<Infos>());
        }
        public static void Accepted(MainCalc calc)
        {
            var level = calc.GetComponentInParent<StartLvl>() ?? UnityEngine.Object.FindFirstObjectByType<StartLvl>();
            if (!level || RunSave.Restoring || !calc.can_send || !decimal.TryParse(level.calc.GetComponent<Text>().text, NumberStyles.Float, CultureInfo.InvariantCulture, out var score)) return;
            // Ignore debug wins and skipped rounds; use the same exact-score proxy as the goal check.
            int proxy = GoalPatch.GoalScore(FractionMath.Format(score), level);
            decimal target = FractionGoals.Target(level);
            bool valid = proxy == level.number || (Owned(1) && decimal.Truncate(score) == target)
                || (GameObject.Find("passive_near1") && Math.Abs(score-target)<=1)
                || (GamePrefs.GetInt("challenge")==1 && Math.Abs(score-target)<=StartLvl.MoreLessTolerance);
            if (!valid) return;
            var state = State(level); state.EnsureRound(level);
            if (state.RewardedRound == level.round) return;
            state.RewardedRound = level.round;
            int digits = FractionMath.FractionDigits(score);
            if (digits == 0) return;
            string formatted = FractionMath.Format(Math.Abs(score));
            int coins = (Owned(0) ? digits : 0) + (Owned(2) && formatted[formatted.Length-1]-'0' == state.LuckyDigit ? 5 : 0);
            if (coins > 0) {
                AccessTools.Method(typeof(StartLvl), "AddRoundCoins").Invoke(level, new object[] { coins });
                level.coins_text.text = level.coins.ToString();
            }
            if (Owned(3))
            {
                var buttons = level.calc_transform.GetComponentsInChildren<FractionButton>().Select(b=>b.GetComponent<CountUses>()).Where(b=>b).ToArray();
                if (buttons.Length > 0) buttons[UnityEngine.Random.Range(0,buttons.Length)].AddUses(1);
            }
        }
    }
    [HarmonyPatch(typeof(RandomisatorPassives), "OnEnable")]
    internal static class PassiveOfferPatch
    {
        static void Postfix(RandomisatorPassives __instance) => FractionPassives.Offer(__instance);
    }
    [HarmonyPatch(typeof(StartLvl), "Update")]
    internal static class PassiveRoundPatch
    {
        static void Prefix(StartLvl __instance) => FractionPassives.State(__instance).EnsureRound(__instance);
    }
}
