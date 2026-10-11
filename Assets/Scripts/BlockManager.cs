using System.Collections.Generic;
using UnityEngine;

// Procedural endless road along +X, built as a repeating section cycle:
//   Town (the theme's backdrop: castle / city) - flat road, only small obstacles (crates)
//   Wild (open nature)                         - real pits without bridges, up / down steps
//   Gate (arrival)                             - flat run-in; the next theme's backdrop + optional gate start here
//   -> Town of the next theme -> ...
// - Road Z = hero Z, so the hero always runs on the road and not in front of / behind it.
// - Backdrops are pushed behind the road by backgroundGap; backdrop meshes that still reach into or in
//   front of the road (waterfalls hanging over the edge) are hidden.
// - Every platform gets a cliff column below it, so blocks never float over the void.
// - Gaps and steps are derived from the hero's real jump physics.
// - Blocks, obstacles, cliffs, gates and props are recycled through an object pool.
public class BlockManager : MonoBehaviour
{
    public enum Phase { Town, Wild, Gate }

    [System.Serializable]
    private class ThemeSettings
    {
        public string themeName = "Forest";
        [Tooltip("Multiplied with the block / cliff materials while this theme is active.")]
        public Color blockTint = Color.white;
        [Range(0f, 1f)] public float rockBlockChance = 0.2f;

        [Header("Edge Props")]
        public GameObject[] propPrefabs;
        [Range(0f, 1f)] public float propChancePerBlock = 0.35f;

        [Header("Gate (optional)")]
        [Tooltip("Spawned across the road where this theme's town begins (arch, gate, banner...). Colliders are disabled.")]
        public GameObject gatePrefab;
        public Vector3 gateOffset;
        public Vector3 gateRotation;

        [Header("Backdrop (optional)")]
        [Tooltip("Scene objects of this environment; they are moved to the start of each new town of this theme.")]
        public Transform[] backdropParts;
        [Tooltip("x = road X where the backdrop currently starts, y = road top height it was built for.")]
        public Vector2 backdropAnchor;

        [System.NonSerialized] public float backdropLength;
    }

    private enum PieceKind { Block, Obstacle, Prop, Gate, Cliff }

    // Measured once per prefab (with the rotation / scale it is spawned with)
    private class PieceInfo
    {
        public float width;
        public float halfDepth;
        public Vector3 boundsCenter; // relative to the pivot
        public float bottom;         // relative to the pivot
    }

    private class PooledObject
    {
        public GameObject prefab;
        public GameObject gameObject;
        public Transform transform;
        public Renderer[] renderers;
        public Material[][][] themedMaterials; // [theme][renderer] -> materials (blocks / obstacles / cliffs)
        public int appliedTheme = -1;
    }

    private struct ActivePiece
    {
        public PooledObject item;
        public float endX;
    }

    private enum Pattern { Flat, Gap, StepUp, StepDown }

    [System.Serializable]
    private class ObstacleType
    {
        public string name = "Crate";
        public GameObject prefab;
        [Tooltip("Scaled to this height (m). Always kept below the hero's jump height.")]
        public float height = 1f;
        [Tooltip("Spawn weight (x = easy, y = hard).")]
        public Vector2 weight = new Vector2(1f, 1f);
        [Tooltip("Can be shot (prefab has DestructibleObstacle). Used for the shoot + pit combo and the tutorial.")]
        public bool destructible;
    }

    [Header("References")]
    [SerializeField] private HeroCharacterController hero;
    [SerializeField] private GameObject dirtBlockPrefab;
    [SerializeField] private GameObject rockBlockPrefab;
    [Tooltip("Fallback obstacle when Obstacle Types is empty.")]
    [SerializeField] private GameObject crateBlockPrefab;

    [Header("Obstacle Types (weighted)")]
    [Tooltip("Crate / explosive barrel (destructible) and stone barricade (jump only) etc.")]
    [SerializeField] private ObstacleType[] obstacleTypes;
    [Tooltip("Chance (x = easy, y = hard) that a wild platform ends with a destructible obstacle right before a pit.")]
    [SerializeField] private Vector2 comboChance = new Vector2(0f, 0.35f);
    [Tooltip("Combos start at this difficulty (0..1).")]
    [SerializeField, Range(0f, 1f)] private float comboMinDifficulty = 0.3f;

    [Header("Tutorial Layout")]
    [Tooltip("Flat blocks between the tutorial target and the tutorial pit.")]
    [SerializeField] private int tutorialRunUpBlocks = 4;
    [SerializeField] private float tutorialGap = 2f;
    [Tooltip("Obstacle type (by name) shot in the tutorial; falls back to any destructible type.")]
    [SerializeField] private string tutorialTargetName = "ExplosiveBarrel";

    [Header("Road Layout")]
    [SerializeField] private float startX = -8f;
    [SerializeField] private float startHeight = 0.4f;
    [Tooltip("Road center Z = the hero's Z at start (recommended). Off = laneZ below is used.")]
    [SerializeField] private bool alignRoadToHero = true;
    [SerializeField] private float laneZ = 1.5f;
    [SerializeField] private float stepHeight = 1f;
    [SerializeField] private int maxStepsUp = 3;
    [SerializeField] private int maxStepsDown = 2;
    [Tooltip("Layer used by HeroCharacterController.groundLayers.")]
    [SerializeField] private int groundLayer = 6;

    [Header("Sections (blocks)")]
    [SerializeField] private Vector2Int townBlocks = new Vector2Int(10, 15);
    [SerializeField] private Vector2Int wildBlocks = new Vector2Int(15, 20);
    [SerializeField, Min(1)] private int gateBlocks = 4;
    [Tooltip("The wild section lasts at least until the current backdrop ends, so two backdrops never overlap.")]
    [SerializeField] private bool fitWildToBackdrop = true;
    [SerializeField] private Vector2 townObstacleChance = new Vector2(0.12f, 0.35f);
    [SerializeField] private Vector2 wildObstacleChance = new Vector2(0.05f, 0.2f);

    [Header("Spawn / Despawn")]
    [Tooltip("Road is generated this far ahead of the hero (keep it beyond the camera view).")]
    [SerializeField] private float spawnAheadDistance = 45f;
    [SerializeField] private int minBlocksAhead = 6;
    [Tooltip("Pieces this far behind the hero are returned to the pool.")]
    [SerializeField] private float despawnBehindDistance = 10f;
    [SerializeField] private int prewarmBlocksPerPrefab = 30;
    [SerializeField] private int prewarmPerProp = 3;

