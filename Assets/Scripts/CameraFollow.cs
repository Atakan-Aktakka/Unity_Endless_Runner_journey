using UnityEngine;

// Side-view follow camera.
// X: locked to the hero every frame, so there is no lag at any speed and the hero keeps the same
//    left-of-center screen position. An optional look-ahead shows more road at high speed.
// Y: follows the road height smoothly (steps up / down); jumps do not shake the camera.
// Z: fixed offset.
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target = null;

    [Header("Horizontal")]
    [Tooltip("Extra look-ahead (m) per m/s above the start speed. 0 = hero always at exactly the same screen X.")]
    [SerializeField] private float lookAheadPerSpeed = 0f;
    [SerializeField] private float maxLookAhead = 2f;
    [SerializeField] private float lookAheadSmoothTime = 0.6f;

    [Header("Vertical")]
    [Tooltip("Share of the road height change the camera follows (0 = fixed height).")]
    [SerializeField, Range(0f, 1f)] private float heightFollow = 0.6f;
    [SerializeField] private float heightSmoothTime = 0.35f;
    [Tooltip("Road surface height the start camera position was set up for.")]
    [SerializeField] private float baseGroundHeight = 0.4f;

    private HeroCharacterController hero;
    private Vector3 offset;
    private float baseCameraY;
    private float baseSpeed;
    private float lookAhead;
    private float lookAheadVelocity;
    private float groundHeight;
    private float cameraY;
    private float yVelocity;

    void Start()
    {
        if (target == null)
        {
            Debug.LogError("CameraFollow: target is not assigned.", this);
            enabled = false;
            return;
        }

        hero = target.GetComponent<HeroCharacterController>();
        offset = transform.position - target.position;
        baseCameraY = transform.position.y;
        cameraY = baseCameraY;
        groundHeight = baseGroundHeight;
        baseSpeed = hero != null ? hero.RunSpeed : 0f;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        Vector3 heroPosition = target.position;

        // Look-ahead grows with the speed (smoothed, so speed-ups do not jerk the view)
        if (hero != null && lookAheadPerSpeed > 0f)
        {
            float desired = Mathf.Clamp((hero.RunSpeed - baseSpeed) * lookAheadPerSpeed, 0f, maxLookAhead);
            lookAhead = Mathf.SmoothDamp(lookAhead, desired, ref lookAheadVelocity, lookAheadSmoothTime, Mathf.Infinity, dt);
        }

        // Road height: taken while grounded; dropping well below it (step down / pit) updates it in the air
        if (hero == null || hero.IsGrounded || heroPosition.y < groundHeight - 0.5f)
        {
            groundHeight = heroPosition.y;
        }
        float targetY = baseCameraY + (groundHeight - baseGroundHeight) * heightFollow;
        cameraY = Mathf.SmoothDamp(cameraY, targetY, ref yVelocity, heightSmoothTime, Mathf.Infinity, dt);

        transform.position = new Vector3(heroPosition.x + offset.x + lookAhead, cameraY, heroPosition.z + offset.z);
    }
}
