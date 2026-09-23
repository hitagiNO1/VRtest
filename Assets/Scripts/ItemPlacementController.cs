using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

// 負責：拖出道具預覽 → 放開放到地上（或取消）+ 指引線
// 桌面用滑鼠版 StartDrag / MoveDrag / StopDrag
// VR 用射線版 StartDragRay / MoveDragRay / StopDragRay
public class ItemPlacementController : MonoBehaviour
{
    public Camera mainCam;          // 在 Inspector 手動拖 Main Camera
    public LayerMask groundLayer;   // 只打到地形（Ground）
    public float rayDistance = 500f;
    public float cancelAlpha = 0.3f; // 不能放的時候半透明程度
    public float floatHeight = 2.5f; // 拖曳時 ghost 浮在落點上方多高（才看得到指引線）

    GameObject nowPrefab;   // 正在拖的是哪一種道具
    GameObject ghost;       // 預覽用的假物件（跟著滑鼠）
    float yOffset;          // 物件中心到地面的高度差（避免埋進地裡）
    bool dragging;          // 現在有沒有在拖

    // 預覽物件的材質（改透明度用）
    Material[] ghostMats;
    Color[] ghostColors;

    // 指引線（可放置時才顯示）
    LineRenderer guideLine;

    // 最近一次 MoveDragRay 算出來的落點，放開時直接沿用
    // （不要在 StopDragRay 再打一次 Raycast，兩次結果不一定一樣）
    bool lastCanPut;
    Vector3 lastHitPoint;

    // 給外面的選取腳本用：現在是不是正在拖道具
    public bool IsDragging
    {
        get { return dragging; }
    }

    void Start()
    {
        // 自己生一條 LineRenderer 當指引線
        GameObject lineObj = new GameObject("GuideLine");
        lineObj.transform.SetParent(transform);

        guideLine = lineObj.AddComponent<LineRenderer>();
        guideLine.positionCount = 2;
        guideLine.startWidth = 0.08f;
        guideLine.endWidth = 0.08f;
        guideLine.useWorldSpace = true;
        guideLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        guideLine.receiveShadows = false;

        // 用簡單顏色材質，黃線比較明顯
        Material lineMat = new Material(Shader.Find("Sprites/Default"));
        lineMat.color = Color.yellow;
        guideLine.material = lineMat;
        guideLine.startColor = Color.yellow;
        guideLine.endColor = Color.yellow;

        guideLine.enabled = false;
    }

    // ===== 桌面：滑鼠版（給 ItemRowUI 用，行為跟以前一樣） =====

    public void StartDrag(GameObject prefab, Vector2 mousePos)
    {
        Ray ray = mainCam.ScreenPointToRay(mousePos);
        StartDragRay(prefab, ray, CheckMouseOnUI(mousePos));
    }

    public void MoveDrag(Vector2 mousePos)
    {
        Ray ray = mainCam.ScreenPointToRay(mousePos);
        MoveDragRay(ray, CheckMouseOnUI(mousePos));
    }

    public void StopDrag(Vector2 mousePos)
    {
        Ray ray = mainCam.ScreenPointToRay(mousePos);
        StopDragRay(ray, CheckMouseOnUI(mousePos));
    }

    // ===== VR：射線版（之後給手柄腳本呼叫） =====
    // onUI：射線／指標是不是正指在 UI 上（在 UI 上就不能放）

    public void StartDragRay(GameObject prefab, Ray ray, bool onUI)
    {
        // 如果上次拖到一半沒清乾淨，先清掉
        ClearGhost();

        nowPrefab = prefab;
        dragging = true;

        // 複製一個當預覽（ghost）
        ghost = Instantiate(prefab);
        ghost.name = prefab.name + "_Ghost";

        // 關掉碰撞，不然射線會打到自己
        Collider[] cols = ghost.GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++)
        {
            cols[i].enabled = false;
        }

        // 如果有剛體也先關掉物理
        Rigidbody[] rbs = ghost.GetComponentsInChildren<Rigidbody>();
        for (int i = 0; i < rbs.Length; i++)
        {
            rbs[i].isKinematic = true;
            rbs[i].detectCollisions = false;
        }

        // 準備可以改半透明的材質
        SetupGhostLook();

        // 算一下要把物件抬高多少（因為 pivot 在中心）
        yOffset = GetHalfHeight(prefab);

