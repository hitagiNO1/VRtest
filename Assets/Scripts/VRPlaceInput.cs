using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

// VR 場景的輸入總管（只靠手把 Near-Far Interactor，不用滑鼠）
// 放置：清單點選 → ghost 跟隨 → 扳機放地上
// 選取：手上沒東西時扣扳機，用「瞄準錐」找最近道具（不靠精準物理射線）
public class VRPlaceInput : MonoBehaviour
{
    public NearFarInteractor nearFar;
    public ItemPlacementController placeScript;
    public ItemSelectMenu selectMenu;

    [Header("選取容錯")]
    public float selectMaxDistance = 30f;   // 最遠可以選多遠
    public float selectMaxAngle = 20f;      // 瞄準方向左右偏幾度內算指到

    GameObject selectedPrefab;
    bool waitTriggerUp;
    float waitTriggerUpUntil; // 逾時強制解除，避免模擬器卡住

    void Start()
    {
        if (nearFar == null)
        {
            nearFar = FindInteractor("Right");
        }

        if (selectMenu == null)
        {
            selectMenu = GetComponent<ItemSelectMenu>();
            if (selectMenu == null)
            {
                selectMenu = FindFirstObjectByType<ItemSelectMenu>();
            }
        }

        if (selectMenu != null)
        {
            selectMenu.enabled = true;
            selectMenu.vrMode = true;
            selectMenu.EnsureReady();
        }

        if (nearFar == null)
        {
            Debug.LogWarning("找不到 NearFarInteractor。請把 Right Near-Far Interactor 拖到 VRPlaceInput.nearFar");
            return;
        }

        CurveInteractionCaster caster = nearFar.farInteractionCaster as CurveInteractionCaster;
        if (caster != null && caster.castDistance < 30f)
        {
            caster.castDistance = 30f;
        }

        Debug.Log("VRPlaceInput 就緒 | nearFar=" + nearFar.name
                  + " | selectMenu=" + (selectMenu != null)
                  + " | placeScript=" + (placeScript != null));
    }

    NearFarInteractor FindInteractor(string handName)
    {
        NearFarInteractor[] list = FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None);

        for (int i = 0; i < list.Length; i++)
        {
            bool selfMatch = list[i].name.Contains(handName);
            bool parentMatch = list[i].transform.parent != null
                               && list[i].transform.parent.name.Contains(handName);

            if (selfMatch || parentMatch)
            {
                return list[i];
            }
        }