    [Header("Difficulty")]
    [Tooltip("Optional (auto-found): owns the speed curve. Empty = the distance ramp below is used.")]
    [SerializeField] private DifficultyProgression difficultyProgression;
    [Tooltip("First blocks of the first town are free of obstacles.")]
    [SerializeField] private int safeStartBlocks = 10;
    [Tooltip("Distance (m) after the safe start until difficulty reaches 1.")]
    [SerializeField] private float difficultyRampDistance = 800f;
    [SerializeField] private float maxRunSpeed = 12f;

    [Header("Wild Pattern Weights (x = easy, y = hard)")]
    [SerializeField] private Vector2 wildFlatWeight = new Vector2(0.15f, 0.05f);
    [SerializeField] private Vector2 wildGapWeight = new Vector2(0.35f, 0.45f);
    [SerializeField] private Vector2 wildStepUpWeight = new Vector2(0.25f, 0.25f);
    [SerializeField] private Vector2 wildStepDownWeight = new Vector2(0.15f, 0.20f);
    [Tooltip("Chance that a step up/down also has a gap.")]
    [SerializeField] private Vector2 gapOnStepChance = new Vector2(0.1f, 0.6f);
    [SerializeField] private Vector2Int wildPlatformBlocksEasy = new Vector2Int(3, 6);
    [SerializeField] private Vector2Int wildPlatformBlocksHard = new Vector2Int(2, 4);

    [Header("Gaps & Obstacles")]
    [SerializeField] private float minGap = 1.5f;
    [SerializeField] private float maxGap = 3.5f;
    [Tooltip("Fraction of the theoretical jump distance a gap may use (timing tolerance).")]
    [SerializeField, Range(0.4f, 1f)] private float jumpSafetyFactor = 0.7f;
    [Tooltip("maxGap is meant for the start speed and grows with the run speed (still capped by the jump physics).")]
    [SerializeField] private bool scaleGapsWithSpeed = true;
    [Tooltip("Height of the obstacle crate. It sits on the road surface and must stay below jumpHeight.")]
    [SerializeField] private float obstacleHeight = 1f;
    [Tooltip("Run-up time (s) the player gets after landing before the next hazard.")]
    [SerializeField] private float reactionTime = 0.25f;
    [Tooltip("Extra speed margin for landing zones, the hero is a bit faster when it arrives.")]
    [SerializeField] private float landingSpeedMargin = 1.1f;

    [Header("Jump Limits")]
    [Tooltip("MaxGapDistance = RunSpeed * (2 * JumpVelocity / Gravity) * gapSafetyFactor")]
    [SerializeField, Range(0.4f, 1f)] private float gapSafetyFactor = 0.75f;
    [Tooltip("Step up height may use at most this fraction of the hero's jump height.")]
    [SerializeField, Range(0.3f, 0.95f)] private float stepSafetyFactor = 0.8f;

    [Header("Aggressive Phase (distance from the hero's start, m)")]
    [Tooltip("From here on gaps are drawn close to the jump limit and landing platforms get shorter.")]
    [SerializeField] private float hardGapDistance = 300f;
    [Tooltip("Hardest gap = this fraction of MaxGapDistance (still capped by the reachable jump distance).")]
    [SerializeField, Range(0.5f, 0.95f)] private float hardGapFactor = 0.85f;
    [Tooltip("Easiest gap in the aggressive phase, fraction of MaxGapDistance.")]
    [SerializeField, Range(0.3f, 0.9f)] private float hardGapMinFactor = 0.6f;
    [Tooltip("Random platform length (blocks) in the aggressive phase; never shorter than the landing zone.")]
    [SerializeField] private Vector2Int hardPlatformBlocks = new Vector2Int(2, 3);
    [Tooltip("Reaction time multiplier for landing zones in the aggressive phase (shorter landing platforms).")]
    [SerializeField, Range(0.3f, 1f)] private float hardLandingReactionScale = 0.6f;

    [Header("Reflex Combos")]
    [Tooltip("Shoot + jump pair (destructible target, then a solid barricade right behind it) starts here (m).")]
    [SerializeField] private float reflexPairDistance = 300f;
    [Tooltip("Chance (x = easy, y = hard) that a placed obstacle becomes a shoot + jump pair.")]
    [SerializeField] private Vector2 reflexPairChance = new Vector2(0.3f, 0.55f);
    [Tooltip("Free space (m) between the target's back and the barricade's front.")]
    [SerializeField] private float reflexPairSpacing = 1f;
    [Tooltip("Obstacle type (by name) used as the pair's target; falls back to any destructible type.")]
    [SerializeField] private string reflexPairTargetName = "Crate";
    [Tooltip("Chance that a step up in the aggressive phase is followed by a gap + a second step up.")]
    [SerializeField, Range(0f, 1f)] private float doubleStepChance = 0.4f;

    [Header("Dense Obstacles")]
    [Tooltip("From here on obstacles come in quicker succession (m).")]
    [SerializeField] private float denseDistance = 500f;
    [SerializeField, Range(0f, 0.5f)] private float denseChanceBonus = 0.2f;
    [SerializeField, Range(0.1f, 1f)] private float denseMaxChance = 0.7f;
    [Tooltip("Reaction time multiplier for the spacing between obstacles (shorter free runs).")]
    [SerializeField, Range(0.3f, 1f)] private float denseReactionScale = 0.6f;

    [Header("Background (Z)")]
    [Tooltip("Distance (m) between the road's back edge and the front edge of every backdrop.")]
    [SerializeField] private float backgroundGap = 2f;
    [Tooltip("Hide backdrop meshes that still reach into / in front of the road (waterfalls over the edge).")]
    [SerializeField] private bool hideBackdropInFrontOfRoad = true;
    [SerializeField] private bool disableBackdropColliders = true;

    [Header("Cliff Base")]
    [SerializeField] private bool buildCliffs = true;
    [Tooltip("Empty = a URP Lit material in cliffColor is created at runtime.")]
    [SerializeField] private Material cliffMaterial;
    [SerializeField] private Color cliffColor = new Color(0.42f, 0.25f, 0.14f);
    [SerializeField] private float cliffBottomY = -25f;
    [Tooltip("Cliff top sits this far below the road surface (hidden inside the block).")]
    [SerializeField] private float cliffTopInset = 0.3f;

    [Header("Themes (Forest, Desert, Dungeon, Winter)")]
    [SerializeField] private ThemeSettings[] themes;
    [Tooltip("Distance of the props' center behind the lane.")]
    [SerializeField] private float propDepthOffset = 0.9f;
    [Tooltip("Props never reach closer than this to the lane center (keeps the running lane free).")]
    [SerializeField] private float propLaneClearance = 0.55f;
    [SerializeField] private float propSpacing = 0.2f;