        MoveDragRay(ray, onUI);
    }

    public void MoveDragRay(Ray ray, bool onUI)
    {
        if (dragging == false || ghost == null)
        {
            lastCanPut = false;
            return;
        }

        bool hitGround = ShootRay(ray, out RaycastHit hit);

        // 只有「不在 UI 上」而且「有打到地面」才能放
        bool canPut = (onUI == false) && hitGround;

        lastCanPut = canPut;
        lastHitPoint = hit.point;

        ghost.SetActive(true);

        if (hitGround == true)
        {
            // 拖曳時先浮在落點上方，不要直接貼地（貼地的話指引線會變成超短看不到）
            ghost.transform.position = hit.point + Vector3.up * (yOffset + floatHeight);
            ghost.transform.rotation = Quaternion.identity;
        }
        else
        {
            // 沒打到地面，就先放在射線前方
            ghost.transform.position = ray.GetPoint(10f);
        }

        // 能放 → 不透明；不能放 → 半透明（告訴玩家放開會取消）
        SetGhostAlpha(canPut);

        // 可放置時才畫指引線：從浮空的 ghost 指到曲面上的準確落點
        if (canPut == true)
        {
            ShowGuideLine(hit.point);
        }
        else
        {
            HideGuideLine();
        }
    }

    public void StopDragRay(Ray ray, bool onUI)
    {
        if (dragging == false)
        {
            return;
        }

        // 先用放開當下的射線更新一次，之後就用同一組結果決定放不放，
        // 這樣「看得到指引線」和「真的放得下去」一定一致
        MoveDragRay(ray, onUI);

        if (lastCanPut == true && nowPrefab != null)
        {
            float offset = GetHalfHeight(nowPrefab);

            // 真正放到場景裡（貼在地面上）
            GameObject obj = Instantiate(nowPrefab);
            obj.transform.position = lastHitPoint + Vector3.up * offset;
            obj.transform.rotation = Quaternion.identity;
            obj.name = nowPrefab.name;

            // 掛上 ItemData 記名字
            ItemData data = obj.GetComponent<ItemData>();
            if (data == null)
            {
                data = obj.AddComponent<ItemData>();
            }
            data.SetName(nowPrefab.name);
        }

        // 不管有沒有放成功，預覽都要清掉
        ClearGhost();
    }

    // 清掉預覽物件
    public void ClearGhost()
    {
        ClearGhostMats();
        HideGuideLine();

        if (ghost != null)
        {
            Destroy(ghost);
            ghost = null;
        }

        nowPrefab = null;
        dragging = false;
        lastCanPut = false;
    }

    // 畫線：從浮空 ghost 底部 → 地面落點
    void ShowGuideLine(Vector3 groundPos)
    {
        guideLine.enabled = true;

        // 起點：ghost 底部；終點：曲面上的準確位置
        Vector3 startPos = ghost.transform.position - Vector3.up * yOffset;
        guideLine.SetPosition(0, startPos);
        guideLine.SetPosition(1, groundPos);
    }

    void HideGuideLine()
    {
        if (guideLine != null)
        {
            guideLine.enabled = false;
        }
    }

    // 用現成的射線打地面（桌面、VR 共用）
    bool ShootRay(Ray ray, out RaycastHit hit)
    {
        hit = new RaycastHit();
        bool ok = Physics.Raycast(ray, out hit, rayDistance, groundLayer);
        return ok;
    }

    // 檢查滑鼠是不是停在 UI 上面（桌面用）
    bool CheckMouseOnUI(Vector2 mousePos)
    {
        PointerEventData data = new PointerEventData(EventSystem.current);
        data.position = mousePos;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, results);

        if (results.Count > 0)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    // 算出物件大約一半高度，放到地上時才不會埋進去
    float GetHalfHeight(GameObject obj)
    {
        MeshFilter mf = obj.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            float scaleY = obj.transform.lossyScale.y;
            float half = mf.sharedMesh.bounds.extents.y * scaleY;
            return half;
        }

        // 找不到 mesh 就給個大概值
        return 0.5f;
    }

    // 複製材質，並設成可以半透明（URP 要多設幾行才會生效）
    // 這段是我查資料後照著改的
    void SetupGhostLook()
    {
        List<Material> matList = new List<Material>();
        List<Color> colorList = new List<Color>();

        Renderer[] rends = ghost.GetComponentsInChildren<Renderer>();
        for (int r = 0; r < rends.Length; r++)
        {
            // .materials 會複製一份，才不會改到專案裡原本的材質
            Material[] mats = rends[r].materials;

            for (int i = 0; i < mats.Length; i++)
            {
                Material mat = mats[i];
                MakeMatSeeThrough(mat);

                Color c;
                if (mat.HasProperty("_BaseColor"))
                {
                    c = mat.GetColor("_BaseColor");
                }
                else
                {
                    c = mat.color;
                }
                c.a = 1f;

                colorList.Add(c);
                matList.Add(mat);
            }

            rends[r].materials = mats;
        }

        ghostMats = matList.ToArray();
        ghostColors = colorList.ToArray();
    }

    // canPut = true 不透明；false 半透明
    void SetGhostAlpha(bool canPut)
    {
        if (ghostMats == null)
        {
            return;
        }

        float a;
        if (canPut == true)
        {
            a = 1f;
        }
        else
        {
            a = cancelAlpha;
        }

        for (int i = 0; i < ghostMats.Length; i++)
        {
            if (ghostMats[i] == null)
            {
                continue;
            }

            Color c = ghostColors[i];
            c.a = a;

            // URP 常用 _BaseColor，舊的用 _Color，兩個都設比較保險
            if (ghostMats[i].HasProperty("_BaseColor"))
            {
                ghostMats[i].SetColor("_BaseColor", c);
            }
            if (ghostMats[i].HasProperty("_Color"))
            {
                ghostMats[i].SetColor("_Color", c);
            }
            ghostMats[i].color = c;
        }
    }

    void ClearGhostMats()
    {
        if (ghostMats == null)
        {
            return;
        }

        for (int i = 0; i < ghostMats.Length; i++)
        {
            if (ghostMats[i] != null)
            {
                Destroy(ghostMats[i]);
            }
        }

        ghostMats = null;
        ghostColors = null;
    }

    // 把材質改成 Transparent 模式，alpha 才看得到效果
    void MakeMatSeeThrough(Material mat)
    {
        if (mat == null)
        {
            return;
        }

        // URP 的 Lit 材質有 _Surface 這個參數
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f); // 1 = Transparent
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }
        else
        {
            // 沒有 _Surface 就用比較通用的寫法
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
