using UnityEngine;

// Interactive first-run tutorial (PlayerPrefs "TutorialCompleted" == 0):
//   1) Fire: a shootable target follows the safe start. The game slows down (timeScale 0.1), the left half
//      lights up with a hand + "EKRANIN SOLUNA DOKUN: ATEŞ ET". Close to the target the hero stops and waits
//      until the target is destroyed.
//   2) Jump: right after that a pit follows. Same slow motion with the right half + "ZIPLA"; the hero waits
//      at the edge until it jumps. Jumping completes the tutorial and stores TutorialCompleted = 1.
// BlockManager builds the fixed tutorial layout (RequestTutorial is called here in Awake, before its Start).
// Later runs skip all of this and start right away.
public class TutorialManager : MonoBehaviour
{
    public const string PrefKey = "TutorialCompleted";

    private enum Stage { Off, WaitFire, Fire, WaitJump, Jump, Done }

    [SerializeField] private BlockManager blockManager;
    [SerializeField] private HeroCharacterController hero;

    [Header("UI (built by Tools > Journey > Build Tutorial UI)")]
    [SerializeField] private GameObject fireHint;
    [SerializeField] private GameObject jumpHint;
    [SerializeField] private RectTransform fireHand;
    [SerializeField] private RectTransform jumpHand;
    [SerializeField] private RectTransform fireRipple;
    [SerializeField] private RectTransform jumpRipple;

    [Header("Timing")]
    [SerializeField, Range(0.02f, 1f)] private float slowTimeScale = 0.1f;
    [Tooltip("Slow motion starts this far (m) before the target / the pit.")]
    [SerializeField] private float triggerDistance = 6f;
    [Tooltip("The hero stops this far (m) before the target until it is destroyed.")]
    [SerializeField] private float targetHoldDistance = 3f;
    [Tooltip("The hero stops this far (m) before the pit edge until it jumps.")]
    [SerializeField] private float edgeHoldDistance = 0.6f;

    [Header("Debug")]
    [Tooltip("Editor: run the tutorial even if it was completed.")]
    [SerializeField] private bool forceInEditor;

    private Stage stage = Stage.Off;
    private Transform heroTransform;
    private DestructibleObstacle target;
    private float targetX;
    private Vector2 fireHandRest;
    private Vector2 jumpHandRest;
    private bool slowed;
    private UnityEngine.UI.Graphic fireRippleGraphic;
    private UnityEngine.UI.Graphic jumpRippleGraphic;

    public static bool IsCompleted
    {
        get { return PlayerPrefs.GetInt(PrefKey, 0) == 1; }
    }

    // "How to play" panel: the next game shows the tutorial again
    public static void ResetProgress()
    {
        PlayerPrefs.DeleteKey(PrefKey);
        PlayerPrefs.Save();
    }

    private void Awake()
    {
        SetHint(fireHint, false);
        SetHint(jumpHint, false);
        if (fireHand != null) fireHandRest = fireHand.anchoredPosition;
        if (jumpHand != null) jumpHandRest = jumpHand.anchoredPosition;
        if (fireRipple != null) fireRippleGraphic = fireRipple.GetComponent<UnityEngine.UI.Graphic>();
        if (jumpRipple != null) jumpRippleGraphic = jumpRipple.GetComponent<UnityEngine.UI.Graphic>();

        bool force = forceInEditor && Application.isEditor;
        if (IsCompleted && !force)
        {
            enabled = false;
            return;
        }

        if (blockManager == null) blockManager = FindFirstObjectByType<BlockManager>();
        if (hero == null) hero = FindFirstObjectByType<HeroCharacterController>();
        if (blockManager == null || hero == null)
        {
            Debug.LogError("TutorialManager: BlockManager or Hero missing.", this);
            enabled = false;
            return;
        }

        heroTransform = hero.transform;
        blockManager.RequestTutorial();

        stage = Stage.WaitFire;
    }

    private void OnEnable()
    {
        DestructibleObstacle.Destroyed += OnObstacleDestroyed;
        if (hero != null) hero.Jumped += OnHeroJumped;
    }