    private readonly Dictionary<GameObject, Stack<PooledObject>> pools = new Dictionary<GameObject, Stack<PooledObject>>();
    private readonly Dictionary<GameObject, PieceInfo> pieceInfos = new Dictionary<GameObject, PieceInfo>();
    private readonly Queue<ActivePiece> activePieces = new Queue<ActivePiece>();
    private readonly List<Material> createdMaterials = new List<Material>();
    private readonly Dictionary<Material, Material[]> tintedMaterialCache = new Dictionary<Material, Material[]>();

    private Transform heroTransform;
    private GameObject cliffTemplate;
    private float baseRunSpeed;
    private float blockWidth;
    private float roadWidth = 2f;
    private readonly Dictionary<GameObject, float> obstacleScales = new Dictionary<GameObject, float>();
    private float nextX;
    private float currentHeight;
    private float lastPropEndX;
    private int safeBlocksRemaining;
    private int currentTheme;

    private Phase phase;
    private int phaseBlocksLeft;
    private float phaseMinEndX;
    private float backdropStartX;
    private bool lastWildWasFlat;
    private bool forceGapNext;
    private bool forceStepGapNext;
    private bool tutorialRequested;
    private float heroStartX;

    // Filled when TutorialManager requested the scripted start
    public DestructibleObstacle TutorialTarget { get; private set; }
    public float TutorialGapStartX { get; private set; }

    // Read by HeroLaneController / UI
    public float LaneZ { get { return laneZ; } }
    public float RoadWidth { get { return roadWidth; } }
    public Phase GenerationPhase { get { return phase; } }
    public float MaxJumpHeight { get { return hero.JumpHeight; } }

    // Flat jump: airtime = 2 * v0 / g, v0 = sqrt(2 * g * jumpHeight)
    public float MaxGapDistance
    {
        get
        {
            float g = Mathf.Abs(hero.Gravity);
            float jumpVelocity = Mathf.Sqrt(2f * g * hero.JumpHeight);
            return hero.RunSpeed * (2f * jumpVelocity / g) * gapSafetyFactor;
        }
    }

    private void Awake()
    {
        if (hero == null)
        {
            hero = FindFirstObjectByType<HeroCharacterController>();
        }
        if (hero == null || dirtBlockPrefab == null)
        {
            Debug.LogError("BlockManager: Hero or dirt block prefab is not assigned.", this);
            enabled = false;
            return;
        }

        heroTransform = hero.transform;
        heroStartX = heroTransform.position.x;
        if (difficultyProgression == null)
        {
            difficultyProgression = FindFirstObjectByType<DifficultyProgression>();
        }
        baseRunSpeed = difficultyProgression != null ? difficultyProgression.StartSpeed : hero.RunSpeed;
        obstacleHeight = Mathf.Min(obstacleHeight, hero.JumpHeight * 0.75f);
        SetupObstacleTypes();
        // A step up can never be higher than the hero can jump (minus landing clearance)
        stepHeight = Mathf.Min(stepHeight, hero.JumpHeight * stepSafetyFactor);

        // Road and hero share one Z plane
        if (alignRoadToHero)
        {
            laneZ = heroTransform.position.z;
        }

        if (themes == null || themes.Length == 0)
        {
            themes = new[] { new ThemeSettings() };
        }

        CreateCliffTemplate();
        PrewarmPools();
        PieceInfo blockInfo = GetInfo(dirtBlockPrefab, PieceKind.Block, dirtBlockPrefab.transform.rotation);
        blockWidth = blockInfo.width;
        roadWidth = blockInfo.halfDepth * 2f;

        // Needs the measured road depth
        PrepareBackdrops();
    }

    private void Start()
    {
        nextX = startX;
        currentHeight = startHeight;
        lastPropEndX = float.MinValue;
        safeBlocksRemaining = safeStartBlocks;
        currentTheme = 0;
        PlaceBackdrop(currentTheme, nextX, currentHeight);
        phase = Phase.Town;

        if (tutorialRequested)
        {
            BuildTutorialStart();
        }
        FillAhead();
    }

    // Called by TutorialManager in Awake (before Start builds the first road)
    public void RequestTutorial()
    {
        tutorialRequested = true;
    }

    // Safe run-in -> shootable target -> run-up -> pit -> landing, then the normal town continues
    private void BuildTutorialStart()
    {
        ObstacleType target = null;
        for (int i = 0; HasObstacles && i < obstacleTypes.Length; i++)
        {
            if (obstacleTypes[i].destructible && obstacleTypes[i].prefab != null && obstacleTypes[i].name == tutorialTargetName)
            {
                target = obstacleTypes[i];
            }
        }
        if (target == null) target = PickObstacle(0f, true);
        if (target == null)
        {
            Debug.LogWarning("BlockManager: no destructible obstacle type for the tutorial.", this);
            return;
        }

        int targetIndex = Mathf.Max(3, safeStartBlocks);
        int runUp = Mathf.Max(2, tutorialRunUpBlocks);
        int count = targetIndex + 1 + runUp;
        float platformStart = nextX;
        BuildPlatform(count, count, 0f);

        float targetCenter = platformStart + (targetIndex + 0.5f) * blockWidth;
        PooledObject item = SpawnObstacle(target, targetCenter, targetCenter + blockWidth * 0.5f);
        TutorialTarget = item != null ? item.gameObject.GetComponent<DestructibleObstacle>() : null;
        TutorialGapStartX = nextX;

        // Comfortable pit at start speed, then a long flat landing
        float gap = Mathf.Min(tutorialGap, MaxGapDistance * 0.8f);
        nextX += gap;
        BuildPlatform(GetClearBlocks(0f, gap) + 3, 99, 0f);
        safeBlocksRemaining = 2;
    }

    private void Update()
    {
        float heroX = heroTransform.position.x;

        // DifficultyProgression drives the speed itself
        if (difficultyProgression == null)
        {
            hero.RunSpeed = Mathf.Lerp(baseRunSpeed, Mathf.Max(baseRunSpeed, maxRunSpeed), GetDifficulty(heroX));
        }

        FillAhead();

        while (activePieces.Count > 0 && activePieces.Peek().endX < heroX - despawnBehindDistance)
        {
            Release(activePieces.Dequeue().item);
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < createdMaterials.Count; i++)
        {
            Destroy(createdMaterials[i]);
        }
    }

    // ---------------------------------------------------------------- section cycle

    private void FillAhead()
    {
        float target = heroTransform.position.x + Mathf.Max(spawnAheadDistance, minBlocksAhead * blockWidth);
        while (nextX < target)
        {
            GenerateSegment();
        }
    }

