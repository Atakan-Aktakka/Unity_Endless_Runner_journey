using UnityEngine;

// Lives on the root of a pooled effect (next to its main ParticleSystem)
public class PooledEffect : MonoBehaviour
{
    private ParticleSystem particles;
    private Light flashLight;
    private float lightIntensity;
    private float lightTimer;
    [SerializeField] private float lightDuration = 0.12f;

    public GameObject Prefab { get; private set; }

    internal void Init(GameObject prefab)
    {
        Prefab = prefab;
        particles = GetComponent<ParticleSystem>();
        flashLight = GetComponentInChildren<Light>(true);
        if (flashLight != null) lightIntensity = flashLight.intensity;

        if (particles != null)
        {
            ParticleSystem.MainModule main = particles.main;
            main.stopAction = ParticleSystemStopAction.Callback;
            main.playOnAwake = false;
        }
    }

    internal void Play()
    {
        if (particles != null)
        {
            particles.Clear(true);
            particles.Play(true);
        }
        if (flashLight != null)
        {
            flashLight.intensity = lightIntensity;
            flashLight.enabled = true;
            lightTimer = lightDuration;
        }
    }

    private void Update()
    {
        if (flashLight == null || lightTimer <= 0f) return;
        lightTimer -= Time.deltaTime;
        flashLight.intensity = lightIntensity * Mathf.Clamp01(lightTimer / lightDuration);
        if (lightTimer <= 0f) flashLight.enabled = false;
    }

    // Called by Unity when the ParticleSystem finished (Stop Action = Callback)
    private void OnParticleSystemStopped()
    {
        EffectPool.Release(this);
    }
}
