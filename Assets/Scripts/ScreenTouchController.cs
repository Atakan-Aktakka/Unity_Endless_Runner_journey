using UnityEngine;
using UnityEngine.EventSystems;

// Mobile split-screen controls (multi-touch, both halves work at the same time):
//   left half  = fire (first shot on touch, auto fire while held)
//   right half = jump (buffered, same grace period as the keyboard)
// Touches that start on UI (pause / game over buttons) are ignored for their whole lifetime.
// Desktop input stays where it was: Space = jump (HeroCharacterController), Ctrl / F = fire (WeaponController).
// In the Editor the left mouse button simulates a touch (left half fire, right half jump), so the mobile
// controls can be tested without an APK; the weapon's own "click anywhere to fire" is turned off then.
public class ScreenTouchController : MonoBehaviour
{
    private enum Role { None, Fire, Jump }

    private struct TrackedTouch
    {
        public int fingerId;
        public Role role;
    }

    [SerializeField] private HeroCharacterController hero;
    [SerializeField] private WeaponController weapon;
    [Tooltip("Screen split in width fractions (0.5 = middle).")]
    [SerializeField, Range(0.1f, 0.9f)] private float splitX = 0.5f;
    [Tooltip("Off = fire on the right, jump on the left (left-handed players).")]
    [SerializeField] private bool fireOnLeft = true;
    [Tooltip("Editor only: the left mouse button behaves like a touch.")]
    [SerializeField] private bool simulateTouchWithMouse = true;

    private const int MaxTouches = 10;
    private readonly TrackedTouch[] tracked = new TrackedTouch[MaxTouches];
    private int trackedCount;
    private bool fireHeld;
    private Role mouseRole;

    private void Awake()
    {
        if (hero == null) hero = GetComponent<HeroCharacterController>();
        if (hero == null) hero = FindFirstObjectByType<HeroCharacterController>();
        if (weapon == null && hero != null) weapon = hero.GetComponent<WeaponController>();

        // This component owns touch input now; the old tap-to-jump paths must not fire as well
        if (hero != null)
        {
            hero.TouchJumpEnabled = false;
            HeroLaneController lane = hero.GetComponent<HeroLaneController>();
            if (lane != null) lane.HandleTouchInput = false;
        }

        if (SimulatingWithMouse && weapon != null)
        {
            weapon.MouseFireEnabled = false;
        }
    }

    private bool SimulatingWithMouse
    {
        get { return simulateTouchWithMouse && Application.isEditor; }
    }

    private void OnDisable()
    {
        trackedCount = 0;
        SetFireHeld(false);
    }

    private void Update()
    {
        // Paused / game over: forget all touches, the UI owns the screen
        if (Time.timeScale == 0f || HeroManager.gameOver)
        {
            trackedCount = 0;
            mouseRole = Role.None;
            SetFireHeld(false);
            return;
        }

        bool anyFire = false;
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    Role role = GetRole(touch);
                    Track(touch.fingerId, role);
                    if (role == Role.Jump && hero != null)
                    {
                        hero.RequestJump();
                    }
                    else if (role == Role.Fire && weapon != null)
                    {
                        weapon.Fire(); // first shot immediately, hold keeps firing
                        anyFire = true;
                    }
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (RoleOf(touch.fingerId) == Role.Fire) anyFire = true;
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    Untrack(touch.fingerId);
                    break;
            }
        }

        if (SimulatingWithMouse && Input.touchCount == 0 && ReadMouse())
        {
            anyFire = true;
        }

        SetFireHeld(anyFire);
    }

    // Editor: left mouse button = one finger. Returns true while it holds the fire half.
    private bool ReadMouse()
    {
        if (Input.GetMouseButtonDown(0))
        {
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            mouseRole = overUI ? Role.None : RoleAt(Input.mousePosition.x);
            if (mouseRole == Role.Jump && hero != null)
            {
                hero.RequestJump();
            }
            else if (mouseRole == Role.Fire && weapon != null)
            {
                weapon.Fire();
            }
        }

        bool firing = mouseRole == Role.Fire && Input.GetMouseButton(0);
        if (Input.GetMouseButtonUp(0))
        {
            mouseRole = Role.None;
        }
        return firing;
    }

    private Role GetRole(Touch touch)
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
        {
            return Role.None;
        }
        return RoleAt(touch.position.x);
    }

    private Role RoleAt(float screenX)
    {
        bool leftHalf = screenX < Screen.width * splitX;
        return leftHalf == fireOnLeft ? Role.Fire : Role.Jump;
    }

    // Only changes the weapon's trigger when our state changes, so FireButton / desktop input keep working
    private void SetFireHeld(bool held)
    {
        if (held == fireHeld) return;
        fireHeld = held;
        if (weapon != null) weapon.SetTriggerHeld(held);
    }

    private void Track(int fingerId, Role role)
    {
        Untrack(fingerId);
        if (trackedCount < MaxTouches)
        {
            tracked[trackedCount++] = new TrackedTouch { fingerId = fingerId, role = role };
        }
    }

    private void Untrack(int fingerId)
    {
        for (int i = 0; i < trackedCount; i++)
        {
            if (tracked[i].fingerId == fingerId)
            {
                tracked[i] = tracked[--trackedCount];
                return;
            }
        }
    }

    private Role RoleOf(int fingerId)
    {
        for (int i = 0; i < trackedCount; i++)
        {
            if (tracked[i].fingerId == fingerId) return tracked[i].role;
        }
        return Role.None;
    }
}