    private float GetDifficulty(float x)
    {
        if (difficultyProgression != null)
        {
            return difficultyProgression.DifficultyAt(x);
        }
        float warmupEnd = startX + safeStartBlocks * blockWidth;
        return Mathf.Clamp01((x - warmupEnd) / Mathf.Max(1f, difficultyRampDistance));
    }

    // Meters run from the hero's start position to x
    private float DistanceAt(float x)
    {
        return x - heroStartX;
    }

    private void GenerateSegment()
    {
        switch (phase)
        {
            case Phase.Town: BuildTown(); break;
            case Phase.Wild: BuildWildPlatform(); break;
            case Phase.Gate: BuildGate(); break;
        }
    }

    // Phase 1: one long flat platform in front of the castle / city, small obstacles only
    private void BuildTown()
    {
        float difficulty = GetDifficulty(nextX);
        int count = Random.Range(townBlocks.x, townBlocks.y + 1);

        // Obstacles never in the first block(s): run-in after the gate / safe start of the game
        int firstObstacle = Mathf.Max(1, safeBlocksRemaining);
        safeBlocksRemaining = 0;
        count = Mathf.Max(count, firstObstacle + 2);

        float chance = Mathf.Lerp(townObstacleChance.x, townObstacleChance.y, difficulty);
        BuildPlatform(count, firstObstacle, chance);

        EnterWild();
    }

    private void EnterWild()
    {
        phase = Phase.Wild;
        phaseBlocksLeft = Random.Range(wildBlocks.x, wildBlocks.y + 1);
        // The wild section always opens with a pit or a step
        lastWildWasFlat = true;

        // Keep running through nature until the current backdrop is behind us
        phaseMinEndX = nextX;
        float length = themes[currentTheme].backdropLength;
        if (fitWildToBackdrop && length > 0f)
        {
            phaseMinEndX = backdropStartX + length;
        }
    }

    // Phase 2: platforms separated by real pits (no bridges) and up / down steps
    private void BuildWildPlatform()
    {
        if (phaseBlocksLeft <= 0 && nextX >= phaseMinEndX)
        {
            phase = Phase.Gate;
            BuildGate();
            return;
        }

        float difficulty = GetDifficulty(nextX);
        float gap = 0f;
        float targetHeight = currentHeight;
        Pattern pattern = PickPattern(difficulty);
        // Never two flat platforms in a row: the wild section must keep the player jumping
        if (pattern == Pattern.Flat && lastWildWasFlat)
        {
            pattern = Pattern.Gap;
        }
        // The previous platform ended with a shoot-me obstacle: the pit must follow
        if (forceGapNext)
        {
            pattern = Pattern.Gap;
            forceGapNext = false;
        }
        // Second half of a double step: step + gap + step
        bool secondStep = forceStepGapNext;
        if (secondStep)
        {
            pattern = Pattern.StepUp;
            forceStepGapNext = false;
        }
        lastWildWasFlat = pattern == Pattern.Flat;
        bool hard = DistanceAt(nextX) >= hardGapDistance;

        switch (pattern)
        {
            case Pattern.Gap:
                gap = GetGap(0f, difficulty);
                break;
            case Pattern.StepUp:
                targetHeight += stepHeight;
                break;
            case Pattern.StepDown:
                targetHeight -= stepHeight * Random.Range(1, 3);
                break;
        }

        float minHeight = startHeight - maxStepsDown * stepHeight;
        float maxHeight = startHeight + maxStepsUp * stepHeight;
        if (targetHeight > maxHeight + 0.01f)
        {
            targetHeight = currentHeight - stepHeight;
        }
        targetHeight = Mathf.Clamp(targetHeight, minHeight, maxHeight);

        float heightDelta = targetHeight - currentHeight;
        bool stepped = !Mathf.Approximately(heightDelta, 0f);
        if (stepped && (secondStep || Random.value < Mathf.Lerp(gapOnStepChance.x, gapOnStepChance.y, difficulty)))
        {
            gap = GetGap(heightDelta, difficulty);
        }

        // A jump started at the previous edge must land on this platform with time left to react.
        // Aggressive phase: less reaction time -> shorter landing platforms.
        int landingBlocks = 1;
        if (gap > 0f || stepped)
        {
            landingBlocks = GetClearBlocks(heightDelta, gap, hard ? hardLandingReactionScale : 1f);
        }

        int minBlocks = Mathf.RoundToInt(Mathf.Lerp(wildPlatformBlocksEasy.x, wildPlatformBlocksHard.x, difficulty));
        int maxBlocks = Mathf.RoundToInt(Mathf.Lerp(wildPlatformBlocksEasy.y, wildPlatformBlocksHard.y, difficulty));
        if (hard)
        {
            minBlocks = hardPlatformBlocks.x;
            maxBlocks = hardPlatformBlocks.y;
        }
        int blockCount = Mathf.Max(landingBlocks + (hard ? 0 : 1), Random.Range(minBlocks, maxBlocks + 1));

        // Combo: destructible obstacle shortly before the edge, pit right after -> shoot, then jump
        bool combo = difficulty >= comboMinDifficulty
            && phaseBlocksLeft > blockCount
            && Random.value < Mathf.Lerp(comboChance.x, comboChance.y, difficulty)
            && PickObstacle(difficulty, true) != null;

        // Double step: this step platform stays as short as its landing zone, gap + next step follow
        bool startDoubleStep = hard && !combo && !secondStep
            && pattern == Pattern.StepUp && heightDelta > 0f
            && targetHeight + stepHeight <= startHeight + maxStepsUp * stepHeight + 0.01f
            && Random.value < doubleStepChance;
        if (startDoubleStep)
        {
            blockCount = Mathf.Max(2, landingBlocks);
        }

        currentHeight = targetHeight;
        nextX += gap;
        float chance = startDoubleStep ? 0f : Mathf.Lerp(wildObstacleChance.x, wildObstacleChance.y, difficulty);
        phaseBlocksLeft -= BuildPlatform(blockCount, landingBlocks, chance, combo);
        if (combo)
        {
            forceGapNext = true;
        }
        if (startDoubleStep)
        {
            forceStepGapNext = true;
            // The second step belongs to this wild section even if its block budget ran out
            phaseBlocksLeft = Mathf.Max(phaseBlocksLeft, 1);
        }
    }

    // Phase 3: arrival at the next town. Flat, no obstacles; the next theme's backdrop starts here.
    private void BuildGate()
    {
        currentTheme = (currentTheme + 1) % themes.Length;
        PlaceBackdrop(currentTheme, nextX, currentHeight);
        SpawnGate(themes[currentTheme], nextX);

        BuildPlatform(gateBlocks, gateBlocks, 0f);
        phase = Phase.Town;
    }

