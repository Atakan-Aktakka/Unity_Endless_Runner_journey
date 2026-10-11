using UnityEngine;

// Add to an obstacle prefab (e.g. BlockCrate). Works with BlockManager's pool:
// on destroy only colliders + renderers are switched off, OnEnable (next spawn) restores them.
public class DestructibleObstacle : MonoBehaviour, IDestructible
{
    [SerializeField, Min(1)] private int health = 1;
    [SerializeField] private int bonusPoints = 50;
    [Tooltip("Explosion effect (pooled). Spawned at the obstacle's center.")]
    [SerializeField] private GameObject explosionEffect;
    [Tooltip("Short hit flash scale punch while it survives a hit.")]
    [SerializeField] private float hitPunchScale = 0.12f;

    private Collider[] colliders;
    private Renderer[] renderers;
    private Vector3 baseScale;
    private int currentHealth;
    private float punchTimer;
    private const float PunchDuration = 0.12f;

    public bool IsAlive { get { return currentHealth > 0; } }

    // Raised when any destructible is destroyed (tutorial, achievements, sounds)
    public static event System.Action<DestructibleObstacle> Destroyed;

    private void Awake()
    {
        colliders = GetComponentsInChildren<Collider>(true);
        renderers = GetComponentsInChildren<Renderer>(true);
        EffectPool.Prewarm(explosionEffect, 2);
    }

    private void OnEnable()
    {
        // BlockManager rescales crates after Instantiate, so the base scale is read per spawn
        baseScale = transform.localScale;
        currentHealth = health;
        punchTimer = 0f;
        SetVisible(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            DestructibleRegistry.Register(colliders[i], this);
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < colliders.Length; i++)
        {
            DestructibleRegistry.Unregister(colliders[i]);
        }
        if (punchTimer > 0f) transform.localScale = baseScale;
    }

    public void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (!IsAlive) return;

        currentHealth -= amount;
        if (currentHealth > 0)
        {
            punchTimer = PunchDuration;
            return;
        }

        // Colliders off first: the hero can never die on a crate that is already exploding
        SetVisible(false);
        transform.localScale = baseScale;
        punchTimer = 0f;

        Vector3 center = renderers.Length > 0 ? renderers[0].bounds.center : transform.position;
        EffectPool.Spawn(explosionEffect, center, Quaternion.LookRotation(hitDirection));
        Score.AddPoints(bonusPoints);
        if (Destroyed != null) Destroyed(this);
    }

    private void Update()
    {
        if (punchTimer <= 0f) return;
        punchTimer -= Time.deltaTime;
        float t = Mathf.Clamp01(punchTimer / PunchDuration);
        transform.localScale = baseScale * (1f + Mathf.Sin(t * Mathf.PI) * hitPunchScale);
    }

    private void SetVisible(bool visible)
    {
        for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = visible;
        for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = visible;
    }
}
