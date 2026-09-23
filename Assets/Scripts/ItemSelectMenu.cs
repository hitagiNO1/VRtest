using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 點場景裡的道具 → 旁邊跳出刪除選單
// 用 Screen Space Overlay + 螢幕座標對齊物件，這樣不會被地形擋住
public class ItemSelectMenu : MonoBehaviour
{
    public Camera cam;                          // Main Camera
    public ItemPlacementController placeScript; // 用來知道現在有沒有在拖曳
    public GameObject menuPrefab;               // 手做的選單 Prefab
    public float rayDistance = 500f;
    public Vector3 menuOffset = new Vector3(1.2f, 1.2f, 0f); // 世界座標偏移（在物件旁一點）

    GameObject menuRoot;     // 實際生出來的選單
    RectTransform menuRT;
    Button deleteBtn;
    GameObject targetObj;    // 目前選到的道具

    void Start()
    {
        // 生在這個 Overlay Canvas 底下（ItemSelectMenu 掛在 Canvas 上）
        menuRoot = Instantiate(menuPrefab, transform);
        menuRoot.name = "ItemMenu";

        // Prefab 若是 World Space，改成 Overlay，就不會跟地形搶深度
        Canvas canvas = menuRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100; // 畫在一般 UI 上面一點

        // World Space Prefab 常是 0.01，Overlay 要改回 1
        menuRoot.transform.localScale = Vector3.one;

        menuRT = menuRoot.GetComponent<RectTransform>();

        // Prefab 裡只要有一個 Button，就當刪除鍵
        deleteBtn = menuRoot.GetComponentInChildren<Button>();
        deleteBtn.onClick.AddListener(ClickDelete);

        menuRoot.SetActive(false);
    }

    void Update()
    {
        // 正在拖曳道具時，不要選取，選單也關掉
        if (placeScript != null && placeScript.IsDragging == true)
        {
            HideMenu();
            return;
        }

        // 有選中物件時：把選單對到物件在螢幕上的位置
        if (targetObj != null && menuRoot.activeSelf == true)
        {
            MoveMenuToTarget();
        }

        // 滑鼠左鍵點下去
        if (Mouse.current == null)
        {
            return;
        }
        if (Mouse.current.leftButton.wasPressedThisFrame == false)
        {
            return;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();

        // 點在 UI 上就交給按鈕處理，這裡不要亂關選單
        if (IsMouseOnUI(mousePos) == true)
        {
            return;
        }

        // 從相機往場景射線，看有沒有點到道具
        Ray ray = cam.ScreenPointToRay(mousePos);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, rayDistance) == true)
        {
            ItemData item = hit.collider.GetComponentInParent<ItemData>();
            if (item != null)
            {
                // 點到東西，跳出選單
                ShowMenu(item.gameObject);
                return;
            }
        }

        // 點到空白（或地形）就關掉選單
        HideMenu();
    }

    // 顯示選單在這個物件旁邊
    void ShowMenu(GameObject obj)
    {
        targetObj = obj;
        menuRoot.SetActive(true);
        MoveMenuToTarget();
    }

    // 把 3D 位置轉成螢幕座標，讓選單跟在旁邊
    void MoveMenuToTarget()
    {
        Vector3 worldPos = targetObj.transform.position + menuOffset;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        // 物件在相機後面就先關掉
        if (screenPos.z < 0f)
        {
            menuRoot.SetActive(false);
            return;
        }

        menuRoot.SetActive(true);
        // Overlay 的 position 就是螢幕像素座標
        menuRT.position = screenPos;
    }

    // 關掉選單
    void HideMenu()
    {
        targetObj = null;
        if (menuRoot != null)
        {
            menuRoot.SetActive(false);
        }
    }

    // 按刪除
    void ClickDelete()
    {
        if (targetObj != null)
        {
            Destroy(targetObj);
        }
        HideMenu();
    }

    // 滑鼠是不是在 UI 上
    bool IsMouseOnUI(Vector2 mousePos)
    {
        PointerEventData data = new PointerEventData(EventSystem.current);
        data.position = mousePos;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, results);

        if (results.Count > 0)
        {
            return true;
        }
        return false;
    }
}
