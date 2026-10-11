using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

// Keeps the hero centered on the road piece below it (Z axis, the hero runs along +X).
// - Every frame a ray finds the ground collider under the hero; its bounds.center.z is the road center.
//   Works for blocks, bridges and crates alike, no hand-tuned offset needed.
// - The hero mesh may sit off-center relative to the root / CharacterController; that offset is measured
//   once and compensated, so the VISIBLE body ends up on the road center, not just the pivot.
// - Optional lanes (laneCount > 1) are spread across the measured road depth.
// - Movement uses SmoothDamp through the CharacterController (collisions stay valid).
// - Owns touch input: swipe up/down = change lane, tap = jump.
[RequireComponent(typeof(CharacterController))]
public class HeroLaneController : MonoBehaviour
{
    [Header("Ground Probe")]
    [Tooltip("Empty = the hero's ground layers are used.")]
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float probeStartHeight = 0.5f;
    [SerializeField] private float probeDistance = 6f;

    [Header("Alignment")]
    [Tooltip("Aligns the visible mesh center instead of the root pivot.")]
    [SerializeField] private bool alignVisualCenter = true;
    [SerializeField] private float alignTime = 0.12f;
    [Tooltip("Lanes are kept this far away from the road edge (hero radius + railing).")]
    [SerializeField] private float edgePadding = 0.35f;

    [Header("Lanes")]
    [Tooltip("1 = always run in the road center. 3 = Left / Middle / Right.")]
    [SerializeField, Min(1)] private int laneCount = 1;

    [Header("Input")]
    [SerializeField] private bool handleTouchInput = true;
    [Tooltip("Minimum swipe length in screen-height fractions.")]
    [SerializeField] private float swipeThreshold = 0.06f;
    [Tooltip("Swipe up moves to +Z by default (away from a camera that looks along +Z).")]
    [SerializeField] private bool invertSwipe = false;

    private CharacterController characterController;
    private HeroCharacterController hero;
    private Renderer[] heroRenderers;
    private float visualOffsetZ;
    private int currentLane;
    private float zVelocity;
    private bool hasGround;
    private float groundCenterZ;
    private float groundDepth = 2f;
    private Vector3 groundCenter;
    private float targetZ;
    private bool hasTarget;
    private Vector2 touchStart;
    private bool touchConsumed;
    private int lastVerticalInput;

    public int CurrentLane { get { return currentLane; } }

    // ScreenTouchController turns this off when it owns the touch input
    public bool HandleTouchInput
    {
        get { return handleTouchInput; }
        set { handleTouchInput = value; }
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        hero = GetComponent<HeroCharacterController>();
        heroRenderers = GetComponentsInChildren<Renderer>();
        currentLane = laneCount / 2;

        if (groundLayers.value == 0 && hero != null)
        {
            groundLayers = hero.GroundLayers;
        }

        // Touch jump is decided here (tap vs. swipe), the hero only keeps keyboard jump
        if (handleTouchInput && hero != null)
        {
            hero.TouchJumpEnabled = false;
        }
    }

    private IEnumerator Start()
    {
        // One frame later: the road is spawned and skins / animator poses are applied
        yield return null;
        MeasureVisualOffset();

        if (ProbeGround())
        {
            targetZ = GetTargetZ();
            hasTarget = true;
            SnapTo(targetZ);
        }
    }

