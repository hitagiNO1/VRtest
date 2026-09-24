using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.UI;

// 點場景裡的道具 → 旁邊跳出刪除選單
// 桌面：滑鼠點選，Screen Space Overlay + 螢幕座標對齊物件，不會被地形擋住
// VR：由 VRPlaceInput 呼叫 ShowMenu / HandleRayClick，選單改成 World Space 立在物件旁邊
public class ItemSelectMenu : MonoBehaviour
{
    public Camera cam;                          // Main Camera
    public ItemPlacementController placeScript; // 用來知道現在有沒有在拖曳
    public GameObject menuPrefab;               // 手做的選單 Prefab
    public float rayDistance = 500f;
    public Vector3 menuOffset = new Vector3(1.2f, 1.2f, 0f); // 世界座標偏移（在物件旁一點）

    [Header("VR 刪除選單")]
    public bool vrMode = false;                    // VR 場景請勾起來
    [Tooltip("選單距離相機多遠（公尺），模擬器建議 1.0～1.5")]
    public float vrMenuDistance = 1.2f;
    [Tooltip("World Space 選單縮放，越大越好點，模擬器建議 0.01～0.02")]
    public float vrMenuScale = 0.012f;
    [Tooltip("相對視線再往上偏一點（公尺）")]
    public float vrMenuHeightOffset = 0.08f;
    [Tooltip("SphereCast 選取半徑（備援）")]
    public float selectRadius = 0.35f;

    // 舊欄位保留，避免場景序列化遺失（新邏輯改用上面幾個）
    public float vrMenuHeight = 0.85f;
    public float vrMenuTowardCam = 0.15f;
    public Vector3 vrMenuOffset = new Vector3(0f, 0.85f, 0f);

    GameObject menuRoot;     // 實際生出來的選單
    RectTransform menuRT;
    Button deleteBtn;
    GameObject targetObj;    // 目前選到的道具
    bool ready;

    void Awake()
    {
        EnsureReady();
    }

    void Start()
    {
        EnsureReady();
    }

    // 可被 VRPlaceInput 反覆呼叫：確保選單一定建好（避免啟用順序問題）
    public void EnsureReady()
    {
        if (ready && menuRoot != null)
        {
            return;
        }

        if (cam == null)
        {
            cam = Camera.main;
        }

        if (menuPrefab == null)
        {
            Debug.LogError("ItemSelectMenu.menuPrefab 是空的，無法建立刪除選單");
            return;
        }

        if (menuRoot != null)
        {
            Destroy(menuRoot);
            menuRoot = null;
        }

        if (vrMode == true)
        {
            BuildWorldSpaceMenu();
        }
        else
        {
            BuildOverlayMenu();
        }

        menuRT = menuRoot.GetComponent<RectTransform>();

        deleteBtn = menuRoot.GetComponentInChildren<Button>();
        if (deleteBtn != null)
        {
            deleteBtn.onClick.RemoveListener(ClickDelete);
            deleteBtn.onClick.AddListener(ClickDelete);
        }

        menuRoot.SetActive(false);
        ready = true;
    }

    // 桌面：生在這個 Overlay Canvas 底下（ItemSelectMenu 掛在 Canvas 上）
    void BuildOverlayMenu()
    {
        menuRoot = Instantiate(menuPrefab, transform);
        menuRoot.name = "ItemMenu";

        Canvas canvas = menuRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;

        menuRoot.transform.localScale = Vector3.one;
    }

