using System.Collections.Generic;
using UnityEngine;

// Anything a projectile can damage
public interface IDestructible
{
    bool IsAlive { get; }
    void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitDirection);
}

// Collider -> IDestructible lookup filled by the destructibles themselves,
// so projectiles never call GetComponent while flying
public static class DestructibleRegistry
{
    private static readonly Dictionary<Collider, IDestructible> map = new Dictionary<Collider, IDestructible>();

    public static void Register(Collider col, IDestructible target)
    {
        if (col != null) map[col] = target;
    }

    public static void Unregister(Collider col)
    {
        if (col != null) map.Remove(col);
    }

    public static bool TryGet(Collider col, out IDestructible target)
    {
        return map.TryGetValue(col, out target);
    }
}
