using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Tools > Journey > Apply Obstacle Styles
// Quiet material language instead of labels or glow:
//   breakable   (BlockCrate, ExplosiveBarrel): warm amber wood tone, faint cracks on the crate's camera-facing side
//   unbreakable (StoneBarricade):              dark, hard grey stone
//   bullets on unbreakable surfaces: small white/yellow sparks that glance off (RicochetSpark), no explosion
// Safe to run again: materials / prefabs are updated in place.
public static class ObstacleStyleBuilder
{
    private const string CratePath = "Assets/Prefabs/BlockCrate.prefab";
    private const string BarrelPath = "Assets/Prefabs/Obstacles/ExplosiveBarrel.prefab";
    private const string StonePath = "Assets/Prefabs/Obstacles/StoneBarricade.prefab";
    private const string BulletPath = "Assets/Prefabs/Weapons/Bullet.prefab";
    private const string RicochetPath = "Assets/Prefabs/Weapons/RicochetSpark.prefab";
    private const string AdditivePath = "Assets/Materials/VFX/VFX_Additive.mat";

    private const string WarmWoodPath = "Assets/Materials/Obstacle_WarmWood.mat";
    private const string BarrelMaterialPath = "Assets/Materials/Obstacle_ExplosiveBarrel.mat";
    private const string DarkStonePath = "Assets/Materials/Obstacle_DarkStone.mat";
    private const string CrackMaterialPath = "Assets/Materials/Obstacle_WoodCracks.mat";
    private const string CrackTexturePath = "Assets/Textures/FX_WoodCracks.png";

    private static readonly Color WarmWood = new Color(1f, 0.88f, 0.68f);
    private static readonly Color BarrelWood = new Color(1f, 0.70f, 0.40f);
    private static readonly Color DarkStone = new Color(0.36f, 0.38f, 0.42f);

    [MenuItem("Tools/Journey/Apply Obstacle Styles")]
    public static void Apply()
    {
        Material crackMaterial = GetCrackMaterial();

        EditPrefab(CratePath, root =>
        {
            Material wood = TintedCopy(FirstMaterial(root), WarmWoodPath, WarmWood, 0.15f);
            SetMaterial(root, wood);
            AddCracks(root, crackMaterial);
        });

        EditPrefab(BarrelPath, root =>
        {
            Material barrel = TintedCopy(FirstMaterial(root), BarrelMaterialPath, BarrelWood, 0.2f);
            SetMaterial(root, barrel);
        });

        EditPrefab(StonePath, root =>
        {
            Material stone = TintedCopy(FirstMaterial(root), DarkStonePath, DarkStone, 0.3f);
            SetMaterial(root, stone);
        });

        GameObject ricochet = CreateRicochetSpark();
        EditPrefab(BulletPath, root =>
        {
            SerializedObject so = new SerializedObject(root.GetComponent<Projectile>());
            so.FindProperty("ricochetEffect").objectReferenceValue = ricochet;
            so.ApplyModifiedPropertiesWithoutUndo();
        });

        AssetDatabase.SaveAssets();
        Debug.Log("ObstacleStyleBuilder: warm wood (crate, barrel), dark stone (barricade) and ricochet sparks applied.");
    }

    // ---------------------------------------------------------------- prefabs