    private Pattern PickPattern(float difficulty)
    {
        float flat = Mathf.Lerp(wildFlatWeight.x, wildFlatWeight.y, difficulty);
        float gapW = Mathf.Lerp(wildGapWeight.x, wildGapWeight.y, difficulty);
        float up = Mathf.Lerp(wildStepUpWeight.x, wildStepUpWeight.y, difficulty);
        float down = Mathf.Lerp(wildStepDownWeight.x, wildStepDownWeight.y, difficulty);

        float roll = Random.value * (flat + gapW + up + down);
        if ((roll -= flat) < 0f) return Pattern.Flat;
        if ((roll -= gapW) < 0f) return Pattern.Gap;
        if ((roll -= up) < 0f) return Pattern.StepUp;
        return Pattern.StepDown;
    }

    // ---------------------------------------------------------------- jump physics

    // Blocks needed after a jump (taken at the latest possible point) before the next hazard
    private int GetClearBlocks(float heightDelta, float gap, float reactionScale = 1f)
    {
        // The hero is faster when it gets there: size landing / run-up zones for that speed
        float speed = GetArrivalSpeed(nextX) * landingSpeedMargin;
        float landing = GetJumpDistance(heightDelta, speed) - gap + speed * reactionTime * reactionScale;
        return Mathf.Max(2, Mathf.CeilToInt(landing / blockWidth));
    }

    // Horizontal distance covered in the air until the hero is back at takeoff height + heightDelta
    private float GetJumpDistance(float heightDelta, float speed)
    {
        float g = Mathf.Abs(hero.Gravity);
        float v0 = Mathf.Sqrt(2f * g * hero.JumpHeight);
        float discriminant = v0 * v0 - 2f * g * heightDelta;
        if (discriminant <= 0f)
        {
            return 0f;
        }
        float airTime = (v0 + Mathf.Sqrt(discriminant)) / g;
        return speed * airTime;
    }

    // Speed the hero will have at x (never lower than now, the speed only rises)
    private float GetArrivalSpeed(float x)
    {
        if (difficultyProgression != null)
        {
            return Mathf.Max(hero.RunSpeed, difficultyProgression.SpeedAt(x));
        }
        return hero.RunSpeed;
    }

    private float GetGap(float heightDelta, float difficulty)
    {
        // Speed only rises over time, so the speed at generation time is the safe lower bound
        float reachable = GetJumpDistance(heightDelta, hero.RunSpeed) * jumpSafetyFactor;

        // Aggressive phase: gaps close to the jump limit, no fixed meter cap
        if (DistanceAt(nextX) >= hardGapDistance)
        {
            float hardUpper = Mathf.Min(MaxGapDistance * hardGapFactor, reachable);
            float hardLower = Mathf.Min(MaxGapDistance * Mathf.Min(hardGapMinFactor, hardGapFactor), hardUpper);
            return Random.Range(Mathf.Lerp(hardLower, hardUpper, difficulty * 0.5f), hardUpper);
        }

        float gapCap = maxGap;
        if (scaleGapsWithSpeed && baseRunSpeed > 0f)
        {
            gapCap *= Mathf.Max(1f, hero.RunSpeed / baseRunSpeed);
        }
        float upper = Mathf.Min(Mathf.Min(gapCap, reachable), MaxGapDistance);
        float lower = Mathf.Min(minGap, upper);
        float hardest = Mathf.Lerp(lower + (upper - lower) * 0.4f, upper, difficulty);
        return Random.Range(lower, hardest);
    }

    // ---------------------------------------------------------------- building

    // Returns the number of blocks actually built (an obstacle near the end extends the platform).
    // comboAtEnd: a destructible obstacle is placed a short run-up before the platform's edge.
    private int BuildPlatform(int blockCount, int firstObstacleIndex, float obstacleChance, bool comboAtEnd = false)
    {
        ThemeSettings theme = themes[currentTheme];
        float platformStartX = nextX;
        float difficulty = GetDifficulty(nextX);
        float distance = DistanceAt(nextX);

        // Dense phase: more obstacles, shorter free runs between them (still landable)
        bool dense = distance >= denseDistance;
        if (dense && obstacleChance > 0f)
        {
            obstacleChance = Mathf.Min(Mathf.Max(obstacleChance, denseMaxChance * 0.5f) + denseChanceBonus, denseMaxChance);
        }
        int obstacleClearBlocks = GetClearBlocks(0f, 0f, dense ? denseReactionScale : 1f);
        int nextObstacleIndex = firstObstacleIndex;
        bool reflexPairs = distance >= reflexPairDistance;
        float pairChance = Mathf.Lerp(reflexPairChance.x, reflexPairChance.y, difficulty);

        // After the target is shot the hero needs a moment to jump: run-up between target and edge
        int comboIndex = -1;
        if (comboAtEnd)
        {
            int runUp = Mathf.Max(1, Mathf.CeilToInt(GetArrivalSpeed(nextX) * reactionTime * 2f / blockWidth));
            blockCount = Mathf.Max(blockCount, firstObstacleIndex + runUp + 1);
            comboIndex = blockCount - 1 - runUp;
        }

        for (int i = 0; i < blockCount; i++)
        {
            float slotStart = nextX;
            float slotEnd = PlaceBlock(theme);
            float slotCenter = (slotStart + slotEnd) * 0.5f;

            if (i == comboIndex)
            {
                SpawnObstacle(PickObstacle(difficulty, true), slotCenter, slotEnd);
                continue;
            }

            // Landing zone and the last (take-off) block stay free; nothing may crowd the combo target
            bool canHaveObstacle = obstacleChance > 0f && HasObstacles
                && i >= nextObstacleIndex && i < blockCount - 1
                && (comboIndex < 0 || i + obstacleClearBlocks <= comboIndex);
            if (canHaveObstacle && Random.value < obstacleChance)
            {
                // Shoot + jump pair: may reach into the following block(s), never into the take-off block
                int extraBlocks = -1;
                if (reflexPairs && Random.value < pairChance)
                {
                    int maxExtra = blockCount - 2 - i;
                    if (comboIndex >= 0) maxExtra = Mathf.Min(maxExtra, comboIndex - obstacleClearBlocks - i);
                    extraBlocks = TrySpawnReflexPair(difficulty, slotCenter, slotEnd, maxExtra);
                }
                if (extraBlocks < 0)
                {
                    extraBlocks = 0;
                    SpawnObstacle(PickObstacle(difficulty, false), slotCenter, slotEnd);
                }

                int lastHazard = i + extraBlocks;
                nextObstacleIndex = lastHazard + obstacleClearBlocks;
                if (comboIndex < 0)
                {
                    blockCount = Mathf.Max(blockCount, lastHazard + obstacleClearBlocks + 1);
                }
            }
            else
            {
                TrySpawnProp(theme, slotStart, slotEnd);
            }
        }

        SpawnCliff(platformStartX, nextX, currentHeight);
        return blockCount;
    }

