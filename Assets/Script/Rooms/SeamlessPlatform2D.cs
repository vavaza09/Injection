using UnityEngine;

public class SeamlessPlatform2D : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Platform Pieces")]
    [Tooltip("Add your platform GameObjects here in order from left to right.")]
    [SerializeField] private Transform[] platforms;

    [Header("Looping Threshold")]
    [Tooltip("X position where a platform's right edge is considered off-screen left.")]
    [SerializeField] private float leftBoundX = -15f;

    private float[] halfWidths;

    void Start()
    {
        halfWidths = new float[platforms.Length];
        for (int i = 0; i < platforms.Length; i++)
        {
            var sr = platforms[i].GetComponent<SpriteRenderer>();
            halfWidths[i] = sr != null ? sr.bounds.size.x * 0.5f : 0.5f;
        }
    }

    void Update()
    {
        for (int i = 0; i < platforms.Length; i++)
            platforms[i].Translate(Vector3.left * moveSpeed * Time.deltaTime);

        for (int i = 0; i < platforms.Length; i++)
        {
            if (platforms[i].position.x + halfWidths[i] > leftBoundX)
                continue;

            // Find the rightmost platform's right edge
            float rightEdge = float.MinValue;
            for (int j = 0; j < platforms.Length; j++)
            {
                float edge = platforms[j].position.x + halfWidths[j];
                if (edge > rightEdge)
                    rightEdge = edge;
            }

            // Place this platform so its left edge touches that right edge
            Vector3 pos = platforms[i].position;
            pos.x = rightEdge + halfWidths[i];
            platforms[i].position = pos;
        }
    }
}
