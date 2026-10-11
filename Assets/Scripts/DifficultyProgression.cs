using UnityEngine;

// Single source of the difficulty curve: distance run -> difficulty 0..1 -> run speed.
// - Sets the hero's run speed every frame (startSpeed -> maxSpeed along the curve).
// - BlockManager asks DifficultyAt(x) / SpeedAt(x) for the road it generates ahead, so obstacle
//   density, platform lengths and gap sizes follow the same curve and landing zones are sized for
//   the speed the hero will actually have when it arrives there.
public class DifficultyProgression : MonoBehaviour
{
    [SerializeField] private HeroCharacterController hero;

    [Header("Speed (m/s)")]
    [SerializeField] private float startSpeed = 8f;
    [SerializeField] private float maxSpeed = 18f;

    [Header("Curve")]
    [Tooltip("Flat start (m) before the difficulty starts to rise.")]
    [SerializeField] private float warmupDistance = 20f;
    [Tooltip("Distance (m) after the warmup until difficulty reaches 1.")]
    [SerializeField] private float rampDistance = 1500f;
    [Tooltip("x = ramp progress 0..1, y = difficulty 0..1. Default rises fast first, then flattens.")]
    [SerializeField] private AnimationCurve curve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 1.8f), new Keyframe(1f, 1f, 0.2f, 0f));

    private Transform heroTransform;
    private float originX;

    public float StartSpeed { get { return startSpeed; } }
    public float MaxSpeed { get { return maxSpeed; } }
    public float CurrentSpeed { get { return heroTransform != null ? SpeedAt(heroTransform.position.x) : startSpeed; } }
    public float CurrentDifficulty { get { return heroTransform != null ? DifficultyAt(heroTransform.position.x) : 0f; } }

    private void Awake()
    {
        if (hero == null) hero = GetComponent<HeroCharacterController>();
        if (hero == null) hero = FindFirstObjectByType<HeroCharacterController>();
        if (hero == null)
        {
            Debug.LogError("DifficultyProgression: no HeroCharacterController found.", this);
            enabled = false;
            return;
        }

        heroTransform = hero.transform;
        originX = heroTransform.position.x;
        hero.RunSpeed = startSpeed;
    }

    private void Update()
    {
        hero.RunSpeed = SpeedAt(heroTransform.position.x);
    }

    public float DifficultyAt(float x)
    {
        float progress = (x - originX - warmupDistance) / Mathf.Max(1f, rampDistance);
        return Mathf.Clamp01(curve.Evaluate(Mathf.Clamp01(progress)));
    }

    public float SpeedAt(float x)
    {
        return Mathf.Lerp(startSpeed, maxSpeed, DifficultyAt(x));
    }
}
