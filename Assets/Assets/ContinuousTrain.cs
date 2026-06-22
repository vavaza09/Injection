using UnityEngine;

public class ContinuousTrain : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float resetDistanceX = 30f; // ระยะทางแกน X ที่วิ่งไปไกลสุดก่อนจะวาร์ปกลับ

    private Vector3 _startPosition;

    void Start()
    {
        // จำพิกัดเริ่มต้นตอนจอดสแตนด์บายไว้
        _startPosition = transform.position;
    }

    void Update()
    {
        // สั่งให้รถไฟวิ่งตรงไปข้างหน้าเรื่อย ๆ ในแกน X (ติดลบถ้าอยากให้วิ่งไปทางซ้าย)
        transform.Translate(Vector3.right * (moveSpeed * Time.deltaTime));

        // ถ้ารถไฟวิ่งห่างจากจุดสตาร์ตเกินระยะที่ตั้งไว้ ให้วาร์ปกลับไปเริ่มใหม่ทันที
        if (Mathf.Abs(transform.position.x - _startPosition.x) >= resetDistanceX)
        {
            transform.position = _startPosition;
        }
    }
}