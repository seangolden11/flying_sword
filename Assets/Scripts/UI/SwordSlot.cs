using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public sealed class SwordSlot : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler,
    IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] Image background;
    [SerializeField] Image icon;
    [SerializeField] Image accent;
    [SerializeField] Text nameLabel;
    [SerializeField] Text effectLabel;
    InventoryPanel panel;
    CanvasGroup canvasGroup;
    Color idleColor = new(.1f, .17f, .24f, 1);
    Color hoverColor = new(.16f, .27f, .33f, 1);
    Vector3 dragOffset;
    bool dragging;

    public SwordInstance Sword { get; private set; }

    public void Bind(InventoryPanel owner, SwordInstance sword, SwordDefinition definition, int number)
    {
        panel = owner;
        Sword = sword;
        icon.sprite = definition.icon;
        icon.color = definition.bladeColor;
        accent.color = definition.bladeColor;
        nameLabel.text = $"{definition.displayName} {number:00}";
        effectLabel.text = definition.description;
        background.color = idleColor;
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!dragging && eventData.button == PointerEventData.InputButton.Left)
            panel.Toggle(Sword);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        dragging = true;
        eventData.eligibleForClick = false;
        var rect = (RectTransform)transform;
        Vector2 size = rect.rect.size;
        dragOffset = rect.position - (Vector3)eventData.position;
        rect.SetParent(panel.DragLayer, true);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        canvasGroup.blocksRaycasts = false;
        background.color = hoverColor;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragging)
            transform.position = (Vector3)eventData.position + dragOffset;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        dragging = false;
        canvasGroup.blocksRaycasts = true;
        panel.Refresh();
    }

    public void OnPointerEnter(PointerEventData eventData) => background.color = hoverColor;
    public void OnPointerExit(PointerEventData eventData)
    {
        if (!dragging)
            background.color = idleColor;
    }
}