    private System.Collections.IEnumerator Start()
    {
        // The layout exists after BlockManager.Start, whose order relative to this Start is undefined
        yield return null;
        target = blockManager.TutorialTarget;
        if (target == null)
        {
            Finish(false);
            yield break;
        }
        targetX = target.transform.position.x;
    }

    private void OnDisable()
    {
        DestructibleObstacle.Destroyed -= OnObstacleDestroyed;
        if (hero != null) hero.Jumped -= OnHeroJumped;
        if (stage != Stage.Off && stage != Stage.Done)
        {
            Release();
        }
    }

    private void Update()
    {
        if (HeroManager.gameOver)
        {
            // Died during the tutorial: hide everything, HeroManager owns the timeScale now
            SetHint(fireHint, false);
            SetHint(jumpHint, false);
            slowed = false;
            if (hero != null) hero.MovementLocked = false;
            stage = Stage.Off;
            enabled = false;
            return;
        }

        float heroX = heroTransform.position.x;
        switch (stage)
        {
            case Stage.WaitFire:
                if (target == null)
                {
                    break; // layout not read yet (first frame)
                }
                if (!target.IsAlive)
                {
                    stage = Stage.WaitJump; // shot early: nothing to teach here
                }
                else if (heroX >= targetX - triggerDistance)
                {
                    stage = Stage.Fire;
                    SetSlow(true);
                    SetHint(fireHint, true);
                }
                break;

            case Stage.Fire:
                hero.MovementLocked = heroX >= targetX - targetHoldDistance;
                break;

            case Stage.WaitJump:
                if (heroX >= blockManager.TutorialGapStartX - triggerDistance)
                {
                    stage = Stage.Jump;
                    SetSlow(true);
                    SetHint(jumpHint, true);
                }
                break;

            case Stage.Jump:
                hero.MovementLocked = heroX >= blockManager.TutorialGapStartX - edgeHoldDistance;
                break;
        }

        AnimateHand(fireHand, fireRippleGraphic, fireHandRest);
        AnimateHand(jumpHand, jumpRippleGraphic, jumpHandRest);
    }

    private void OnObstacleDestroyed(DestructibleObstacle obstacle)
    {
        if (obstacle != target) return;

        if (stage == Stage.Fire)
        {
            Release();
            stage = Stage.WaitJump;
        }
        else if (stage == Stage.WaitFire)
        {
            stage = Stage.WaitJump;
        }
    }

    private void OnHeroJumped()
    {
        if (stage == Stage.Jump)
        {
            Finish(true);
        }
    }

    private void Finish(bool completed)
    {
        Release();
        stage = Stage.Done;
        if (completed)
        {
            PlayerPrefs.SetInt(PrefKey, 1);
            PlayerPrefs.Save();
        }
        enabled = false;
    }

    // Back to normal speed, hero runs again, hints hidden
    private void Release()
    {
        SetSlow(false);
        if (hero != null) hero.MovementLocked = false;
        SetHint(fireHint, false);
        SetHint(jumpHint, false);
    }

    // Time.timeScale stays in sync with HeroManager: never touched once the game is over
    private void SetSlow(bool slow)
    {
        if (HeroManager.gameOver || slowed == slow) return;
        slowed = slow;
        Time.timeScale = slow ? slowTimeScale : 1f;
    }

    private static void SetHint(GameObject hint, bool visible)
    {
        if (hint != null && hint.activeSelf != visible) hint.SetActive(visible);
    }

    // Tap motion in real time (the game itself runs in slow motion)
    private static void AnimateHand(RectTransform hand, UnityEngine.UI.Graphic ripple, Vector2 rest)
    {
        if (hand == null || !hand.gameObject.activeInHierarchy) return;

        float t = Time.unscaledTime;
        float tap = Mathf.Abs(Mathf.Sin(t * 3.5f));
        hand.anchoredPosition = rest + new Vector2(0f, tap * 18f);
        hand.localScale = Vector3.one * (1f - (1f - tap) * 0.08f);

        if (ripple != null)
        {
            float phase = Mathf.Repeat(t * 1.1f, 1f);
            ripple.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.4f, phase);
            Color c = ripple.color;
            c.a = 1f - phase;
            ripple.color = c;
        }
    }
}
