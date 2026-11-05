using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class InventorySection : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] RectTransform content;
    [SerializeField] Text heading;
    [SerializeField] Image background;
    [SerializeField] Color normalColor = new(.07f, .12f, .18f, 1);
    [SerializeField] Color hoverColor = new(.12f, .24f, .28f, 1);
    InventoryPanel panel;
    bool equipped;

    public Transform Content => content;

    public void Initialize(InventoryPanel owner, bool isEquipped)
    {
        panel = owner;
        equipped = isEquipped;
    }

    public void RefreshCount(int count)
    {
        heading.text = $"{(equipped ? "장착 중" : "보관함")}  {count}";
        background.color = normalColor;
    }

    public void OnDrop(PointerEventData eventData)
    {
        SwordSlot slot = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<SwordSlot>() : null;
        if (slot != null)
            panel.Move(slot.Sword, equipped);
        background.color = normalColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData.pointerDrag != null)
            background.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData) => background.color = normalColor;
}
