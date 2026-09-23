using UnityEngine;

// 掛在放到場景裡的道具上，先記一下名字（之後做刪除選單可能會用到）
public class ItemData : MonoBehaviour
{
    // 道具名字
    public string itemName;

    // 外面呼叫這個來設定名字
    public void SetName(string newName)
    {
        itemName = newName;
    }
}
