using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Pool;

// Put on the Hero. Mounts the weapon prefab on the selected character's hand (humanoid bone)
// in Start, after CharacterSkinLoader swapped the model in Awake.
// Fire: F / Left Ctrl / left mouse (desktop), FireButton (mobile UI) or Fire() from code.
// Space stays jump.
public class WeaponController : MonoBehaviour
{
    [Header("Weapon")]
    [SerializeField] private WeaponModel weaponPrefab;
    [Tooltip("Mount bone of the humanoid rig. Non-humanoid / missing bone = mountFallback.")]
    [SerializeField] private HumanBodyBones mountBone = HumanBodyBones.RightHand;
    [SerializeField] private Transform mountFallback;
    [Tooltip("Offset from the bone in world units, along the bone's axes.")]
    [SerializeField] private Vector3 mountLocalPosition = new Vector3(0f, 0.05f, 0.05f);
    [SerializeField] private Vector3 mountLocalEuler = Vector3.zero;
    [Tooltip("Keeps the barrel pointing along the run direction regardless of the hand animation.")]
    [SerializeField] private bool alignToRunDirection = true;

    [Header("Firing")]
    [SerializeField] private Projectile projectilePrefab;
    [Tooltip("Shots per second.")]
    [SerializeField] private float fireRate = 6f;
    [SerializeField] private int poolDefaultSize = 16;
    [SerializeField] private int poolMaxSize = 64;
    [SerializeField] private KeyCode[] fireKeys = { KeyCode.F, KeyCode.LeftControl };
    [SerializeField] private bool mouseFire = true;

    [Header("Procedural Recoil")]
    [SerializeField] private float recoilKick = 2.5f;
    [SerializeField] private float recoilStiffness = 400f;
    [SerializeField] private float recoilDamping = 22f;
    [SerializeField] private float maxRecoilOffset = 0.15f;

    [Header("Muzzle Flash")]
    [SerializeField] private float flashLightDuration = 0.05f;

    private ObjectPool<Projectile> projectilePool;
    private Transform poolRoot;
    private WeaponModel weapon;
    private Transform weaponTransform;
    private Transform muzzle;
    private float flashIntensity;
    private float flashTimer;
    private float nextFireTime;
    private bool triggerHeld;
    private float recoilOffset;
    private float recoilVelocity;

    // Raised for every shot that actually leaves the barrel (sounds, effects)
    public static event System.Action Fired;

    private void Awake()
    {
        if (projectilePrefab == null)
        {
            Debug.LogError("WeaponController: projectile prefab is not assigned.", this);
            enabled = false;
            return;
        }

        poolRoot = new GameObject("[Projectiles]").transform;
        projectilePool = new ObjectPool<Projectile>(
            CreateProjectile,
            p => p.gameObject.SetActive(true),
            p => p.gameObject.SetActive(false),
            p => { if (p != null) Destroy(p.gameObject); },
            false, poolDefaultSize, poolMaxSize);

        // Prewarm so the first shots do not instantiate
        Projectile[] warm = new Projectile[poolDefaultSize];
        for (int i = 0; i < warm.Length; i++) warm[i] = projectilePool.Get();
        for (int i = 0; i < warm.Length; i++) projectilePool.Release(warm[i]);
    }

    private void Start()
    {
        MountWeapon();
    }

    private void OnDestroy()
    {
        if (poolRoot != null) Destroy(poolRoot.gameObject);
    }

    private Projectile CreateProjectile()
    {
        Projectile p = Instantiate(projectilePrefab, poolRoot);
        p.SetPool(projectilePool);
        p.gameObject.SetActive(false);
        return p;
    }