    // VR：直接生在場景根部，不能掛在會跟著頭轉的 Canvas 底下
    void BuildWorldSpaceMenu()
    {
        menuRoot = Instantiate(menuPrefab);
        menuRoot.name = "ItemMenu";

        Canvas canvas = menuRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;

        menuRoot.transform.localScale = Vector3.one * vrMenuScale;

        GraphicRaycaster oldRaycaster = menuRoot.GetComponent<GraphicRaycaster>();
        if (oldRaycaster != null)
        {
            oldRaycaster.enabled = false;
        }

        if (menuRoot.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
        {
            menuRoot.AddComponent<TrackedDeviceGraphicRaycaster>();
        }
    }

    void Update()
    {
        if (ready == false || menuRoot == null)
        {
            return;
        }

        // 正在拖曳道具時，不要選取，選單也關掉
        if (placeScript != null && placeScript.IsDragging == true)
        {
            HideMenu();
            return;
        }

        // 選到的東西被刪掉或消失了，順手收起來
        if (targetObj == null && menuRoot.activeSelf == true)
        {
            HideMenu();
            return;
        }

        // 有選中物件時：把選單對到物件旁邊
        if (targetObj != null && menuRoot.activeSelf == true)
        {
            MoveMenuToTarget();
        }

        // VR 的點擊由 VRPlaceInput 負責，這裡不碰滑鼠
        if (vrMode == true)
        {
            return;
        }

        if (Mouse.current == null)
        {
            return;
        }
        if (Mouse.current.leftButton.wasPressedThisFrame == false)
        {
            return;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();

        if (IsMouseOnUI(mousePos) == true)
        {
            return;
        }

        HandleRayClick(cam.ScreenPointToRay(mousePos));
    }

    public void HandleRayClick(Ray ray)
    {
        EnsureReady();

        ItemData item = FindItemAlongRay(ray);
        if (item != null)
        {
            ShowMenu(item.gameObject);
            return;
        }

        HideMenu();
    }

    public ItemData FindItemAlongRay(Ray ray)
    {
        return FindItemAlongRay(ray, rayDistance);
    }

    public ItemData FindItemAlongRay(Ray ray, float maxDist)
    {
        if (maxDist <= 0.0001f)
        {
            return null;
        }

        RaycastHit[] hits = Physics.RaycastAll(ray, maxDist, ~0, QueryTriggerInteraction.Ignore);
        if (hits != null && hits.Length > 0)
        {
            System.Array.Sort(hits, CompareHitDistance);

            for (int i = 0; i < hits.Length; i++)
            {
                ItemData item = hits[i].collider.GetComponentInParent<ItemData>();
                if (item != null)
                {
                    return item;
                }
            }
        }

        if (selectRadius > 0.001f)
        {
            RaycastHit[] fatHits = Physics.SphereCastAll(ray, selectRadius, maxDist, ~0, QueryTriggerInteraction.Ignore);
            if (fatHits != null && fatHits.Length > 0)
            {
                System.Array.Sort(fatHits, CompareHitDistance);
                for (int i = 0; i < fatHits.Length; i++)
                {
                    ItemData fatItem = fatHits[i].collider.GetComponentInParent<ItemData>();
                    if (fatItem != null)
                    {
                        return fatItem;
                    }
                }
            }
        }

        return null;
    }

    // 落點附近找最近的道具（射線擦過時的備援）
    public ItemData FindItemNearPoint(Vector3 point, float radius)
    {
        Collider[] cols = Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Ignore);
        ItemData best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < cols.Length; i++)
        {
            ItemData item = cols[i].GetComponentInParent<ItemData>();
            if (item == null)
            {
                continue;
            }

            float d = (item.transform.position - point).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = item;
            }
        }

        return best;
    }

    static int CompareHitDistance(RaycastHit a, RaycastHit b)
    {
        return a.distance.CompareTo(b.distance);
    }

    public void ShowMenu(GameObject obj)
    {
        EnsureReady();

        if (menuRoot == null || obj == null)
        {
            Debug.LogWarning("ShowMenu 失敗：menuRoot 或物件是空的");
            return;
        }

        targetObj = obj;
        menuRoot.SetActive(true);
        MoveMenuToTarget();
        Debug.Log("刪除選單已打開：" + obj.name + " @ " + menuRoot.transform.position);
    }

    void MoveMenuToTarget()
    {
        if (vrMode == true)
        {
            MoveMenuToTargetVR();
        }
        else
        {
            MoveMenuToTargetScreen();
        }
    }

    void MoveMenuToTargetScreen()
    {
        Vector3 worldPos = targetObj.transform.position + menuOffset;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        if (screenPos.z < 0f)
        {
            menuRoot.SetActive(false);
            return;
        }

        menuRoot.SetActive(true);
        menuRT.position = screenPos;
    }

    // VR：選單放在「玩家眼前固定距離」，朝向選中物件的方向，方便模擬器射線點到
    void MoveMenuToTargetVR()
    {
        if (cam == null)
        {
            cam = Camera.main;
        }

        if (cam == null || targetObj == null || menuRoot == null)
        {
            return;
        }

        // 每幀套用縮放，方便你在 Play 模式調 Inspector
        float scale = vrMenuScale;
        if (scale < 0.0001f)
        {
            scale = 0.01f;
        }
        menuRoot.transform.localScale = Vector3.one * scale;

        Vector3 camPos = cam.transform.position;
        Vector3 toObj = targetObj.transform.position - camPos;
        float distToObj = toObj.magnitude;

        Vector3 dir;
        if (distToObj > 0.05f)
        {
            dir = toObj / distToObj;
        }
        else
        {
            dir = cam.transform.forward;
        }

        // 選單放在眼前固定距離；若物件比這個還近，就放在物件前一點，避免穿過物件
        float placeDist = vrMenuDistance;
        if (placeDist < 0.3f)
        {
            placeDist = 0.3f;
        }
        if (distToObj > 0.05f)
        {
            placeDist = Mathf.Min(placeDist, Mathf.Max(0.35f, distToObj - 0.25f));
        }

        Vector3 pos = camPos + dir * placeDist + Vector3.up * vrMenuHeightOffset;
        menuRoot.transform.position = pos;

        // World Space Canvas 正面朝向玩家
        menuRoot.transform.LookAt(cam.transform);
        menuRoot.transform.Rotate(0f, 180f, 0f);
    }

    public void HideMenu()
    {
        targetObj = null;
        if (menuRoot != null)
        {
            menuRoot.SetActive(false);
        }
    }

    void ClickDelete()
    {
        if (targetObj != null)
        {
            Destroy(targetObj);
        }
        HideMenu();
    }

    bool IsMouseOnUI(Vector2 mousePos)
    {
        PointerEventData data = new PointerEventData(EventSystem.current);
        data.position = mousePos;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, results);
        return results.Count > 0;
    }
}
