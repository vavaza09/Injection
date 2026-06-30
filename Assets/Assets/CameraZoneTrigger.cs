using UnityEngine;

public class CameraZoneTrigger : MonoBehaviour
{
    [Header("Cinemachine Camera Settings")]
    [Tooltip("ลากกล้อง Cinemachine (Virtual Camera) ประจำโซนนี้มาใส่")]
    [SerializeField] private GameObject zoneCamera;

    [Tooltip("ค่า Priority ตอนที่ผู้เล่นอยู่ในโซนนี้ (ยิ่งสูงยิ่งสำคัญ)")]
    [SerializeField] private int activePriority = 20;

    [Tooltip("ค่า Priority ตอนที่ผู้เล่นเดินออกจากโซนนี้ไปแล้ว")]
    [SerializeField] private int inactivePriority = 10;

    private void Start()
    {
        // เริ่มเกมมา ให้กล้องทุกโซนเซ็ตค่าระดับปกติ และยังไม่ต้องตามใคร (Target เป็น null)
        UpdateZoneCamera(inactivePriority, null);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // ตรวจสอบว่าวัตถุที่เข้ามาคือผู้เล่น (ตั้ง Tag ที่ตัวละครว่า Player ด้วยนะครับ)
        if (collision.CompareTag("Player"))
        {
            // ส่งทั้งค่าความสำคัญ (Priority) และ Transform ของตัวเอกคนนั้นเข้าไปที่กล้อง
            UpdateZoneCamera(activePriority, collision.transform);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            // เมื่อเดินออกจากโซน ให้ลด Priority และคืนค่า Target เป็น null เพื่อเคลียร์ระบบให้กล้องโซนถัดไปทำงานแทน
            UpdateZoneCamera(inactivePriority, null);
        }
    }

    private void UpdateZoneCamera(int priorityValue, Transform target)
    {
        if (zoneCamera == null) return;

        // [กรณีที่ 1] รองรับ Cinemachine v3 (Unity 2023 ขึ้นไป)
        if (zoneCamera.TryGetComponent(out Unity.Cinemachine.CinemachineCamera newCam))
        {
            newCam.Priority = priorityValue;
            newCam.Follow = target;      // สั่งให้กล้องวิ่งตามตำแหน่ง (แกน X, Y) ของผู้เล่นที่เพิ่งเดินเข้า Trigger
            newCam.LookAt = target;      // (แถม) เผื่อใช้ระบบหมุนกล้องตามวัตถุ
        }
    }
}