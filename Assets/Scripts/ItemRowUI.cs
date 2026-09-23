using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// 道具清單的「一列」
// 實作 Unity 的拖曳介面，按住這一列拖出去時會通知放置腳本
public class ItemRowUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // 負責真正把 3D 物件放進場景的腳本
    ItemPlacementController placeScript;

    // 這一列對應哪一個 prefab
    GameObject myPrefab;

    // 建立這一列的時候呼叫，把需要的資料傳進來
    public void Init(ItemPlacementController script, GameObject prefab, string showName, Sprite icon)
    {
        placeScript = script;
        myPrefab = prefab;

        // 改文字
        TextMeshProUGUI text = GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = showName;
        }

        // 如果有圖示就換上（現在多半沒有，先留著）
        if (icon != null)
        {
            Transform iconTF = transform.Find("Icon");
            if (iconTF != null)
            {
                Image iconImage = iconTF.GetComponent<Image>();
                if (iconImage != null)
                {
                    iconImage.sprite = icon;
                    iconImage.enabled = true;
                }
            }
        }
    }

    // 開始拖曳
    public void OnBeginDrag(PointerEventData eventData)
    {
        placeScript.StartDrag(myPrefab, eventData.position);
    }

    // 拖曳中（每一幀都會進來）
    public void OnDrag(PointerEventData eventData)
    {
        placeScript.MoveDrag(eventData.position);
    }

    // 放開滑鼠
    public void OnEndDrag(PointerEventData eventData)
    {
        placeScript.StopDrag(eventData.position);
    }
}