        if (list.Length > 0)
        {
            return list[0];
        }
        return null;
    }

    public void SelectPrefab(GameObject prefab)
    {
        selectedPrefab = prefab;
        ArmWaitTriggerUp();

        if (placeScript == null || nearFar == null)
        {
            return;
        }

        if (selectMenu != null)
        {
            selectMenu.HideMenu();
        }

        Ray ray = GetControllerRay();
        placeScript.StartDragRay(prefab, ray, true);
        Debug.Log("已選擇：" + prefab.name + " → 指著地面再扣扳機放置");
    }

    void Update()
    {
        if (placeScript == null || nearFar == null)
        {
            return;
        }

        // 逾時解除「等扳機放開」，避免 Device Simulator 狀態卡住
        if (waitTriggerUp && Time.unscaledTime >= waitTriggerUpUntil)
        {
            waitTriggerUp = false;
        }

        Ray ray = GetControllerRay();
        bool onUI = IsUIBlockingGround(ray);
        bool press = TriggerPressedThisFrame();
        bool held = TriggerHeld();

        if (waitTriggerUp)
        {
            if (held == false)
            {
                waitTriggerUp = false;
            }

            if (placeScript.IsDragging)
            {
                placeScript.MoveDragRay(ray, onUI);
            }
            return;
        }

        if (placeScript.IsDragging)
        {
            placeScript.MoveDragRay(ray, onUI);

            if (press)
            {
                placeScript.StopDragRay(ray, onUI);
                selectedPrefab = null;
                ArmWaitTriggerUp();
                Debug.Log("放置結束，放開扳機後可再指物件選取");
            }
            return;
        }

        if (press == false)
        {
            return;
        }

        // 有預選 prefab 但沒在拖：重新開始拖（少見）
        if (selectedPrefab != null && onUI == false)
        {
            placeScript.StartDragRay(selectedPrefab, ray, onUI);
            return;
        }

        // 選取場景物件
        if (selectMenu == null)
        {
            Debug.LogWarning("扣扳機想選取，但 selectMenu 是空的");
            return;
        }

        TrySelectItem(onUI);
    }

    void ArmWaitTriggerUp()
    {
        waitTriggerUp = true;
        waitTriggerUpUntil = Time.unscaledTime + 0.5f;
    }

    void TrySelectItem(bool onUI)
    {
        selectMenu.EnsureReady();

        ItemData picked = FindBestItemInAimCone();
        int itemCount = CountPlacedItems();

        Debug.Log("扣扳機選取 | onUI=" + onUI
                  + " | 場上道具數=" + itemCount
                  + " | 瞄到=" + (picked != null ? picked.gameObject.name : "無"));

        if (picked != null)
        {
            selectMenu.ShowMenu(picked.gameObject);
            return;
        }

        // 沒瞄到道具：若指著 UI，把事件留給按鈕；否則關掉選單
        if (onUI || IsPointingAtUI())
        {
            return;
        }

        selectMenu.HideMenu();
    }

    int CountPlacedItems()
    {
        ItemData[] all = FindObjectsByType<ItemData>(FindObjectsSortMode.None);
        int n = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name.Contains("_Ghost") == false)
            {
                n++;
            }
        }
        return n;
    }

    // 不靠物理射線精準命中：看瞄準方向錐體內哪個道具最接近中心
    ItemData FindBestItemInAimCone()
    {
        Ray aim = GetAimRay();
        ItemData[] all = FindObjectsByType<ItemData>(FindObjectsSortMode.None);

        ItemData best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < all.Length; i++)
        {
            ItemData item = all[i];
            if (item == null)
            {
                continue;
            }
            if (item.gameObject.name.Contains("_Ghost"))
            {
                continue;
            }

            Vector3 toItem = item.transform.position - aim.origin;
            float dist = toItem.magnitude;
            if (dist < 0.05f || dist > selectMaxDistance)
            {
                continue;
            }

            float angle = Vector3.Angle(aim.direction, toItem);
            if (angle > selectMaxAngle)
            {
                continue;
            }

            // 角度越準、距離越近分數越好
            float score = angle + dist * 0.15f;
            if (score < bestScore)
            {
                bestScore = score;
                best = item;
            }
        }

        // 錐體沒找到 → 再用物理備援
        if (best == null && selectMenu != null)
        {
            best = selectMenu.FindItemAlongRay(aim);
            if (best == null && placeScript != null
                && Physics.Raycast(aim, out RaycastHit groundHit, placeScript.rayDistance, placeScript.groundLayer))
            {
                best = selectMenu.FindItemNearPoint(groundHit.point, 1.5f);
            }
        }

        return best;
    }

    bool TriggerPressedThisFrame()
    {
        // 扳機（Activate / UI Press）或握把（Select）都可以選取／放置
        // Device Simulator：滑鼠左鍵=Trigger，G=Grip
        return nearFar.activateInput.ReadWasPerformedThisFrame()
               || nearFar.uiPressInput.ReadWasPerformedThisFrame()
               || nearFar.selectInput.ReadWasPerformedThisFrame();
    }

    bool TriggerHeld()
    {
        return nearFar.activateInput.ReadIsPerformed()
               || nearFar.uiPressInput.ReadIsPerformed()
               || nearFar.selectInput.ReadIsPerformed();
    }

    bool IsPointingAtUI()
    {
        return nearFar.TryGetCurrentUIRaycastResult(out _);
    }

    Ray GetAimRay()
    {
        Transform originTF = nearFar.transform;
        if (nearFar.curveOrigin != null)
        {
            originTF = nearFar.curveOrigin;
        }

        Vector3 origin = originTF.position;
        Vector3 forward = originTF.forward;

        CurveInteractionCaster caster = nearFar.farInteractionCaster as CurveInteractionCaster;
        if (caster != null && caster.samplePoints.IsCreated && caster.samplePoints.Length >= 2)
        {
            origin = caster.samplePoints[0];
            Vector3 end = caster.samplePoints[caster.samplePoints.Length - 1];
            Vector3 dir = end - origin;
            if (dir.sqrMagnitude > 0.0001f)
            {
                return new Ray(origin, dir.normalized);
            }
        }

        return new Ray(origin, forward);
    }

    public Ray GetControllerRay()
    {
        Transform originTF = nearFar.transform;
        if (nearFar.curveOrigin != null)
        {
            originTF = nearFar.curveOrigin;
        }

        Vector3 origin = originTF.position;
        Vector3 forward = originTF.forward;

        CurveInteractionCaster caster = nearFar.farInteractionCaster as CurveInteractionCaster;
        if (caster != null && caster.samplePoints.IsCreated && caster.samplePoints.Length >= 2)
        {
            origin = caster.samplePoints[0];

            if (placeScript != null)
            {
                for (int i = 1; i < caster.samplePoints.Length; i++)
                {
                    Vector3 a = caster.samplePoints[i - 1];
                    Vector3 b = caster.samplePoints[i];
                    Vector3 ab = b - a;
                    float len = ab.magnitude;
                    if (len < 0.0001f)
                    {
                        continue;
                    }

                    if (Physics.Raycast(a, ab / len, out RaycastHit hit, len, placeScript.groundLayer))
                    {
                        Vector3 toHit = hit.point - origin;
                        if (toHit.sqrMagnitude > 0.0001f)
                        {
                            return new Ray(origin, toHit.normalized);
                        }
                    }
                }
            }

            Vector3 end = caster.samplePoints[caster.samplePoints.Length - 1];
            Vector3 dir = end - origin;
            if (dir.sqrMagnitude > 0.0001f)
            {
                return new Ray(origin, dir.normalized);
            }
        }

        return new Ray(origin, forward);
    }

    bool IsUIBlockingGround(Ray ray)
    {
        bool hasUI = nearFar.TryGetCurrentUIRaycastResult(out RaycastResult uiHit);
        if (hasUI == false)
        {
            return false;
        }

        if (placeScript != null
            && Physics.Raycast(ray, out RaycastHit groundHit, placeScript.rayDistance, placeScript.groundLayer))
        {
            return uiHit.distance < groundHit.distance;
        }

        return true;
    }
}
