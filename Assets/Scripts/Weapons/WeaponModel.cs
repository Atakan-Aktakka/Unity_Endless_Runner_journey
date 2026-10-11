using UnityEngine;

// Root of a weapon prefab: tells WeaponController where the muzzle and its flash are
public class WeaponModel : MonoBehaviour
{
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private Light muzzleLight;

    public Transform MuzzlePoint { get { return muzzlePoint != null ? muzzlePoint : transform; } }
    public ParticleSystem MuzzleFlash { get { return muzzleFlash; } }
    public Light MuzzleLight { get { return muzzleLight; } }
}