    // Snaps one road block to nextX and returns its end
    private float PlaceBlock(ThemeSettings theme)
    {
        GameObject prefab = rockBlockPrefab != null && Random.value < theme.rockBlockChance ? rockBlockPrefab : dirtBlockPrefab;
        PieceInfo info = GetInfo(prefab, PieceKind.Block, prefab.transform.rotation);

        float endX = nextX + info.width;
        Vector3 pivot = new Vector3(nextX + info.width * 0.5f - info.boundsCenter.x, currentHeight, laneZ);
        Spawn(prefab, PieceKind.Block, pivot, prefab.transform.rotation, endX);
        nextX = endX;
        return endX;
    }

    private bool HasObstacles
    {
        get { return obstacleTypes != null && obstacleTypes.Length > 0; }
    }

    private PooledObject SpawnObstacle(ObstacleType type, float centerX, float endX)
    {
        if (type == null || type.prefab == null) return null;
        GameObject prefab = type.prefab;
        Quaternion rotation = prefab.transform.rotation;
        PieceInfo info = GetInfo(prefab, PieceKind.Obstacle, rotation);

        // Bottom of the obstacle rests exactly on the road surface, centered on the lane
        Vector3 pivot = new Vector3(centerX - info.boundsCenter.x, currentHeight - info.bottom, laneZ - info.boundsCenter.z);
        return Spawn(prefab, PieceKind.Obstacle, pivot, rotation, endX);
    }

    // Destructible target, then a solid barricade reflexPairSpacing behind it: shoot, then jump.
    // Returns the number of blocks the pair reaches past slotEnd, or -1 if it was not placed.
    private int TrySpawnReflexPair(float difficulty, float targetCenter, float slotEnd, int maxExtraBlocks)
    {
        if (maxExtraBlocks < 0) return -1;

        ObstacleType target = null;
        for (int i = 0; i < obstacleTypes.Length; i++)
        {
            ObstacleType type = obstacleTypes[i];
            if (type.destructible && type.prefab != null && type.name == reflexPairTargetName) target = type;
        }
        if (target == null) target = PickObstacle(difficulty, true);
        ObstacleType barricade = PickObstacle(difficulty, false, true);
        if (target == null || barricade == null) return -1;

        PieceInfo targetInfo = GetInfo(target.prefab, PieceKind.Obstacle, target.prefab.transform.rotation);
        PieceInfo barricadeInfo = GetInfo(barricade.prefab, PieceKind.Obstacle, barricade.prefab.transform.rotation);
        float barricadeCenter = targetCenter + targetInfo.width * 0.5f + reflexPairSpacing + barricadeInfo.width * 0.5f;
        float barricadeEnd = barricadeCenter + barricadeInfo.width * 0.5f;

        int extraBlocks = Mathf.Max(0, Mathf.CeilToInt((barricadeEnd - slotEnd) / blockWidth - 0.001f));
        if (extraBlocks > maxExtraBlocks) return -1;

        SpawnObstacle(target, targetCenter, slotEnd);
        SpawnObstacle(barricade, barricadeCenter, barricadeEnd);
        return extraBlocks;
    }

    // Weighted pick; easy / hard weights follow the difficulty
    private ObstacleType PickObstacle(float difficulty, bool destructibleOnly, bool solidOnly = false)
    {
        if (!HasObstacles) return null;

        float total = 0f;
        for (int i = 0; i < obstacleTypes.Length; i++)
        {
            ObstacleType type = obstacleTypes[i];
            if (type.prefab == null || (destructibleOnly && !type.destructible) || (solidOnly && type.destructible)) continue;
            total += Mathf.Max(0f, Mathf.Lerp(type.weight.x, type.weight.y, difficulty));
        }
        if (total <= 0f) return null;

        float roll = Random.value * total;
        ObstacleType last = null;
        for (int i = 0; i < obstacleTypes.Length; i++)
        {
            ObstacleType type = obstacleTypes[i];
            if (type.prefab == null || (destructibleOnly && !type.destructible) || (solidOnly && type.destructible)) continue;
            last = type;
            roll -= Mathf.Max(0f, Mathf.Lerp(type.weight.x, type.weight.y, difficulty));
            if (roll < 0f) return type;
        }
        return last;
    }

    // Empty list = the old single crate; heights stay below the jump height
    private void SetupObstacleTypes()
    {
        if (!HasObstacles && crateBlockPrefab != null)
        {
            obstacleTypes = new[]
            {
                new ObstacleType
                {
                    name = "Crate",
                    prefab = crateBlockPrefab,
                    height = obstacleHeight,
                    destructible = crateBlockPrefab.GetComponent<DestructibleObstacle>() != null
                }
            };
        }
        if (obstacleTypes == null) return;

        float maxHeight = hero.JumpHeight * 0.75f;
        for (int i = 0; i < obstacleTypes.Length; i++)
        {
            obstacleTypes[i].height = Mathf.Clamp(obstacleTypes[i].height, 0.2f, maxHeight);
        }
    }

    // One column under the whole platform, from just below the surface down to cliffBottomY
    private void SpawnCliff(float fromX, float toX, float surfaceY)
    {
        if (!buildCliffs || cliffTemplate == null || toX <= fromX)
        {
            return;
        }

        float top = surfaceY - cliffTopInset;
        float height = top - cliffBottomY;
        if (height <= 0f)
        {
            return;
        }

        Vector3 center = new Vector3((fromX + toX) * 0.5f, (top + cliffBottomY) * 0.5f, laneZ);
        PooledObject item = Spawn(cliffTemplate, PieceKind.Cliff, center, Quaternion.identity, toX);
        // Slightly thinner than the road so the block sides stay visible (no z-fighting)
        item.transform.localScale = new Vector3(toX - fromX, height, Mathf.Max(0.1f, roadWidth - 0.06f));
    }

    private void SpawnGate(ThemeSettings theme, float x)
    {
        if (theme.gatePrefab == null)
        {
            return;
        }

        Quaternion rotation = Quaternion.Euler(theme.gateRotation);
        PieceInfo info = GetInfo(theme.gatePrefab, PieceKind.Gate, rotation);
        Vector3 pivot = new Vector3(
            x + info.width * 0.5f - info.boundsCenter.x,
            currentHeight - info.bottom,
            laneZ - info.boundsCenter.z) + theme.gateOffset;
        Spawn(theme.gatePrefab, PieceKind.Gate, pivot, rotation, x + info.width);
    }

