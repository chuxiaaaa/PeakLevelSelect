using BepInEx.Configuration;
using BepInEx.Logging;

using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

using Zorro.Core;

using static LocalizedText;


namespace PeakLevelSelect
{
    [BepInEx.BepInPlugin("PeakLevelSelect", "PeakLevelSelect", "1.0.2")]
    public class PeakLevelSelectPlugin : BepInEx.BaseUnityPlugin
    {
        internal static ManualLogSource logger = null;

        public static ConfigEntry<int> SelectedLevel { get; set; }
        public static ConfigEntry<int> SelectedAscent { get; set; }

        void Awake()
        {
            logger = base.Logger;
            SelectedLevel = Config.Bind<int>("General", "SelectedLevel", -2, "-2 = Random,-1 = Daily,0-14=Level_Num");
            SelectedAscent = Config.Bind<int>("General", "SelectedAscent", -2, "-2 = Default,-1-7 = AscentNum");
            Harmony.CreateAndPatchAll(typeof(GUIManagerPatch), null);
        }
    }

    [HarmonyWrapSafe]
    public static class GUIManagerPatch
    {
        private static Dictionary<string, List<string>> langTable { get; set; }

        [HarmonyPatch(typeof(BoardingPass), "UpdateAscent")]
        [HarmonyPostfix]
        public static void UpdateAscent(BoardingPass __instance)
        {
            PeakLevelSelectPlugin.SelectedAscent.Value = __instance.ascentIndex;
        }

        [HarmonyPatch(typeof(MapBaker), "GetLevel")]
        [HarmonyPrefix]
        public static void GetLevel(ref int levelIndex)
        {
            if (PeakLevelSelectPlugin.SelectedLevel.Value == -2)
            {
                levelIndex = UnityEngine.Random.Range(0, SingletonAsset<MapBaker>.Instance.AllLevels.Length);
            }
            else if (PeakLevelSelectPlugin.SelectedLevel.Value == -1)
            {
                levelIndex = todayLevelIndex;
            }
            else if (PeakLevelSelectPlugin.SelectedLevel.Value < SingletonAsset<MapBaker>.Instance.AllLevels.Length)
            {
                levelIndex = PeakLevelSelectPlugin.SelectedLevel.Value;
            }
        }

