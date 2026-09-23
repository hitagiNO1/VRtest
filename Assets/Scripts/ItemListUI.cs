using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 右邊的道具清單 UI
// 點按鈕打開 / 再點一次關閉，並動態生出可以拖曳的道具列
public class ItemListUI : MonoBehaviour
{
    // 一個道具的資料（給 Inspector 填）
    [System.Serializable]
    public class ItemInfo
    {
        public string showName;   // 畫面上顯示的名字
        public Sprite icon;       // 小圖（可以空白）
        public GameObject prefab; // 真正要複製出來的 3D 物件
    }

    public Button openButton;                 // 「選擇道具」按鈕
    public GameObject listPanel;              // 展開後的那個面板底圖（Container）
    public RectTransform itemList;            // 放每一列道具的地方（Panel）
    public ItemPlacementController placeScript;
    public ItemInfo[] items;                  // 道具清單

    public float itemHeight = 64f; // 每一列高度
    public float space = 8f;       // 列與列之間的間隔

    void Awake()
    {

        // 一開始先關起來
        if (listPanel != null)
        {
            listPanel.SetActive(false);
        }

        SetupListLayout();
        MakeItemRows();
    }

    // 打開或關閉道具清單
    public void OpenCloseList()
    {
        // 現在開著就關，關著就開
        bool nowOpen = listPanel.activeSelf;
        listPanel.SetActive(!nowOpen);
    }

    // 設定垂直排列（讓道具一列一列排好）
    void SetupListLayout()
    {
        VerticalLayoutGroup layout = itemList.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = itemList.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = space;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    // 依照 items 陣列生出每一列 UI
    void MakeItemRows()
    {
        // 先清掉舊的（避免重複）
        for (int i = itemList.childCount - 1; i >= 0; i--)
        {
            Destroy(itemList.GetChild(i).gameObject);
        }

        for (int i = 0; i < items.Length; i++)
        {
            ItemInfo info = items[i];
            if (info == null || info.prefab == null)
            {
                continue;
            }

            string name = info.showName;
            if (name == null || name == "")
            {
                name = info.prefab.name;
            }

            MakeOneRow(name, info.icon, info.prefab);
        }
    }

    // 做出「一列」道具 UI（白底 + 小方塊圖示 + 文字）
    void MakeOneRow(string showName, Sprite icon, GameObject prefab)
    {
        // --- 整列的底 ---
        GameObject row = new GameObject(showName + "_Row");
        row.AddComponent<RectTransform>();
        row.AddComponent<CanvasRenderer>();
        row.AddComponent<Image>();
        row.AddComponent<LayoutElement>();
        row.AddComponent<ItemRowUI>();

        RectTransform rowRT = row.GetComponent<RectTransform>();
        rowRT.SetParent(itemList, false);

        Image bg = row.GetComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.92f);
        bg.raycastTarget = true; // 一定要開，不然拖不起來

        LayoutElement le = row.GetComponent<LayoutElement>();
        le.preferredHeight = itemHeight;
        le.minHeight = itemHeight;

        // --- 左邊小圖示（沒圖就先用灰色） ---
        GameObject iconObj = new GameObject("Icon");
        iconObj.AddComponent<RectTransform>();
        iconObj.AddComponent<CanvasRenderer>();
        iconObj.AddComponent<Image>();

        RectTransform iconRT = iconObj.GetComponent<RectTransform>();
        iconRT.SetParent(rowRT, false);
        iconRT.anchorMin = new Vector2(0f, 0.5f);
        iconRT.anchorMax = new Vector2(0f, 0.5f);
        iconRT.pivot = new Vector2(0f, 0.5f);
        iconRT.anchoredPosition = new Vector2(12f, 0f);
        iconRT.sizeDelta = new Vector2(40f, 40f);

        Image iconImage = iconObj.GetComponent<Image>();
        iconImage.raycastTarget = false;
        if (icon != null)
        {
            iconImage.sprite = icon;
        }
        else
        {
            iconImage.color = new Color(0.75f, 0.75f, 0.75f, 1f);
        }

        // --- 名稱文字 ---
        GameObject textObj = new GameObject("Label");
        textObj.AddComponent<RectTransform>();
        textObj.AddComponent<CanvasRenderer>();
        textObj.AddComponent<TextMeshProUGUI>();

        RectTransform textRT = textObj.GetComponent<RectTransform>();
        textRT.SetParent(rowRT, false);
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(60f, 4f);
        textRT.offsetMax = new Vector2(-12f, -4f);

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = showName;
        tmp.fontSize = 22f;
        tmp.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.raycastTarget = false;

        // 把資料傳給這一列的拖曳腳本
        ItemRowUI rowUI = row.GetComponent<ItemRowUI>();
        rowUI.Init(placeScript, prefab, showName, icon);
    }
}
