using UnityEngine;

// 掛在 VR 場景：依 Boot 選單的偵測結果，決定要不要開 XR Device Simulator
public class VRModeBootstrap : MonoBehaviour
{
    [Tooltip("可手動拖 XR Device Simulator；空白就依名字自動找")]
    public GameObject deviceSimulator;

    void Awake()
    {
        if (deviceSimulator == null)
        {
            // 場景裡的模擬器一開始是關掉的，要用含未啟用物件的搜尋
            Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "XR Device Simulator")
                {
                    deviceSimulator = all[i].gameObject;
                    break;
                }
            }
        }

        bool useSim = PlayerPrefs.GetInt(AppModeUtil.PrefUseSimulator, 1) == 1;

        // 再保險：執行中其實已有頭顯，就不要開模擬器
        if (AppModeUtil.IsXRDisplayRunning())
        {
            useSim = false;
        }

        if (deviceSimulator != null)
        {
            deviceSimulator.SetActive(useSim);
            Debug.Log(useSim
                ? "[VR] 未使用實機頭顯 → 啟用 XR Device Simulator"
                : "[VR] 使用實機頭顯 → 關閉 XR Device Simulator");
        }
        else
        {
            Debug.LogWarning("[VR] 找不到 XR Device Simulator 物件");
        }
    }
}
