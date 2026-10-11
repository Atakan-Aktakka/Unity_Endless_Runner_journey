using UnityEngine;
using UnityEngine.Pool;

// Pooled bullet. Moves with a swept SphereCast instead of a Rigidbody trigger:
// no tunneling at high speed and no physics bodies to simulate.
public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 30f;
    [SerializeField] private float maxRange = 25f;
    [SerializeField] private float maxLifetime = 2f;
    [SerializeField] private float radius = 0.08f;
    [SerializeField] private int damage = 1;
    [Tooltip("Layers the bullet stops on (road blocks + obstacles).")]
    [SerializeField] private LayerMask hitMask = 1 << 6;
    [Tooltip("Spark / impact effect played at the hit point (pooled).")]
    [SerializeField] private GameObject impactEffect;
    [Tooltip("Hard, unbreakable surfaces (stone, metal): sparks bounce off along the reflected direction. Empty = impactEffect.")]
    [SerializeField] private GameObject ricochetEffect;

    private IObjectPool<Projectile> pool;
    private TrailRenderer trail;
    private Vector3 direction;
    private float travelled;
    private float age;
    private bool released;

    public float Speed { get { return speed; } }

    private void Awake()
    {
        trail = GetComponentInChildren<TrailRenderer>();
        EffectPool.Prewarm(impactEffect, 4);
        EffectPool.Prewarm(ricochetEffect, 4);
    }

    public void SetPool(IObjectPool<Projectile> owner)
    {
        pool = owner;
    }

    public void Launch(Vector3 position, Vector3 dir)
    {
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(dir));
        direction = dir.normalized;
        travelled = 0f;
        age = 0f;
        released = false;
        if (trail != null) trail.Clear();
    }

    private void Update()
    {
        float step = speed * Time.deltaTime;
        if (step <= 0f) return;

        Vector3 origin = transform.position;
        RaycastHit hit;
        if (Physics.SphereCast(origin, radius, direction, out hit, step, hitMask, QueryTriggerInteraction.Ignore))
        {
            OnHit(hit);
            return;
        }

        transform.position = origin + direction * step;
        travelled += step;
        age += Time.deltaTime;
        if (travelled >= maxRange || age >= maxLifetime)
        {
            ReturnToPool();
        }
    }

    private void OnHit(RaycastHit hit)
    {
        Vector3 normal = hit.normal.sqrMagnitude > 0f ? hit.normal : -direction;

        IDestructible target;
        if (DestructibleRegistry.TryGet(hit.collider, out target) && target.IsAlive)
        {
            target.TakeDamage(damage, hit.point, direction);
            EffectPool.Spawn(impactEffect, hit.point, Quaternion.LookRotation(normal));
        }
        else
        {
            // Unbreakable: the bullet glances off, sparks fly along the bounce direction
            Vector3 bounce = Vector3.Reflect(direction, normal);
            GameObject effect = ricochetEffect != null ? ricochetEffect : impactEffect;
            EffectPool.Spawn(effect, hit.point, Quaternion.LookRotation(bounce.sqrMagnitude > 0f ? bounce : normal));
        }
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (released) return;
        released = true;

        if (pool != null)
        {
            pool.Release(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
