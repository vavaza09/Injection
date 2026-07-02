using UnityEngine;

[ExecuteInEditMode] // สั่งให้โค้ดทำงานในหน้า Editor ได้ทันทีโดยไม่ต้องกด Play
public class ParentMaterialController : MonoBehaviour
{
    [Header("หย่อน Material ที่ต้องการรวมไว้ที่นี่")]
    [SerializeField] private Material globalMaterial;

    // ฟังก์ชันนี้จะทำงานอัตโนมัติทันทีเมื่อเราเปลี่ยนหรือหยอด Material ใน Inspector
    private void OnValidate()
    {
        ApplyMaterialToAllChildren();
    }

    [ContextMenu("Force Apply Material")] // สามารถคลิกขวาที่ชื่อสคริปต์ใน Inspector เพื่อสั่งรันเองได้
    public void ApplyMaterialToAllChildren()
    {
        if (globalMaterial == null) return;

        // สั่งดึง SpriteRenderer ของลูกๆ ทุกตัวที่ซ่อนอยู่ในโฟลเดอร์นี้ออกมา
        SpriteRenderer[] childRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer renderer in childRenderers)
        {
            // ใช้ sharedMaterial เพื่อให้มันเปลี่ยนค่าถาวรในหน้าต่างออกแบบด่าน
            if (renderer.sharedMaterial != globalMaterial)
            {
                renderer.sharedMaterial = globalMaterial;
            }
        }
    }
}