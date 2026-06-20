using UnityEngine;

public class FurnaceLaunchBullet : MonoBehaviour
{
    public float speed = 20f;

    private void Update()
    {
        transform.position += Vector3.up * speed * Time.deltaTime;
        if (Camera.main != null && Camera.main.WorldToViewportPoint(transform.position).y > 1.3f)
            Destroy(gameObject);
    }
}