    // Distance between the root pivot and the visible body's center (Z)
    public void MeasureVisualOffset()
    {
        visualOffsetZ = 0f;
        if (!alignVisualCenter || heroRenderers == null) return;

        bool found = false;
        Bounds bounds = new Bounds();
        for (int i = 0; i < heroRenderers.Length; i++)
        {
            Renderer r = heroRenderers[i];
            // Trails / particles would distort the body bounds
            if (r == null || !r.enabled || r is TrailRenderer || r is ParticleSystemRenderer) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        if (found)
        {
            visualOffsetZ = bounds.center.z - transform.position.z;
        }
    }

    private bool ProbeGround()
    {
        RaycastHit hit;
        Vector3 origin = transform.position + Vector3.up * probeStartHeight;
        if (Physics.Raycast(origin, Vector3.down, out hit, probeDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            Bounds b = hit.collider.bounds;
            groundCenter = new Vector3(transform.position.x, b.max.y, b.center.z);
            groundCenterZ = b.center.z;
            groundDepth = b.size.z;
            hasGround = true;
        }
        else
        {
            // Over a gap: keep the last road center
            hasGround = false;
        }
        return hasGround;
    }

    private float GetTargetZ()
    {
        float laneZ = groundCenterZ;
        if (laneCount > 1)
        {
            float usable = Mathf.Max(0f, groundDepth - 2f * edgePadding);
            float spacing = usable / (laneCount - 1);
            laneZ += (currentLane - (laneCount - 1) * 0.5f) * spacing;
        }
        // Root position that puts the visible body on the lane
        return laneZ - visualOffsetZ;
    }

    private void SnapTo(float z)
    {
        characterController.enabled = false;
        Vector3 p = transform.position;
        transform.position = new Vector3(p.x, p.y, z);
        characterController.enabled = true;
        zVelocity = 0f;
    }

    private void Update()
    {
        // Paused / game over: ignore input (taps on the UI must not queue a jump)
        if (Time.timeScale == 0f || HeroManager.gameOver)
        {
            return;
        }

        ReadKeyboard();
        if (handleTouchInput)
        {
            ReadTouch();
        }
    }

    // Runs after HeroCharacterController.Update moved the hero along X / Y
    private void LateUpdate()
    {
        if (Time.deltaTime <= 0f || !characterController.enabled)
        {
            return;
        }

        if (ProbeGround())
        {
            targetZ = GetTargetZ();
            hasTarget = true;
        }
        if (!hasTarget)
        {
            return; // no ground seen yet
        }

        float z = transform.position.z;
        float newZ = Mathf.SmoothDamp(z, targetZ, ref zVelocity, alignTime);
        characterController.Move(new Vector3(0f, 0f, newZ - z));
    }

    private void ReadKeyboard()
    {
        // W/S, Up/Down arrows ("Vertical" axis), edge-triggered
        float axis = Input.GetAxisRaw("Vertical");
        int input = axis > 0.5f ? 1 : axis < -0.5f ? -1 : 0;
        if (input != 0 && input != lastVerticalInput)
        {
            ChangeLane(input);
        }
        lastVerticalInput = input;
    }

    private void ReadTouch()
    {
        if (Input.touchCount == 0) return;

        Touch touch = Input.GetTouch(0);
        switch (touch.phase)
        {
            case TouchPhase.Began:
                // Taps on buttons (pause etc.) belong to the UI
                touchConsumed = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId);
                touchStart = touch.position;
                break;

            case TouchPhase.Moved:
                if (touchConsumed) break;
                Vector2 delta = touch.position - touchStart;
                if (Mathf.Abs(delta.y) > swipeThreshold * Screen.height && Mathf.Abs(delta.y) > Mathf.Abs(delta.x))
                {
                    int dir = delta.y > 0f ? 1 : -1;
                    ChangeLane(invertSwipe ? -dir : dir);
                    touchConsumed = true;
                }
                break;

            case TouchPhase.Ended:
                if (!touchConsumed && hero != null)
                {
                    hero.RequestJump();
                }
                touchConsumed = true;
                break;

            case TouchPhase.Canceled:
                touchConsumed = true;
                break;
        }
    }

    public void ChangeLane(int direction)
    {
        currentLane = Mathf.Clamp(currentLane + direction, 0, laneCount - 1);
    }

#if UNITY_EDITOR
    // Green: center of the block below the hero and the point the hero's body is pulled to.
    // Works in edit mode too (probes the scene directly).
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            LayerMask mask = groundLayers;
            HeroCharacterController h = GetComponent<HeroCharacterController>();
            if (mask.value == 0 && h != null) mask = h.GroundLayers;

            RaycastHit hit;
            if (!Physics.Raycast(transform.position + Vector3.up * probeStartHeight, Vector3.down, out hit, probeDistance, mask, QueryTriggerInteraction.Ignore))
            {
                return;
            }
            Bounds b = hit.collider.bounds;
            groundCenter = new Vector3(transform.position.x, b.max.y, b.center.z);
            targetZ = b.center.z - visualOffsetZ;
            hasTarget = true;
        }
        if (!hasTarget) return;

        // Road center on the block surface
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCenter, 0.25f);
        Gizmos.DrawLine(groundCenter + Vector3.left * 2f, groundCenter + Vector3.right * 2f);

        // Where the body is pulled to (visible center) and the current body center
        Vector3 bodyTarget = new Vector3(transform.position.x, groundCenter.y + 0.9f, targetZ + visualOffsetZ);
        Gizmos.DrawWireSphere(bodyTarget, 0.15f);
        Gizmos.color = new Color(0.5f, 1f, 0.5f, 0.6f);
        Gizmos.DrawLine(bodyTarget, new Vector3(bodyTarget.x, bodyTarget.y, transform.position.z + visualOffsetZ));
    }
#endif
}
