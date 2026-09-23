using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// 初始選單：選 Desktop 或 VR（按鈕 OnClick 請在 Inspector 手動拉）
// 選 VR 時先偵測頭顯；有設備走實機，沒有就標記要用 XR Device Simulator
public class BootMenu : MonoBehaviour
{
    public string desktopSceneName = AppModeUtil.SceneDesktop;
    public string vrSceneName = AppModeUtil.SceneVR;

    public Button desktopBtn;
    public Button vrBtn;
    public TextMeshProUGUI statusText;

    bool busy;

    void Start()
    {
        RefreshStatus();
    }

    // Inspector → DesktopBtn OnClick 拉這裡
    public void OnClickDesktop()
    {
        if (busy)
        {
            return;
        }
        StartCoroutine(GoDesktop());
    }

    // Inspector → VRBtn OnClick 拉這裡
    public void OnClickVR()
    {
        if (busy)
        {
            return;
        }
        StartCoroutine(GoVR());
    }

    IEnumerator GoDesktop()
    {
        busy = true;
        SetStatus("進入桌面模式…");

        // 桌面不需要 XR，關掉避免頭顯搶畫面
        AppModeUtil.StopXR();
        PlayerPrefs.SetInt(AppModeUtil.PrefUseSimulator, 0);
        PlayerPrefs.Save();

        yield return null;
        SceneManager.LoadScene(desktopSceneName);
    }

    IEnumerator GoVR()
    {
        busy = true;
        SetButtons(false);
        SetStatus("正在偵測 VR 設備…");

        bool hasDevice = false;
        yield return AppModeUtil.TryStartXR(ok => hasDevice = ok);

        if (hasDevice)
        {
            PlayerPrefs.SetInt(AppModeUtil.PrefUseSimulator, 0);
            SetStatus("偵測到 VR 設備，進入實機模式…");
        }
        else
        {
            // 沒有頭顯：關掉失敗的 loader，進場景後開模擬器
            AppModeUtil.StopXR();
            PlayerPrefs.SetInt(AppModeUtil.PrefUseSimulator, 1);
            SetStatus("未偵測到 VR 設備，將使用 XR 模擬器…");
        }

        PlayerPrefs.Save();
        yield return new WaitForSecondsRealtime(0.35f);
        SceneManager.LoadScene(vrSceneName);
    }

    void RefreshStatus()
    {
        bool running = AppModeUtil.IsXRDisplayRunning();
        if (running)
        {
            SetStatus("目前狀態：已偵測到 VR 顯示裝置");
        }
        else
        {
            SetStatus("目前狀態：尚未偵測到 VR 設備（選 VR 將使用模擬器）");
        }
    }

    void SetStatus(string msg)
    {
        if (statusText != null)
        {
            statusText.text = msg;
        }
        Debug.Log("[Boot] " + msg);
    }

    void SetButtons(bool on)
    {
        if (desktopBtn != null)
        {
            desktopBtn.interactable = on;
        }
        if (vrBtn != null)
        {
            vrBtn.interactable = on;
        }
    }
}