        [HarmonyPatch(typeof(GUIManager), "Start")]
        [HarmonyPostfix]
        public static void Start()
        {
            langTable = new Dictionary<string, List<string>>();
            langTable.Add("Random", MakeList((Language.English, "Random"), (Language.SimplifiedChinese, "随机地图")));
            langTable.Add("Daily", MakeList((Language.English, "Daily"), (Language.SimplifiedChinese, "当天地图")));
            langTable.Add("Level", MakeList((Language.English, "Level_{0}"), (Language.SimplifiedChinese, "轮换{0}")));
            langTable.Add("Today", MakeList((Language.English, "Today"), (Language.SimplifiedChinese, "今日地图")));
            NextLevelService service = GameHandler.GetService<NextLevelService>();
            if (service != null && service.Data.IsSome)
            {
                int levelIndex = service.Data.Value.CurrentLevelIndex + NextLevelService.debugLevelIndexOffset;
                levelIndex %= SingletonAsset<MapBaker>.Instance.AllLevels.Length;
                todayLevelIndex = levelIndex;
            }
            else
            {
                todayLevelIndex = -3;
            }
            buttons = new List<GameObject>();
            var Canvas_BoardingPass = GameObject.Find("GAME/GUIManager/Canvas_BoardingPass");
            var boardingPass = Canvas_BoardingPass.GetComponent<BoardingPass>();

            GameObject referenceButton = GameObject.Find("GAME/GUIManager/Canvas_BoardingPass/BoardingPass/Panel/Ascent/IncrementButton");
            if (referenceButton == null)
            {
                Debug.LogError("find IncrementButton fail!");
                return;
            }
            Transform panel = GameObject.Find("GAME/GUIManager/Canvas_BoardingPass/BoardingPass/Panel").transform;
            if (panel == null)
            {
                Debug.LogError("fail Panel fail!");
                return;
            }
            To1 = GameObject.Find("GAME/GUIManager/Canvas_BoardingPass/BoardingPass/Panel/To (1)").GetComponent<TextMeshProUGUI>();
            var referenceFont = GameObject.Find("GAME/GUIManager/Canvas_BoardingPass/BoardingPass/Panel/BOARDING PASS");
            font = referenceFont.GetComponent<TMPro.TextMeshProUGUI>().font;
            GameObject button1 = CreateButton(referenceButton, panel, GetText("Random"), new Vector2(140, 75), (sender) =>
            {
                RandomInvoke();
            });
            buttons.Add(button1);
            float buttonWidth = button1.GetComponent<RectTransform>().sizeDelta.x;
            var DailyText = GetText("Daily");
           
            if (todayLevelIndex != -3)
            {
                DailyText += $"({todayLevelIndex})";
            }
            GameObject button2 = CreateButton(referenceButton, panel, DailyText, new Vector2(140 + buttonWidth + 20, 75), (sender) =>
            {
                DailyInvoke();
            });
            buttons.Add(button2);

            var Reward = GameObject.Find("GAME/GUIManager/Canvas_BoardingPass/BoardingPass/Panel/Ascent/Reward");
            if (Reward != null)
            {
                var RewardRect = Reward.GetComponent<RectTransform>();
                RewardRect.anchoredPosition = new Vector2(RewardRect.anchoredPosition.x, -10);
            }
            PeakLevelSelectPlugin.logger.LogInfo("Create Button Sussecs!");
            CreateDropdown(panel);
            if (PeakLevelSelectPlugin.SelectedAscent.Value != -2)
            {
                boardingPass.ascentIndex = PeakLevelSelectPlugin.SelectedAscent.Value;
                boardingPass.UpdateAscent();
            }
        }

        private static void RandomInvoke()
        {
            dropDownText.text = GetText("Random");
            PeakLevelSelectPlugin.SelectedLevel.Value = -2;
            buttons[0].GetComponent<Image>().color = new Color(0.9804f, 0.8075f, 0.1922f, 1);
            To1.text = "???";
        }

        private static void DailyInvoke()
        {
            dropDownText.text = GetText("Daily");
            var map = SingletonAsset<MapBaker>.Instance;
            if (todayLevelIndex != -3 && todayLevelIndex < map.selectedBiomes.Count)
            {
                To1.text = todayLevelIndex.ToString();
                dropDownText.text += $"({string.Join(",", map.selectedBiomes[todayLevelIndex].selectedBiomes.Where(x => x != Biome.BiomeType.Shore && x != Biome.BiomeType.Volcano).Select(x => LocalizedText.GetText(x.ToString())))})";
            }
            else
            {
                To1.text = "0";
            }
            PeakLevelSelectPlugin.SelectedLevel.Value = -1;
            buttons[1].GetComponent<Image>().color = new Color(0.9804f, 0.8075f, 0.1922f, 1);
        }

        private static int todayLevelIndex;

        private static TMP_FontAsset font;

        private static Image image;

        private static List<GameObject> buttons { get; set; }

        private static TextMeshProUGUI dropDownText;

        private static TextMeshProUGUI To1;

        private static Image dropdownImage;

        private static List<string> MakeList(params (LocalizedText.Language lang, string text)[] items)
        {
            var list = new List<string>(new string[LocalizedText.LANGUAGE_COUNT]);

            foreach (var (lang, text) in items)
            {
                list[(int)lang] = text;
            }
            for (int i = 0; i < LocalizedText.LANGUAGE_COUNT; i++)
            {
                if (string.IsNullOrEmpty(list[i]))
                    list[i] = list[(int)LocalizedText.Language.English];
            }
            return list;
        }

