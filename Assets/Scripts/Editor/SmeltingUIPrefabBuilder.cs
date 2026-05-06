using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace LingoteRush.Editor
{
    public static class SmeltingUIPrefabBuilder
    {
        private const string UiRootFolder = "Assets/UI/Smelting";
        private const string PrefabsFolder = "Assets/UI/Smelting/Prefabs";
        private const string CanvasPrefabPath = "Assets/UI/Smelting/Prefabs/UI_SmeltingCanvas.prefab";
        private const string EventSystemPrefabPath = "Assets/UI/Smelting/Prefabs/UI_SmeltingEventSystem.prefab";

        private static Font cachedFont;

        [MenuItem("Lingote Rush/UI/Rebuild Smelting UI Prefabs")]
        public static void BuildPrefabsFromMenu()
        {
            BuildAll();
        }

        public static void BuildAll()
        {
            EnsureFolders();
            BuildCanvasPrefab();
            BuildEventSystemPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Smelting UI prefabs rebuilt at '{PrefabsFolder}'.");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/UI"))
            {
                AssetDatabase.CreateFolder("Assets", "UI");
            }

            if (!AssetDatabase.IsValidFolder(UiRootFolder))
            {
                AssetDatabase.CreateFolder("Assets/UI", "Smelting");
            }

            if (!AssetDatabase.IsValidFolder(PrefabsFolder))
            {
                AssetDatabase.CreateFolder(UiRootFolder, "Prefabs");
            }
        }

        private static void BuildCanvasPrefab()
        {
            var rootObject = new GameObject("UI_SmeltingCanvas", typeof(RectTransform));
            var canvas = rootObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            canvas.sortingOrder = 100;

            var scaler = rootObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            rootObject.AddComponent<GraphicRaycaster>();

            var rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var heatBarBackground = EnsurePanel(
                rootRect,
                "HeatBarBG",
                new Vector2(24f, -24f),
                new Vector2(320f, 26f),
                new Color(0.12f, 0.12f, 0.12f, 0.92f));

            EnsureFillImage(heatBarBackground, "HeatBarFill", new Color(1f, 0.48f, 0.12f, 1f));

            var micBarBackground = EnsurePanel(
                rootRect,
                "MicLevelBG",
                new Vector2(24f, -60f),
                new Vector2(320f, 26f),
                new Color(0.12f, 0.12f, 0.12f, 0.92f));

            EnsureFillImage(micBarBackground, "MicLevelFill", new Color(0.25f, 0.85f, 1f, 1f));

            EnsureStatusText(rootRect);
            EnsureDropdown(rootRect);
            EnsureSlider(rootRect);

            SavePrefab(rootObject, CanvasPrefabPath);
        }

        private static void BuildEventSystemPrefab()
        {
            var rootObject = new GameObject("UI_SmeltingEventSystem", typeof(EventSystem));

#if ENABLE_INPUT_SYSTEM
            rootObject.AddComponent<InputSystemUIInputModule>();
#else
            rootObject.AddComponent<StandaloneInputModule>();
#endif

            SavePrefab(rootObject, EventSystemPrefabPath);
        }

        private static void SavePrefab(GameObject rootObject, string prefabPath)
        {
            PrefabUtility.SaveAsPrefabAsset(rootObject, prefabPath);
            Object.DestroyImmediate(rootObject);
        }

        private static RectTransform EnsurePanel(
            RectTransform parent,
            string objectName,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
        {
            var panelRect = CreateRectTransform(parent, objectName);
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = anchoredPosition;
            panelRect.sizeDelta = size;

            var panelImage = panelRect.gameObject.AddComponent<Image>();
            panelImage.color = color;
            return panelRect;
        }

        private static Image EnsureFillImage(RectTransform background, string objectName, Color color)
        {
            var fillRect = CreateRectTransform(background, objectName);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.pivot = new Vector2(0.5f, 0.5f);
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);

            var fillImage = fillRect.gameObject.AddComponent<Image>();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = 0f;
            fillImage.color = color;
            return fillImage;
        }

        private static Text EnsureStatusText(RectTransform parent)
        {
            var textRect = CreateRectTransform(parent, "StatusText");
            textRect.anchorMin = new Vector2(0.5f, 1f);
            textRect.anchorMax = new Vector2(0.5f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.anchoredPosition = new Vector2(0f, -104f);
            textRect.sizeDelta = new Vector2(900f, 34f);

            var statusText = textRect.gameObject.AddComponent<Text>();
            statusText.font = GetFont();
            statusText.fontSize = 24;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.horizontalOverflow = HorizontalWrapMode.Overflow;
            statusText.verticalOverflow = VerticalWrapMode.Overflow;
            statusText.color = Color.white;
            statusText.text = "Waiting for microphone";
            return statusText;
        }

        private static Dropdown EnsureDropdown(RectTransform parent)
        {
            var dropdownRect = CreateRectTransform(parent, "DeviceDropdown");
            dropdownRect.anchorMin = new Vector2(1f, 1f);
            dropdownRect.anchorMax = new Vector2(1f, 1f);
            dropdownRect.pivot = new Vector2(1f, 1f);
            dropdownRect.anchoredPosition = new Vector2(-24f, -24f);
            dropdownRect.sizeDelta = new Vector2(320f, 34f);

            var dropdownImage = dropdownRect.gameObject.AddComponent<Image>();
            dropdownImage.color = new Color(0.95f, 0.95f, 0.95f, 0.96f);

            var dropdown = dropdownRect.gameObject.AddComponent<Dropdown>();
            dropdown.targetGraphic = dropdownImage;

            var label = EnsureText(
                dropdownRect,
                "Label",
                new Vector2(10f, 2f),
                new Vector2(-30f, -2f),
                18,
                TextAnchor.MiddleLeft,
                new Color(0.1f, 0.1f, 0.1f, 1f),
                "Microphone");

            var arrowRect = CreateRectTransform(dropdownRect, "Arrow");
            arrowRect.anchorMin = new Vector2(1f, 0f);
            arrowRect.anchorMax = new Vector2(1f, 1f);
            arrowRect.pivot = new Vector2(1f, 0.5f);
            arrowRect.anchoredPosition = new Vector2(-8f, 0f);
            arrowRect.sizeDelta = new Vector2(18f, 0f);

            var arrowText = arrowRect.gameObject.AddComponent<Text>();
            arrowText.font = GetFont();
            arrowText.fontSize = 18;
            arrowText.alignment = TextAnchor.MiddleCenter;
            arrowText.horizontalOverflow = HorizontalWrapMode.Overflow;
            arrowText.verticalOverflow = VerticalWrapMode.Overflow;
            arrowText.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            arrowText.text = "v";

            var templateRect = CreateRectTransform(dropdownRect, "Template");
            templateRect.anchorMin = new Vector2(0f, 1f);
            templateRect.anchorMax = new Vector2(1f, 1f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, -36f);
            templateRect.sizeDelta = new Vector2(0f, 180f);

            var templateImage = templateRect.gameObject.AddComponent<Image>();
            templateImage.color = new Color(0.96f, 0.96f, 0.96f, 0.98f);

            var scrollRect = templateRect.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;

            var viewportRect = CreateRectTransform(templateRect, "Viewport");
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.offsetMin = new Vector2(4f, 4f);
            viewportRect.offsetMax = new Vector2(-4f, -4f);

            var viewportImage = viewportRect.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.02f);

            var viewportMask = viewportRect.gameObject.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            var contentRect = CreateRectTransform(viewportRect, "Content");
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 28f);

            var layoutGroup = contentRect.gameObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.childControlHeight = true;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.spacing = 2f;
            layoutGroup.padding = new RectOffset(2, 2, 2, 2);

            var contentSizeFitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var itemRect = CreateRectTransform(contentRect, "Item");
            itemRect.anchorMin = new Vector2(0f, 1f);
            itemRect.anchorMax = new Vector2(1f, 1f);
            itemRect.pivot = new Vector2(0.5f, 1f);
            itemRect.sizeDelta = new Vector2(0f, 24f);

            var itemImage = itemRect.gameObject.AddComponent<Image>();
            itemImage.color = new Color(1f, 1f, 1f, 0.95f);

            var itemToggle = itemRect.gameObject.AddComponent<Toggle>();
            itemToggle.targetGraphic = itemImage;

            var checkmarkRect = CreateRectTransform(itemRect, "Item Checkmark");
            checkmarkRect.anchorMin = new Vector2(0f, 0.5f);
            checkmarkRect.anchorMax = new Vector2(0f, 0.5f);
            checkmarkRect.pivot = new Vector2(0.5f, 0.5f);
            checkmarkRect.anchoredPosition = new Vector2(12f, 0f);
            checkmarkRect.sizeDelta = new Vector2(16f, 16f);

            var checkmarkImage = checkmarkRect.gameObject.AddComponent<Image>();
            checkmarkImage.color = new Color(0.22f, 0.62f, 0.18f, 1f);
            itemToggle.graphic = checkmarkImage;

            var itemLabel = EnsureText(
                itemRect,
                "Item Label",
                new Vector2(28f, 1f),
                new Vector2(-8f, -1f),
                16,
                TextAnchor.MiddleLeft,
                new Color(0.12f, 0.12f, 0.12f, 1f),
                "Device");

            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            dropdown.template = templateRect;
            dropdown.captionText = label;
            dropdown.itemText = itemLabel;
            dropdown.options.Add(new Dropdown.OptionData("Microphone"));
            dropdown.RefreshShownValue();
            templateRect.gameObject.SetActive(false);
            return dropdown;
        }

        private static Slider EnsureSlider(RectTransform parent)
        {
            var sliderRect = CreateRectTransform(parent, "SensitivitySlider");
            sliderRect.anchorMin = new Vector2(1f, 1f);
            sliderRect.anchorMax = new Vector2(1f, 1f);
            sliderRect.pivot = new Vector2(1f, 1f);
            sliderRect.anchoredPosition = new Vector2(-24f, -72f);
            sliderRect.sizeDelta = new Vector2(320f, 24f);

            var slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0.1f;
            slider.maxValue = 4f;
            slider.wholeNumbers = false;
            slider.value = 1.25f;

            var backgroundRect = CreateRectTransform(sliderRect, "Background");
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(1f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.offsetMin = new Vector2(0f, -8f);
            backgroundRect.offsetMax = new Vector2(0f, 8f);

            var backgroundImage = backgroundRect.gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.14f, 0.14f, 0.14f, 0.94f);

            var fillAreaRect = CreateRectTransform(sliderRect, "Fill Area");
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(10f, 5f);
            fillAreaRect.offsetMax = new Vector2(-10f, -5f);

            var fillRect = CreateRectTransform(fillAreaRect, "Fill");
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var fillImage = fillRect.gameObject.AddComponent<Image>();
            fillImage.color = new Color(0.32f, 0.84f, 0.26f, 1f);

            var handleAreaRect = CreateRectTransform(sliderRect, "Handle Slide Area");
            handleAreaRect.anchorMin = new Vector2(0f, 0f);
            handleAreaRect.anchorMax = new Vector2(1f, 1f);
            handleAreaRect.offsetMin = new Vector2(10f, 0f);
            handleAreaRect.offsetMax = new Vector2(-10f, 0f);

            var handleRect = CreateRectTransform(handleAreaRect, "Handle");
            handleRect.anchorMin = new Vector2(0f, 0.5f);
            handleRect.anchorMax = new Vector2(0f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(18f, 28f);

            var handleImage = handleRect.gameObject.AddComponent<Image>();
            handleImage.color = Color.white;

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            return slider;
        }

        private static Text EnsureText(
            RectTransform parent,
            string objectName,
            Vector2 offsetMin,
            Vector2 offsetMax,
            int fontSize,
            TextAnchor alignment,
            Color color,
            string textValue)
        {
            var textRect = CreateRectTransform(parent, objectName);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = offsetMin;
            textRect.offsetMax = offsetMax;

            var text = textRect.gameObject.AddComponent<Text>();
            text.font = GetFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = color;
            text.text = textValue;
            return text;
        }

        private static RectTransform CreateRectTransform(Transform parent, string objectName)
        {
            var childObject = new GameObject(objectName, typeof(RectTransform));
            childObject.transform.SetParent(parent, false);
            return childObject.GetComponent<RectTransform>();
        }

        private static Font GetFont()
        {
            if (cachedFont == null)
            {
                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return cachedFont;
        }
    }
}
