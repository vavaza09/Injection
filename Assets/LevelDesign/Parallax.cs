using UnityEngine;

public class Parallax : MonoBehaviour
{
    private float length, startPos;
    public GameObject cam;

    [Header("1 = ติดกับกล้อง, 0 = อยู่กับที่, Negative = วิ่งสวนทาง (Foreground)")]
    public float parallaxFactor;

    void Start()
    {
        startPos = transform.position.x;
        // เก็บค่าความกว้างของสไปรต์ไว้ใช้คำนวณการวนลูป (ถ้าต้องการ)
        if (GetComponent<SpriteRenderer>() != null)
        {
            length = GetComponent<SpriteRenderer>().bounds.size.x;
        }
    }

    void LateUpdate()
    {
        // คำนวณระยะทางที่เลเยอร์นี้ควรขยับตามตำแหน่งกล้อง
        float distance = (cam.transform.position.x * parallaxFactor);

        // สั่งให้เลเยอร์ขยับตามที่คำนวณ
        transform.position = new Vector3(startPos + distance, transform.position.y, transform.position.z);
    }
}