        private static void CreateDropdown(Transform panel)
        {
            try
            {
                GameObject dropdownGO = new GameObject("CustomDropdown");
                dropdownGO.transform.SetParent(panel, false);

                RectTransform dropdownRect = dropdownGO.AddComponent<RectTransform>();
                dropdownRect.anchorMin = new Vector2(0, 0);
                dropdownRect.anchorMax = new Vector2(0, 0);
                dropdownRect.pivot = new Vector2(0, 0);
                dropdownRect.anchoredPosition = new Vector2(520, 75);
                dropdownRect.sizeDelta = new Vector2(0, 65);

                var backgroundImage = dropdownGO.AddComponent<Image>();
                backgroundImage.sprite = image.sprite;
                backgroundImage.color = image.color;
                backgroundImage.type = image.type;
                backgroundImage.pixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier;
                backgroundImage.material = image.material;
                backgroundImage.overrideSprite = image.overrideSprite;
                backgroundImage.raycastTarget = image.raycastTarget;
                dropdownImage = backgroundImage;

                GameObject labelGO = new GameObject("Label");
                labelGO.transform.SetParent(dropdownGO.transform, false);

                var label = labelGO.AddComponent<TextMeshProUGUI>();
                label.fontSize = 14;
                label.font = font;
                label.color = Color.white;
                label.text = "";
                label.alignment = TextAlignmentOptions.Left;
                dropDownText = label;
                RectTransform labelRect = label.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(10, 6);
                labelRect.offsetMax = new Vector2(-30, -7);


                GameObject arrowGO = new GameObject("Arrow");
                arrowGO.transform.SetParent(dropdownGO.transform, false);

                var arrow = arrowGO.AddComponent<TextMeshProUGUI>();
                arrow.font = font;
                arrow.color = Color.white;
                arrow.text = "▼";
                arrow.fontSize = 24;
                arrow.alignment = TextAlignmentOptions.Center;

                RectTransform arrowRect = arrow.GetComponent<RectTransform>();
                arrowRect.anchorMin = new Vector2(1, 0.5f);
                arrowRect.anchorMax = new Vector2(1, 0.5f);
                arrowRect.pivot = new Vector2(1, 0.5f);
                arrowRect.sizeDelta = new Vector2(20, 20);
                arrowRect.anchoredPosition = new Vector2(-20, 0);

                GameObject templateGO = new GameObject("Template");
                templateGO.transform.SetParent(dropdownGO.transform, false);
                templateGO.SetActive(false);

                var map = SingletonAsset<MapBaker>.Instance;

                RectTransform templateRect = templateGO.AddComponent<RectTransform>();
                templateRect.anchorMin = new Vector2(0, 0);
                templateRect.anchorMax = new Vector2(1, 0);
                templateRect.pivot = new Vector2(0.5f, 1);
                PeakLevelSelectPlugin.logger.LogInfo($"AllLevels: {map.AllLevels.Length}");
                templateRect.sizeDelta = new Vector2(0, map.AllLevels.Length * 25);


                var templateImage = templateGO.AddComponent<Image>();
                templateImage.color = new Color(0.2f, 0.2f, 0.2f);
                templateGO.AddComponent<RectMask2D>();


                GameObject viewportGO = new GameObject("Viewport");
                viewportGO.transform.SetParent(templateGO.transform, false);

                RectTransform viewportRect = viewportGO.AddComponent<RectTransform>();
                viewportRect.anchorMin = Vector2.zero;
                viewportRect.anchorMax = Vector2.one;
                viewportRect.sizeDelta = Vector2.zero;

                var viewportImage = viewportGO.AddComponent<Image>();
                viewportImage.color = Color.clear;
                viewportImage.raycastTarget = false;


                GameObject contentGO = new GameObject("Content");
                contentGO.transform.SetParent(viewportGO.transform, false);

                RectTransform contentRect = contentGO.AddComponent<RectTransform>();
                contentRect.anchorMin = new Vector2(0, 1);
                contentRect.anchorMax = new Vector2(1, 1);
                contentRect.pivot = new Vector2(0.5f, 1);
                contentRect.anchoredPosition = Vector2.zero;
                contentRect.sizeDelta = new Vector2(0, 0);

                GameObject itemGO = new GameObject("Item");
                itemGO.transform.SetParent(contentGO.transform, false);

                Toggle itemToggle = itemGO.AddComponent<Toggle>();
                RectTransform itemRect = itemGO.GetComponent<RectTransform>();
                itemRect.anchorMin = new Vector2(0, 1);
                itemRect.anchorMax = new Vector2(1, 1);
                itemRect.pivot = new Vector2(0.5f, 1);
                itemRect.sizeDelta = new Vector2(0, 25);


                GameObject itemBG = new GameObject("Item Background");
                itemBG.transform.SetParent(itemGO.transform, false);
                var itemBGImage = itemBG.AddComponent<Image>();
                itemBGImage.color = new Color(0.3f, 0.3f, 0.3f);

                RectTransform itemBGRect = itemBG.GetComponent<RectTransform>();
                itemBGRect.anchorMin = Vector2.zero;
                itemBGRect.anchorMax = Vector2.one;
                itemBGRect.offsetMin = Vector2.zero;
                itemBGRect.offsetMax = Vector2.zero;

                itemToggle.targetGraphic = itemBGImage;

                GameObject checkGO = new GameObject("Item Checkmark");
                checkGO.transform.SetParent(itemBG.transform, false);
                var checkImg = checkGO.AddComponent<Image>();
                checkImg.color = Color.white;

                RectTransform checkRect = checkGO.GetComponent<RectTransform>();
                checkRect.anchorMin = new Vector2(0, 0.5f);
                checkRect.anchorMax = new Vector2(0, 0.5f);
                checkRect.pivot = new Vector2(0, 0.5f);
                checkRect.sizeDelta = new Vector2(20, 20);
                checkRect.anchoredPosition = new Vector2(10, 0);

                itemToggle.graphic = checkImg;


                GameObject itemLabelGO = new GameObject("Item Label");
                itemLabelGO.transform.SetParent(itemGO.transform, false);

                TextMeshProUGUI itemLabel = itemLabelGO.AddComponent<TextMeshProUGUI>();
                itemLabel.font = font;
                itemLabel.fontSize = 14;
                itemLabel.color = Color.white;
                itemLabel.text = "Option";

                RectTransform itemLabelRect = itemLabel.GetComponent<RectTransform>();
                itemLabelRect.anchorMin = new Vector2(0, 0);
                itemLabelRect.anchorMax = new Vector2(1, 1);
                itemLabelRect.offsetMin = new Vector2(30, 0);
                itemLabelRect.offsetMax = new Vector2(-10, 0);

                var dropdown = dropdownGO.AddComponent<TMP_Dropdown>();
                dropdown.targetGraphic = backgroundImage;
                dropdown.captionText = label;
                dropdown.template = templateRect;

                dropdown.itemText = itemLabel;
                dropdown.itemImage = checkImg;

                dropdown.options.Clear();
                float maxWidth = 0f;
                var Canvas_BoardingPass = GameObject.Find("GAME/GUIManager/Canvas_BoardingPass");
                Canvas_BoardingPass.SetActive(true);
                Canvas_BoardingPass.SetActive(false);

                for (int i = 0; i < map.selectedBiomes.Count; i++)
                {
                    string text = $"{GetText("Level", i)}({string.Join(",", map.selectedBiomes[i].selectedBiomes.Where(x => x != Biome.BiomeType.Shore && x != Biome.BiomeType.Volcano).Select(x => LocalizedText.GetText(x.ToString())))})";
                    if (i == todayLevelIndex)
                    {
                        text = $"({GetText("Today")}){text}";
                    }
                    Vector2 size = dropDownText.GetPreferredValues(text);
                    if (size.x > maxWidth)
                    {
                        maxWidth = size.x;
                    }
                    dropdown.options.Add(new TMP_Dropdown.OptionData(text));
                }
                dropdownRect.sizeDelta = new Vector2(maxWidth + 80, dropdownRect.sizeDelta.y);
                float itemHeight = 25f;
                float maxVisibleCount = 8;
                float templateHeight = Mathf.Min(map.AllLevels.Length * itemHeight, maxVisibleCount * itemHeight);
                templateRect.sizeDelta = new Vector2(0, templateHeight);
                var scrollRect = templateGO.AddComponent<ScrollRect>();
                scrollRect.content = contentRect;
                scrollRect.viewport = viewportRect;
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.verticalScrollbar = null;
                scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
                scrollRect.inertia = true;

                if (viewportGO.GetComponent<RectMask2D>() == null)
                    viewportGO.AddComponent<RectMask2D>();
                contentRect.sizeDelta = new Vector2(0, map.AllLevels.Length * itemHeight);
                dropdown.onValueChanged.AddListener((value) =>
                {
                    To1.text = value.ToString();
                    PeakLevelSelectPlugin.SelectedLevel.Value = value;
                    foreach (var item in buttons)
                    {
                        item.GetComponent<Image>().color = new Color(0.1922f, 0.2941f, 0.9804f, 1);
                    }
                    dropdownImage.color = new Color(0.9804f, 0.8075f, 0.1922f, 1);
                });
                if (PeakLevelSelectPlugin.SelectedLevel.Value == -1)
                {
                    DailyInvoke();
                }
                else if (PeakLevelSelectPlugin.SelectedLevel.Value == -2)
                {
                    RandomInvoke();
                }
                else
                {
                    if (PeakLevelSelectPlugin.SelectedLevel.Value < dropdown.options.Count)
                    {
                        dropdown.value = PeakLevelSelectPlugin.SelectedLevel.Value;
                        To1.text = dropdown.value.ToString();
                        dropdown.RefreshShownValue();
                        dropdownImage.color = new Color(0.9804f, 0.8075f, 0.1922f, 1);
                    }
                }
            }
            catch (Exception e)
            {
                PeakLevelSelectPlugin.logger.LogError("CreateDropdown Exception: " + e);
            }
        }

