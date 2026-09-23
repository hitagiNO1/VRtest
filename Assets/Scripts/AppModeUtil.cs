using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

// 桌面 / VR 模式切換時共用的小工具
public static class AppModeUtil
{
    public const string PrefUseSimulator = "App.UseXRSimulator";
    public const string SceneBoot = "Start";
    public const string SceneDesktop = "Desktop";
    public const string SceneVR = "VR";

    public static bool IsXRDisplayRunning()
    {
        List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);

        for (int i = 0; i < displays.Count; i++)
        {
            if (displays[i] != null && displays[i].running)
            {
                return true;
            }
        }

        return false;
    }

    // 嘗試初始化 XR，回傳是否真的有頭顯在跑
    public static IEnumerator TryStartXR(System.Action<bool> done)
    {
        XRGeneralSettings settings = XRGeneralSettings.Instance;
        if (settings == null || settings.Manager == null)
        {
            done(false);
            yield break;
        }

        XRManagerSettings mgr = settings.Manager;

        if (mgr.activeLoader == null)
        {
            yield return mgr.InitializeLoader();
        }

        if (mgr.activeLoader == null)
        {
            done(false);
            yield break;
        }

        mgr.StartSubsystems();

        // 等一兩幀讓 display subsystem 起來
        yield return null;
        yield return null;

        bool ok = IsXRDisplayRunning();
        done(ok);
    }

    public static void StopXR()
    {
        XRGeneralSettings settings = XRGeneralSettings.Instance;
        if (settings == null || settings.Manager == null)
        {
            return;
        }

        XRManagerSettings mgr = settings.Manager;
        if (mgr.activeLoader == null)
        {
            return;
        }

        mgr.StopSubsystems();
        mgr.DeinitializeLoader();
    }
}
