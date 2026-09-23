using UnityEngine;
using UnityEngine.EventSystems;

// Generic helper: detects a genuine double-click (uGUI's own click-count tracking, not two
// unrelated single clicks) on whatever UI element it's attached to and invokes a callback,
// plus an optional single-click callback (fires on the first click of any click, including
// the first half of a double-click). Used by InventoryMenu's item icons: double-click an
// icon to use that item (onDoubleClick), single-click it to assign it to an armed hotkey
// slot (onSingleClick). Attach to any UI element that already has an Image/Graphic on the
// same GameObject (so it's a valid raycast target) via AddComponent<DoubleClickTrigger>().
public class DoubleClickTrigger : MonoBehaviour, IPointerClickHandler
{
    public System.Action onDoubleClick;
    public System.Action onSingleClick;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount >= 2 && onDoubleClick != null)
        {
            onDoubleClick();
        }
        else if (eventData.clickCount == 1 && onSingleClick != null)
        {
            onSingleClick();
        }
    }
}
