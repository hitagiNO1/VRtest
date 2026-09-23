using UnityEngine;

// 讓選單跟著頭部前方，但不要掛在 Camera 底下
// （掛在頭上時，手部射線常打到 UI 背面，點不到也不會變色）
public class VRMenuFollow : MonoBehaviour
{
    public Transform head;          // Main Camera
    public float distance = 1.5f;   // 放在眼前多遠
    public float heightOffset = -0.1f;
    public float followSpeed = 8f;  // 越大跟越緊

    void LateUpdate()
    {
        if (head == null)
        {
            return;
        }

        // 只取頭部的水平朝向：低頭瞄地面時面板不會跟著壓下來擋住射線
        Vector3 flatForward = head.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f)
        {
            // 正上或正下看的瞬間，維持面板原本的朝向
            flatForward = transform.forward;
            flatForward.y = 0f;
        }
        flatForward.Normalize();

        // 目標：頭部前方、固定在眼睛高度
        Vector3 targetPos = head.position
            + flatForward * distance
            + Vector3.up * heightOffset;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPos,
            Time.deltaTime * followSpeed
        );

        // 正面對準玩家（手部射線才能打到正面）
        Vector3 toHead = transform.position - head.position;
        toHead.y = 0f;
        if (toHead.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(toHead, Vector3.up);
        }
    }
}
