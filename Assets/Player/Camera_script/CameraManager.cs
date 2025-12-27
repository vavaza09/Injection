using UnityEngine;
using Unity.Cinemachine; 

public class CameraManager : MonoBehaviour
{
    public static CameraManager instance;

    [Header("References")]
    public CinemachineCamera activeCamera; 
    public PlayerMovement player;
    
    [Header("Look Down Settings")]
    [Tooltip("How far down the camera looks when falling.")]
    [SerializeField] private float maxFallOffset = -2f;
    [Tooltip("How fast the camera shifts focus.")]
    [SerializeField] private float lookSpeed = 2f;

    // Internal cache
    private CinemachinePositionComposer composer;
    private float defaultYOffset;
    private Rigidbody2D playerRb;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        //else
        //{
        //    Destroy(gameObject);
        //    return;
        //}
    }

    private void Start()
    {
        if (player != null)
        {
            playerRb = player.GetComponent<Rigidbody2D>();
        }

        //Get the Composer from the Cinemachine Camera to change offsets
        if (activeCamera != null)
        {
            composer = activeCamera.GetComponent<CinemachinePositionComposer>();
            if (composer != null)
            {
                defaultYOffset = composer.TargetOffset.y;
            }
        }
    }

    private void Update()
    {
        if (playerRb == null || composer == null) return;

        // Check if falling
        // If vertical velocity is negative (falling)
        // Otherwise, return to the default offset.
        float targetY = (playerRb.linearVelocity.y < -0.1f) ? maxFallOffset : defaultYOffset;

        // move from current to target
        Vector3 currentOffset = composer.TargetOffset;
        currentOffset.y = Mathf.Lerp(currentOffset.y, targetY, Time.deltaTime * lookSpeed);

        // Apply the new offset
        composer.TargetOffset = currentOffset;
    }
}