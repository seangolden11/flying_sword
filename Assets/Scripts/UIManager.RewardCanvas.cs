using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed partial class UIManager
{
    Canvas rewardCanvas;
    RectTransform rewardPanel;
    int drawnSelection = -1, drawnWidth, drawnHeight;

    void Update()
    {
        var stage = GetComponent<StageManager>();
        if (stage != null && stage.Actors != null) UpdateRewardCanvas(stage.Actors);
    }
    void UpdateRewardCanvas(TurnActors actors)
    {
        if (!actors.IsChoosingReward)
        {
            if (rewardCanvas != null) rewardCanvas.gameObject.SetActive(false);
            drawnSelection = -1;
            return;
        }
        EnsureRewardCanvas();
        rewardCanvas.gameObject.SetActive(true);
        if (drawnSelection == actors.RewardSelectionId && drawnWidth == Screen.width && drawnHeight == Screen.height) return;
        drawnSelection = actors.RewardSelectionId;
        drawnWidth = Screen.width; drawnHeight = Screen.height;
        if (rewardPanel != null)
        {
            rewardPanel.gameObject.SetActive(false);
            Destroy(rewardPanel.gameObject);
        }
        rewardPanel = Element<RectTransform>(rewardCanvas.transform, "Reward panel");
        Anchors(rewardPanel, new Vector2(.04f, .06f), new Vector2(.96f, .94f), Vector2.zero, Vector2.zero);
        var title = TextElement(rewardPanel, "LEVEL UP", 32, KeyColor);
        Anchors(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -55), Vector2.zero);
        string caption = actors.IsChoosingWeapon ? actors.TargetRewardName + " · 대상 무기 선택" : "보상을 하나 선택하세요";
        if (actors.PendingRewardCount > 1) caption += $" · 남은 선택 {actors.PendingRewardCount}회";
        var info = TextElement(rewardPanel, caption, 18, Text);
        Anchors(info.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -95), new Vector2(0, -55));
        int token = actors.RewardSelectionId;
        bool choosingWeapon = actors.IsChoosingWeapon;
        var action = ButtonElement(rewardPanel, choosingWeapon ? "뒤로 가기" : actors.CanReroll ? "리롤 · 1회 남음" : "리롤 사용 완료");
        Anchors(action.GetComponent<RectTransform>(), new Vector2(.25f, 1), new Vector2(.75f, 1), new Vector2(0, -145), new Vector2(0, -105));
        action.interactable = choosingWeapon || actors.CanReroll;
        action.onClick.AddListener(() => {
            if (choosingWeapon) actors.BackToRewards(token); else actors.RerollRewards(token);
        });
        if (!choosingWeapon) AddIcon(action.transform, GameArt.Get("reroll"), new Vector2(.05f, .15f), new Vector2(.15f, .85f));

        var scroll = Element<ScrollRect>(rewardPanel, "Reward scroll");
        Anchors(scroll.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -165));
        var viewport = Element<Image>(scroll.transform, "Viewport");
        Anchors(viewport.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewport.color = new Color(0, 0, 0, .01f);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var content = Element<RectTransform>(viewport.transform, "Cards");
        Anchors(content, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
        content.pivot = new Vector2(.5f, 1);
        scroll.viewport = viewport.rectTransform; scroll.content = content;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 25;
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Screen.width < 650 ? 1 : 3;
        grid.spacing = new Vector2(12, 12);
        grid.padding = new RectOffset(4, 4, 4, 4);
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Canvas.ForceUpdateCanvases();
        grid.cellSize = new Vector2(Mathf.Max(100, (viewport.rectTransform.rect.width - 8 - 12 * (grid.constraintCount - 1)) / grid.constraintCount), 240);
        int count = choosingWeapon ? actors.WeaponOptionCount : actors.RewardOptionCount;
        for (int i = 0; i < count; i++)
        {
            int index = i;
            var card = ButtonElement(content, "");
            Sprite sprite;
            string text;
            if (choosingWeapon)
            {
                sprite = actors.GetWeaponIcon(i);
                text = actors.GetWeaponDescription(i);
                card.interactable = actors.CanChooseWeapon(i);
                int weaponId = actors.GetWeaponId(i);
                card.onClick.AddListener(() => actors.ChooseWeapon(weaponId, token));
            }
            else
            {
                var reward = actors.GetRewardOption(i);
                sprite = reward.icon;
                text = reward.name + "\n\n" + reward.description;
                card.onClick.AddListener(() => actors.ChooseReward(index, token));
            }
            AddIcon(card.transform, sprite, new Vector2(.32f, .6f), new Vector2(.68f, .96f));
            var label = TextElement(card.transform, text, 17, Text);
            Anchors(label.rectTransform, Vector2.zero, new Vector2(1, .59f), new Vector2(12, 8), new Vector2(-12, -4));
        }
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 1;
    }
    void EnsureRewardCanvas()
    {
        if (rewardCanvas != null) return;
        hudFont ??= Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR" }, 16);
        var canvasObject = new GameObject("Level rewards", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        rewardCanvas = canvasObject.GetComponent<Canvas>();
        rewardCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rewardCanvas.sortingOrder = 100;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;
        var background = canvasObject.AddComponent<Image>();
        background.color = new Color32(7, 12, 22, 240);
        EventSystem events = FindFirstObjectByType<EventSystem>();
        if (events == null)
        {
            var eventObject = new GameObject("Reward input", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventObject.transform.SetParent(transform, false);
            events = eventObject.GetComponent<EventSystem>();
        }
        var input = events.GetComponent<InputSystemUIInputModule>();
        if (input == null) input = events.gameObject.AddComponent<InputSystemUIInputModule>();
        var legacy = events.GetComponent<StandaloneInputModule>();
        if (legacy != null) legacy.enabled = false;
        if (input.actionsAsset == null) input.AssignDefaultActions();
    }
    static T Element<T>(Transform parent, string name) where T : Component
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return typeof(T) == typeof(RectTransform) ? child.GetComponent<T>() : child.AddComponent<T>();
    }
    static void Anchors(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax; }
    UnityEngine.UI.Text TextElement(Transform parent, string text, int size, Color color)
    {
        var label = Element<UnityEngine.UI.Text>(parent, "Label");
        label.font = hudFont;
        label.text = text; label.fontSize = size; label.color = color;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;
        return label;
    }
    Button ButtonElement(Transform parent, string text)
    {
        var image = Element<Image>(parent, "Choice");
        image.color = new Color32(35, 58, 72, 255);
        var button = image.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(.5f, 1, .9f);
        colors.disabledColor = new Color(.35f, .35f, .35f);
        button.colors = colors;
        button.targetGraphic = image;
        if (text.Length > 0)
        {
            var label = TextElement(button.transform, text, 18, Text);
            Anchors(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }
        return button;
    }
    static void AddIcon(Transform parent, Sprite sprite, Vector2 min, Vector2 max)
    {
        if (sprite == null) return;
        var image = Element<Image>(parent, "Icon");
        image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
        Anchors(image.rectTransform, min, max, Vector2.zero, Vector2.zero);
    }
}
