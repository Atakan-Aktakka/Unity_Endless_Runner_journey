using System.Collections.Generic;
using UnityEngine;

// Pool for one-shot particle effects (hit sparks, explosions).
// An effect returns itself when its ParticleSystem stops (Stop Action = Callback).
public static class EffectPool
{
    private static readonly Dictionary<GameObject, Stack<PooledEffect>> pools = new Dictionary<GameObject, Stack<PooledEffect>>();
    private static Transform root;

    public static void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null) return;
        Stack<PooledEffect> pool = GetPool(prefab);
        for (int i = pool.Count; i < count; i++)
        {
            PooledEffect effect = Create(prefab);
            effect.gameObject.SetActive(false);
            pool.Push(effect);
        }
    }

    public static void Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return;

        Stack<PooledEffect> pool = GetPool(prefab);
        PooledEffect effect = null;
        // Objects of an unloaded scene are destroyed but still in the stack
        while (pool.Count > 0 && effect == null)
        {
            effect = pool.Pop();
        }
        if (effect == null)
        {
            effect = Create(prefab);
        }

        effect.transform.SetPositionAndRotation(position, rotation);
        effect.gameObject.SetActive(true);
        effect.Play();
    }

    internal static void Release(PooledEffect effect)
    {
        effect.gameObject.SetActive(false);
        GetPool(effect.Prefab).Push(effect);
    }

    private static Stack<PooledEffect> GetPool(GameObject prefab)
    {
        Stack<PooledEffect> pool;
        if (!pools.TryGetValue(prefab, out pool))
        {
            pool = new Stack<PooledEffect>();
            pools.Add(prefab, pool);
        }
        return pool;
    }

    private static PooledEffect Create(GameObject prefab)
    {
        if (root == null)
        {
            root = new GameObject("[EffectPool]").transform;
        }

        GameObject go = Object.Instantiate(prefab, root);
        PooledEffect effect = go.GetComponent<PooledEffect>();
        if (effect == null)
        {
            effect = go.AddComponent<PooledEffect>();
        }
        effect.Init(prefab);
        return effect;
    }
}
