using UnityEngine;

// Lightweight 2D SFX player for the run scene (one per scene, on any GameObject).
// - Gunshot: WeaponController.Fired
// - Explosion: DestructibleObstacle.Destroyed
// - Jump: HeroCharacterController calls PlayJump
// One-shots go through pooled AudioSources, so rapid fire never cuts off the previous shot
// and nothing is instantiated at runtime.
public class SoundManager : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] private AudioClip gunshotClip;
    [SerializeField] private AudioClip explosionClip;
    [Tooltip("Empty = the clip assigned on the hero is used.")]
    [SerializeField] private AudioClip jumpClip;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float gunshotVolume = 0.45f;
    [SerializeField, Range(0f, 1f)] private float explosionVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float jumpVolume = 0.5f;

    [Header("Variation")]
    [Tooltip("Random pitch range so repeated shots do not sound machine-gun identical.")]
    [SerializeField] private Vector2 gunshotPitch = new Vector2(0.93f, 1.07f);
    [SerializeField] private Vector2 explosionPitch = new Vector2(0.9f, 1.05f);
    [SerializeField, Min(1)] private int voices = 6;

    private static SoundManager instance;
    private AudioSource[] sources;
    private int nextSource;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("SoundManager: a second instance was found and disabled.", this);
            enabled = false;
            return;
        }
        instance = this;

        sources = new AudioSource[voices];
        for (int i = 0; i < voices; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            sources[i] = source;
        }
    }

    private void OnEnable()
    {
        WeaponController.Fired += OnFired;
        DestructibleObstacle.Destroyed += OnObstacleDestroyed;
    }

    private void OnDisable()
    {
        WeaponController.Fired -= OnFired;
        DestructibleObstacle.Destroyed -= OnObstacleDestroyed;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Falls back to the hero's own clip / a 3D one-shot when the scene has no SoundManager
    public static void PlayJump(AudioClip fallbackClip, Vector3 position)
    {
        if (instance != null && instance.enabled)
        {
            AudioClip clip = instance.jumpClip != null ? instance.jumpClip : fallbackClip;
            instance.Play(clip, instance.jumpVolume, 1f);
        }
        else if (fallbackClip != null)
        {
            AudioSource.PlayClipAtPoint(fallbackClip, position, 0.5f);
        }
    }

    private void OnFired()
    {
        Play(gunshotClip, gunshotVolume, Random.Range(gunshotPitch.x, gunshotPitch.y));
    }

    private void OnObstacleDestroyed(DestructibleObstacle obstacle)
    {
        Play(explosionClip, explosionVolume, Random.Range(explosionPitch.x, explosionPitch.y));
    }

    private void Play(AudioClip clip, float volume, float pitch)
    {
        if (clip == null || sources == null) return;

        // Round robin: each voice keeps its own pitch, PlayOneShot overlaps on the same voice if needed
        AudioSource source = sources[nextSource];
        nextSource = (nextSource + 1) % sources.Length;
        source.pitch = pitch;
        source.PlayOneShot(clip, volume * masterVolume);
    }
}