    private void MountWeapon()
    {
        Transform mount = mountFallback != null ? mountFallback : transform;
        Animator animator = GetComponent<Animator>();
        if (animator != null && animator.isHuman)
        {
            Transform bone = animator.GetBoneTransform(mountBone);
            if (bone != null) mount = bone;
        }

        if (weaponPrefab != null)
        {
            weapon = Instantiate(weaponPrefab, mount);
            weaponTransform = weapon.transform;
            weaponTransform.localRotation = Quaternion.Euler(mountLocalEuler);

            // Rig bones are often scaled (FBX 0.01 / 100): keep the prefab's world size
            Vector3 boneScale = mount.lossyScale;
            Vector3 prefabScale = weaponPrefab.transform.localScale;
            weaponTransform.localScale = new Vector3(
                prefabScale.x / Mathf.Max(0.0001f, Mathf.Abs(boneScale.x)),
                prefabScale.y / Mathf.Max(0.0001f, Mathf.Abs(boneScale.y)),
                prefabScale.z / Mathf.Max(0.0001f, Mathf.Abs(boneScale.z)));
            weaponTransform.position = mount.position + mount.rotation * mountLocalPosition;
            muzzle = weapon.MuzzlePoint;

            if (weapon.MuzzleLight != null)
            {
                flashIntensity = weapon.MuzzleLight.intensity;
                weapon.MuzzleLight.enabled = false;
            }
        }
        else
        {
            muzzle = mount;
        }
    }

    // ---------------------------------------------------------------- input

    // ScreenTouchController turns this off while it simulates touches with the mouse in the Editor
    public bool MouseFireEnabled
    {
        get { return mouseFire; }
        set { mouseFire = value; }
    }

    // UI button (single shot, respects fire rate)
    public void Fire()
    {
        TryFire();
    }

    // UI hold-to-fire (FireButton)
    public void SetTriggerHeld(bool held)
    {
        triggerHeld = held;
    }

    private void Update()
    {
        if (Time.timeScale == 0f || HeroManager.gameOver)
        {
            triggerHeld = false;
            return;
        }

        if (triggerHeld || ReadDesktopInput())
        {
            TryFire();
        }
    }

    private bool ReadDesktopInput()
    {
        for (int i = 0; i < fireKeys.Length; i++)
        {
            if (Input.GetKey(fireKeys[i])) return true;
        }

        // Touches also simulate mouse clicks; on mobile only the FireButton shoots
        if (mouseFire && Input.touchCount == 0 && Input.GetMouseButton(0))
        {
            return EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
        }
        return false;
    }

    private void TryFire()
    {
        if (!enabled || Time.time < nextFireTime || Time.timeScale == 0f || HeroManager.gameOver) return;
        nextFireTime = Time.time + 1f / Mathf.Max(0.01f, fireRate);

        // Bullets fly along the run direction, the hand animation must not spray them
        Vector3 dir = transform.forward;
        Projectile p = projectilePool.Get();
        p.Launch(muzzle.position, dir);

        PlayMuzzleFlash();
        recoilVelocity -= recoilKick;
        if (Fired != null) Fired();
    }

    private void PlayMuzzleFlash()
    {
        if (weapon == null) return;

        if (weapon.MuzzleFlash != null)
        {
            weapon.MuzzleFlash.Play(true);
        }
        if (weapon.MuzzleLight != null)
        {
            weapon.MuzzleLight.enabled = true;
            weapon.MuzzleLight.intensity = flashIntensity;
            flashTimer = flashLightDuration;
        }
    }

    // ---------------------------------------------------------------- recoil / flash

    // After the Animator posed the hand
    private void LateUpdate()
    {
        if (weaponTransform == null) return;

        float dt = Time.deltaTime;
        if (dt > 0f)
        {
            // Damped spring: kick back, overshoot slightly, settle
            float accel = -recoilStiffness * recoilOffset - recoilDamping * recoilVelocity;
            recoilVelocity += accel * dt;
            recoilOffset = Mathf.Clamp(recoilOffset + recoilVelocity * dt, -maxRecoilOffset, maxRecoilOffset);
        }

        if (alignToRunDirection)
        {
            weaponTransform.rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
        }

        // Follow the hand (world units, independent of bone scale) + kick back along the barrel
        Transform mount = weaponTransform.parent;
        weaponTransform.position = mount.position + mount.rotation * mountLocalPosition
            + weaponTransform.forward * recoilOffset;

        if (flashTimer > 0f)
        {
            flashTimer -= dt;
            if (weapon.MuzzleLight != null)
            {
                weapon.MuzzleLight.intensity = flashIntensity * Mathf.Clamp01(flashTimer / flashLightDuration);
                if (flashTimer <= 0f) weapon.MuzzleLight.enabled = false;
            }
        }
    }
}