    private void TrySpawnProp(ThemeSettings theme, float slotStart, float slotEnd)
    {
        if (theme.propPrefabs == null || theme.propPrefabs.Length == 0 || Random.value >= theme.propChancePerBlock)
        {
            return;
        }

        GameObject prefab = theme.propPrefabs[Random.Range(0, theme.propPrefabs.Length)];
        if (prefab == null)
        {
            return;
        }

        Quaternion rotation = prefab.transform.rotation;
        PieceInfo info = GetInfo(prefab, PieceKind.Prop, rotation);
        float halfWidth = info.width * 0.5f;
        float centerX = (slotStart + slotEnd) * 0.5f;

        // Stay on this block and keep distance to the previous prop
        if (centerX - halfWidth < slotStart - propSpacing || centerX + halfWidth > slotEnd + propSpacing) return;
        if (centerX - halfWidth < lastPropEndX + propSpacing) return;

        float centerZ = Mathf.Max(laneZ + propDepthOffset, laneZ + propLaneClearance + info.halfDepth);

        Vector3 pivot = new Vector3(centerX - info.boundsCenter.x, currentHeight - info.bottom, centerZ - info.boundsCenter.z);
        Spawn(prefab, PieceKind.Prop, pivot, rotation, slotEnd);
        lastPropEndX = centerX + halfWidth;
    }

    // ---------------------------------------------------------------- backdrops