        public static string GetText(string key, params object[] args)
        {

            if (!langTable.TryGetValue(key, out var list))
                return args.Length > 0 ? string.Format(key, args) : key;

            string text = list[(int)CURRENT_LANGUAGE];

            return args.Length > 0 ? string.Format(text, args) : text;
        }


        private static GameObject CreateButton(GameObject referenceButton, Transform parent, string buttonText, Vector2 position, UnityEngine.Events.UnityAction<GameObject> onClick)
        {

            GameObject button = GameObject.Instantiate(referenceButton);
            button.name = "Button_" + buttonText;
            button.transform.SetParent(parent, false);
            GameObject.Destroy(button.transform.Find("Image").gameObject);
            GameObject.Destroy(button.transform.GetComponent<Animator>());
            if (image == null)
            {
                image = button.GetComponent<Image>();
            }

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(170, rect.sizeDelta.y);
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 0);
            rect.pivot = new Vector2(0, 0);
            rect.anchoredPosition = position;


            CreateButtonText(button, buttonText);


            Button buttonComp = button.GetComponent<Button>();
            buttonComp.onClick.RemoveAllListeners();
            buttonComp.onClick.AddListener(() =>
            {
                foreach (var item in buttons)
                {
                    dropdownImage.color = new Color(0.1922f, 0.2941f, 0.9804f, 1);
                    if (item == button)
                    {
                        item.GetComponent<Image>().color = new Color(0.9804f, 0.8075f, 0.1922f, 1);
                    }
                    else
                    {
                        item.GetComponent<Image>().color = new Color(0.1922f, 0.2941f, 0.9804f, 1);
                    }
                }
                onClick(button);
            });
            return button;
        }

        private static void CreateButtonText(GameObject button, string text)
        {
            GameObject textGO = new GameObject("Text (TMP)");
            textGO.transform.SetParent(button.transform, false);

            RectTransform textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TMPro.TextMeshProUGUI textComp = textGO.AddComponent<TMPro.TextMeshProUGUI>();
            textComp.text = text;
            textComp.font = font;
            textComp.color = Color.white;
            textComp.fontStyle = TMPro.FontStyles.Normal;
            textComp.alignment = TMPro.TextAlignmentOptions.Center;
            textComp.autoSizeTextContainer = true;
            textComp.fontSize = 24;
        }
    }
}