    private static void EditPrefab(string path, System.Action<GameObject> edit)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            Debug.LogWarning("ObstacleStyleBuilder: missing " + path + " (run Setup Obstacle Types first).");
            return;
        }
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        edit(root);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static Material FirstMaterial(GameObject root)
    {
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r.gameObject.name == "Cracks") continue;
            if (r.sharedMaterial != null) return r.sharedMaterial;
        }
        return null;
    }

    private static void SetMaterial(GameObject root, Material material)
    {
        if (material == null) return;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r.gameObject.name == "Cracks") continue;
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = material;
            r.sharedMaterials = mats;
        }
    }

    // Copy of the model's own material (keeps its palette texture), only the tone changes
    private static Material TintedCopy(Material source, string path, Color tint, float smoothness)
    {
        if (source == null) return null;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(source);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material != source)
        {
            Texture baseMap = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : null;
            if (baseMap != null && material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", baseMap);
        }
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
        else material.color = tint;
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    // Thin quad with a crack decal on the side the camera sees (-Z), slightly in front of the face
    private static void AddCracks(GameObject root, Material crackMaterial)
    {
        Transform old = root.transform.Find("Cracks");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        Bounds bounds = new Bounds();
        bool found = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        if (!found) return;

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Cracks";
        Object.DestroyImmediate(quad.GetComponent<Collider>()); // must never become part of the obstacle collider
        quad.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - 0.004f * bounds.size.z);
        quad.transform.rotation = Quaternion.identity; // a Unity quad faces -Z
        quad.transform.localScale = new Vector3(bounds.size.x * 0.9f, bounds.size.y * 0.9f, 1f);
        quad.transform.SetParent(root.transform, true);

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = crackMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    // ---------------------------------------------------------------- crack decal

    private static Material GetCrackMaterial()
    {
        Texture2D texture = GetCrackTexture();
        Material material = AssetDatabase.LoadAssetAtPath<Material>(CrackMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, CrackMaterialPath);
        }
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", new Color(0.24f, 0.07f, 0.02f, 0.9f));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    // A few thin branching cracks from the edges (deterministic, generated once)
    private static Texture2D GetCrackTexture()
    {
        if (!File.Exists(CrackTexturePath))
        {
            const int size = 256;
            float[] alpha = new float[size * size];
            System.Random random = new System.Random(7);
            Vector2[] starts =
            {
                new Vector2(0.02f, 0.78f), new Vector2(0.98f, 0.30f), new Vector2(0.55f, 0.02f), new Vector2(0.30f, 0.98f)
            };
            float[] angles = { -0.35f, 2.9f, 1.75f, -1.35f };
            for (int i = 0; i < starts.Length; i++)
            {
                Crack(alpha, size, starts[i] * size, angles[i], 40 + random.Next(25), 2.4f, random, 2);
            }

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int i = 0; i < alpha.Length; i++)
            {
                tex.SetPixel(i % size, i / size, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha[i])));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(CrackTexturePath));
            File.WriteAllBytes(CrackTexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(CrackTexturePath);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(CrackTexturePath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(CrackTexturePath);
    }

    // Random walk with a slowly turning heading, thinning out, with a few side branches
    private static void Crack(float[] alpha, int size, Vector2 position, float heading, int steps, float width, System.Random random, int depth)
    {
        for (int s = 0; s < steps; s++)
        {
            // Mostly straight with occasional sharp kinks: splintered wood, not a curvy line
            heading += (float)(random.NextDouble() - 0.5) * 0.12f;
            if (random.NextDouble() < 0.08) heading += (float)(random.NextDouble() - 0.5) * 1.1f;
            Vector2 next = position + new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * 2f;
            float w = Mathf.Lerp(width, 0.6f, s / (float)steps);
            Stamp(alpha, size, next, w);
            position = next;

            if (depth > 0 && random.NextDouble() < 0.05)
            {
                float side = random.NextDouble() < 0.5 ? -1f : 1f;
                Crack(alpha, size, position, heading + side * (0.6f + (float)random.NextDouble() * 0.5f), steps / 3, w * 0.8f, random, depth - 1);
            }
            if (position.x < 0 || position.y < 0 || position.x >= size || position.y >= size) return;
        }
    }

    private static void Stamp(float[] alpha, int size, Vector2 center, float radius)
    {
        int r = Mathf.CeilToInt(radius + 1f);
        for (int y = (int)center.y - r; y <= (int)center.y + r; y++)
        {
            for (int x = (int)center.x - r; x <= (int)center.x + r; x++)
            {
                if (x < 0 || y < 0 || x >= size || y >= size) continue;
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float a = Mathf.Clamp01(radius - d + 0.5f);
                int i = y * size + x;
                if (a > alpha[i]) alpha[i] = a;
            }
        }
    }

    // ---------------------------------------------------------------- ricochet sparks

    // Small, fast, bright sparks along the bounce direction + a tiny flash: "hard surface", not "explosion"
    private static GameObject CreateRicochetSpark()
    {
        Material additive = AssetDatabase.LoadAssetAtPath<Material>(AdditivePath);
        GameObject go = new GameObject("RicochetSpark");
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.9f, 0.55f));
        main.gravityModifier = 2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 32;
        main.stopAction = ParticleSystemStopAction.Callback;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)14) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 28f;
        shape.radius = 0.02f;

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.6f, 0.2f), 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = fade;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.035f;
        renderer.lengthScale = 1.5f;
        renderer.sharedMaterial = additive;
        renderer.shadowCastingMode = ShadowCastingMode.Off;

        GameObject flash = new GameObject("Flash");
        flash.transform.SetParent(go.transform, false);
        Light light = flash.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.95f, 0.85f);
        light.range = 1.6f;
        light.intensity = 2.5f;
        light.shadows = LightShadows.None;
        light.enabled = false;

        go.AddComponent<PooledEffect>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, RicochetPath);
        Object.DestroyImmediate(go);
        return prefab;
    }
}
