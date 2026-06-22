using UnityEngine;

public class GearRotator : MonoBehaviour
{
    [Header("Gear Type")]
    [Tooltip("ถ้าติ๊กถูก อันนี้จะเป็นเฟืองตัวแม่ที่คุมความเร็วหลัก")]
    [SerializeField] private bool isMasterGear = false;

    [Tooltip("ถ้าเป็นเฟืองลูก ให้ลากเฟืองตัวที่มันขบอยู่มาใส่ช่องนี้")]
    [SerializeField] private GearRotator connectedGear;

    [Header("Gear Settings")]
    [Tooltip("ความเร็วในการหมุน (ใช้เฉพาะเฟืองแม่) ติดลบคือหมุนทวนเข็ม")]
    [SerializeField] private float masterRotationSpeed = 50f;

    [Tooltip("จำนวนซี่ฟันของเฟืองอันนี้ (เอาไว้คำนวณอัตราทดให้หมุนไม่เหลื่อมกัน)")]
    [SerializeField] private int toothCount = 12;

    private float _currentSpeed;

    void Start()
    {
        if (!isMasterGear && connectedGear != null)
        {
            // คำนวณความเร็วของเฟืองลูก: หมุนทิศตรงข้าม (-) * ความเร็วเฟืองแม่ * (จำนวนฟันแม่ / จำนวนฟันลูก)
            _currentSpeed = -connectedGear.GetRotationSpeed() * ((float)connectedGear.toothCount / toothCount);
        }
        else if (isMasterGear)
        {
            _currentSpeed = masterRotationSpeed;
        }
    }

    void Update()
    {
        // สั่งหมุนรอบแกน Z ตามความเร็วที่คำนวณได้
        transform.Rotate(0, 0, _currentSpeed * Time.deltaTime);
    }

    public float GetRotationSpeed()
    {
        if (isMasterGear) return masterRotationSpeed;

        // ถ้าเป็นเฟืองลูกซ้อนเฟืองลูก ให้ไปดึงค่าความเร็วที่คำนวณเสร็จแล้วมาใช้
        return _currentSpeed;
    }
}