    // Pushes every backdrop behind the road (once) and measures its length
    private void PrepareBackdrops()
    {
        float roadBackZ = laneZ + roadWidth * 0.5f;
        var minZs = new List<float>();
        var maxXs = new List<float>();
        var renderers = new List<Renderer>();

        foreach (ThemeSettings theme in themes)
        {
            if (theme.backdropParts == null) continue;

            minZs.Clear();
            maxXs.Clear();
            renderers.Clear();
            float floorY = theme.backdropAnchor.y - 30f;

            foreach (Transform part in theme.backdropParts)
            {
                if (part == null) continue;

                // Backdrops are pure scenery; their old colliders would create invisible floors on the road
                if (disableBackdropColliders)
                {
                    foreach (Collider col in part.GetComponentsInChildren<Collider>(true))
                    {
                        col.enabled = false;
                    }
                }

                foreach (Renderer r in part.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled) continue;
                    renderers.Add(r);
                    if (r is ParticleSystemRenderer) continue; // bounds of idle particles are meaningless
                    Bounds b = r.bounds;
                    if (b.max.y < floorY) continue;
                    minZs.Add(b.min.z);
                    maxXs.Add(b.max.x);
                }
            }
            if (minZs.Count == 0) continue;

            // Front edge = 5th percentile (single stray meshes do not count), length = 98th percentile
            float frontZ = Percentile(minZs, 0.05f);
            float shift = roadBackZ + backgroundGap - frontZ;
            foreach (Transform part in theme.backdropParts)
            {
                if (part != null) part.position += new Vector3(0f, 0f, shift);
            }
            theme.backdropLength = Mathf.Max(0f, Percentile(maxXs, 0.98f) - theme.backdropAnchor.x);

            // Whatever still hangs into / in front of the road (waterfalls, overhangs) is hidden
            if (hideBackdropInFrontOfRoad)
            {
                for (int i = 0; i < renderers.Count; i++)
                {
                    Bounds b = renderers[i].bounds;
                    if (b.min.z < roadBackZ + 0.05f && b.max.y > floorY)
                    {
                        renderers[i].enabled = false;
                    }
                }
            }
        }
    }

    private static float Percentile(List<float> values, float p)
    {
        values.Sort();
        int index = Mathf.Clamp(Mathf.RoundToInt((values.Count - 1) * p), 0, values.Count - 1);
        return values[index];
    }

    private void PlaceBackdrop(int themeIndex, float roadX, float roadHeight)
    {
        ThemeSettings theme = themes[themeIndex];
        backdropStartX = roadX;

        if (theme.backdropParts == null || theme.backdropParts.Length == 0)
        {
            return;
        }

        // Only X / Y move here, the Z offset was applied once in PrepareBackdrops
        Vector3 delta = new Vector3(roadX - theme.backdropAnchor.x, roadHeight - theme.backdropAnchor.y, 0f);
        foreach (Transform part in theme.backdropParts)
        {
            if (part != null)
            {
                part.position += delta;
            }
        }
        theme.backdropAnchor = new Vector2(roadX, roadHeight);
    }

    // ---------------------------------------------------------------- pooling

    private void CreateCliffTemplate()
    {
        if (!buildCliffs) return;

        cliffTemplate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cliffTemplate.name = "CliffBase";
        // Immediate: the template is instantiated in this frame, a deferred Destroy would copy the collider
        DestroyImmediate(cliffTemplate.GetComponent<Collider>());
        cliffTemplate.transform.SetParent(transform, false);
        cliffTemplate.SetActive(false);

        Material material = cliffMaterial;
        if (material == null)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null)
            {
                material = new Material(lit) { name = "CliffBase_Runtime" };
                material.SetColor("_BaseColor", cliffColor);
                material.SetFloat("_Smoothness", 0.1f);
                createdMaterials.Add(material);
            }
        }

        MeshRenderer meshRenderer = cliffTemplate.GetComponent<MeshRenderer>();
        if (material != null) meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void PrewarmPools()
    {
        if (HasObstacles)
        {
            foreach (ObstacleType type in obstacleTypes)
            {
                if (type.prefab == null || obstacleScales.ContainsKey(type.prefab)) continue;
                // Scale every obstacle to its height (measured unscaled: no scale is registered yet)
                PooledObject probe = CreateItem(type.prefab, PieceKind.Obstacle, type.prefab.transform.rotation);
                float nativeHeight = GetBounds(probe).size.y;
                probe.gameObject.SetActive(false);
                Destroy(probe.gameObject);
                obstacleScales[type.prefab] = type.height / Mathf.Max(0.01f, nativeHeight);
                Prewarm(type.prefab, PieceKind.Obstacle, prewarmBlocksPerPrefab / 3);
            }
        }

        Prewarm(dirtBlockPrefab, PieceKind.Block, prewarmBlocksPerPrefab);
        Prewarm(rockBlockPrefab, PieceKind.Block, prewarmBlocksPerPrefab / 2);
        Prewarm(cliffTemplate, PieceKind.Cliff, 12, Quaternion.identity);

        foreach (ThemeSettings theme in themes)
        {
            Prewarm(theme.gatePrefab, PieceKind.Gate, 1, Quaternion.Euler(theme.gateRotation));
            if (theme.propPrefabs == null) continue;
            foreach (GameObject prop in theme.propPrefabs)
            {
                Prewarm(prop, PieceKind.Prop, prewarmPerProp);
            }
        }
    }

    private void Prewarm(GameObject prefab, PieceKind kind, int count)
    {
        if (prefab == null) return;
        Prewarm(prefab, kind, count, prefab.transform.rotation);
    }

    private void Prewarm(GameObject prefab, PieceKind kind, int count, Quaternion rotation)
    {
        if (prefab == null) return;
        Stack<PooledObject> pool = GetPool(prefab);
        for (int i = pool.Count; i < count; i++)
        {
            PooledObject item = CreateItem(prefab, kind, rotation);
            item.gameObject.SetActive(false);
            pool.Push(item);
        }
    }

    private Stack<PooledObject> GetPool(GameObject prefab)
    {
        Stack<PooledObject> pool;
        if (!pools.TryGetValue(prefab, out pool))
        {
            pool = new Stack<PooledObject>();
            pools.Add(prefab, pool);
        }
        return pool;
    }

    private PieceInfo GetInfo(GameObject prefab, PieceKind kind, Quaternion rotation)
    {
        PieceInfo info;
        if (pieceInfos.TryGetValue(prefab, out info))
        {
            return info;
        }

        // Measure a pooled instance at the origin with its spawn rotation / scale
        PooledObject item = CreateItem(prefab, kind, rotation);
        item.gameObject.SetActive(true);
        info = Measure(item);
        item.gameObject.SetActive(false);
        GetPool(prefab).Push(item);

        pieceInfos[prefab] = info;
        return info;
    }

    private static Bounds GetBounds(PooledObject item)
    {
        if (item.renderers.Length == 0)
        {
            return new Bounds(item.transform.position, Vector3.zero);
        }

        Bounds bounds = item.renderers[0].bounds;
        for (int i = 1; i < item.renderers.Length; i++)
        {
            bounds.Encapsulate(item.renderers[i].bounds);
        }
        return bounds;
    }

    private static PieceInfo Measure(PooledObject item)
    {
        Bounds bounds = GetBounds(item);
        Vector3 pivot = item.transform.position;
        return new PieceInfo
        {
            width = bounds.size.x,
            halfDepth = bounds.extents.z,
            boundsCenter = bounds.center - pivot,
            bottom = bounds.min.y - pivot.y
        };
    }

    private PooledObject CreateItem(GameObject prefab, PieceKind kind, Quaternion rotation)
    {
        GameObject go = Instantiate(prefab, Vector3.zero, rotation, transform);
        PooledObject item = new PooledObject
        {
            prefab = prefab,
            gameObject = go,
            transform = go.transform,
            renderers = go.GetComponentsInChildren<Renderer>(true)
        };

        switch (kind)
        {
            case PieceKind.Block:
                SetLayerRecursively(go.transform, groundLayer);
                FitBlockCollider(item);
                item.themedMaterials = BuildThemedMaterials(item.renderers);
                break;

            case PieceKind.Obstacle:
                float scale;
                if (obstacleScales.TryGetValue(prefab, out scale))
                {
                    go.transform.localScale = prefab.transform.localScale * scale;
                }
                SetLayerRecursively(go.transform, groundLayer);
                item.themedMaterials = BuildThemedMaterials(item.renderers);
                break;

            case PieceKind.Cliff:
                item.themedMaterials = BuildThemedMaterials(item.renderers);
                break;

            case PieceKind.Prop:
            case PieceKind.Gate:
                foreach (Collider col in go.GetComponentsInChildren<Collider>(true))
                {
                    col.enabled = false;
                }
                break;
        }

        return item;
    }

    // The block mesh is a bit wider than its 2m collider; match them so snapped blocks touch without overlapping
    private static void FitBlockCollider(PooledObject item)
    {
        BoxCollider box = item.gameObject.GetComponent<BoxCollider>();
        if (box == null || item.renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = GetBounds(item);
        Vector3 localCenter = item.transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = item.transform.InverseTransformVector(bounds.size);
        box.center = new Vector3(localCenter.x, box.center.y, box.center.z);
        box.size = new Vector3(Mathf.Abs(localSize.x), box.size.y, box.size.z);
    }

    private Material[][][] BuildThemedMaterials(Renderer[] renderers)
    {
        Material[][][] result = new Material[themes.Length][][];
        for (int t = 0; t < themes.Length; t++)
        {
            result[t] = new Material[renderers.Length][];
            for (int r = 0; r < renderers.Length; r++)
            {
                Material[] source = renderers[r].sharedMaterials;
                Material[] tinted = new Material[source.Length];
                for (int m = 0; m < source.Length; m++)
                {
                    tinted[m] = GetTintedMaterial(source[m], t);
                }
                result[t][r] = tinted;
            }
        }
        return result;
    }

    // One tinted copy per (source material, theme), shared by every piece so batching still works
    private Material GetTintedMaterial(Material source, int themeIndex)
    {
        if (source == null || themes[themeIndex].blockTint == Color.white)
        {
            return source;
        }

        Material[] perTheme;
        if (!tintedMaterialCache.TryGetValue(source, out perTheme))
        {
            perTheme = new Material[themes.Length];
            tintedMaterialCache.Add(source, perTheme);
        }

        if (perTheme[themeIndex] == null)
        {
            Material tinted = new Material(source);
            tinted.name = source.name + "_" + themes[themeIndex].themeName;
            tinted.color = source.color * themes[themeIndex].blockTint;
            perTheme[themeIndex] = tinted;
            createdMaterials.Add(tinted);
        }
        return perTheme[themeIndex];
    }

    private PooledObject Spawn(GameObject prefab, PieceKind kind, Vector3 pivot, Quaternion rotation, float endX)
    {
        Stack<PooledObject> pool = GetPool(prefab);
        PooledObject item = pool.Count > 0 ? pool.Pop() : CreateItem(prefab, kind, rotation);

        item.transform.SetPositionAndRotation(pivot, rotation);
        ApplyTheme(item);
        item.gameObject.SetActive(true);

        activePieces.Enqueue(new ActivePiece { item = item, endX = endX });
        return item;
    }

    private void ApplyTheme(PooledObject item)
    {
        if (item.themedMaterials == null || item.appliedTheme == currentTheme)
        {
            return;
        }

        Material[][] materials = item.themedMaterials[currentTheme];
        for (int r = 0; r < item.renderers.Length; r++)
        {
            item.renderers[r].sharedMaterials = materials[r];
        }
        item.appliedTheme = currentTheme;
    }

    private void Release(PooledObject item)
    {
        item.gameObject.SetActive(false);
        GetPool(item.prefab).Push(item);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }
}
