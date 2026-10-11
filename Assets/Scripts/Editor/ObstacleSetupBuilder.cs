using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Journey > Setup Obstacle Types
// Creates the obstacle prefabs (if missing) and fills BlockManager's weighted obstacle list in the open game scene:
//   Crate            (BlockCrate, destructible)     easy 1.0 -> hard 0.6
//   ExplosiveBarrel  (red barrel, destructible)     easy 0.4 -> hard 0.8   tutorial target
//   StoneBarricade   (stone block, jump only)       easy 0.3 -> hard 0.7   bullets only spark on it
public static class ObstacleSetupBuilder
{
    private const string Folder = "Assets/Prefabs/Obstacles";
    private const string BarrelPath = Folder + "/ExplosiveBarrel.prefab";
    private const string StonePath = Folder + "/StoneBarricade.prefab";
    private const string BarrelMaterialPath = "Assets/Materials/Obstacle_ExplosiveBarrel.mat";
    private const string BarrelModel = "Assets/AurynSky/Forest Pack/Prefabs/ForestBarrel.prefab";
    private const string StoneModel = "Assets/AurynSky/WinterArena/Prefabs/Ground/WinterGroundRock.prefab";
    private const string CratePath = "Assets/Prefabs/BlockCrate.prefab";
    private const string ExplosionPath = "Assets/Prefabs/Weapons/CrateExplosion.prefab";

    [MenuItem("Tools/Journey/Setup Obstacle Types")]
    public static void Setup()
    {
        Directory.CreateDirectory(Folder);
        GameObject barrel = AssetDatabase.LoadAssetAtPath<GameObject>(BarrelPath);
        if (barrel == null) barrel = CreateBarrel();
        GameObject stone = AssetDatabase.LoadAssetAtPath<GameObject>(StonePath);
        if (stone == null) stone = CreateStone();
        GameObject crate = AssetDatabase.LoadAssetAtPath<GameObject>(CratePath);

        BlockManager blockManager = Object.FindFirstObjectByType<BlockManager>(FindObjectsInactive.Include);
        if (blockManager == null)
        {
            Debug.LogWarning("ObstacleSetupBuilder: prefabs created; open the game scene to fill BlockManager's list.");
            return;
        }

        SerializedObject so = new SerializedObject(blockManager);
        SerializedProperty list = so.FindProperty("obstacleTypes");
        list.arraySize = 3;
        Fill(list.GetArrayElementAtIndex(0), "Crate", crate, 1f, new Vector2(1f, 0.6f), true);
        Fill(list.GetArrayElementAtIndex(1), "ExplosiveBarrel", barrel, 1f, new Vector2(0.4f, 0.8f), true);
        Fill(list.GetArrayElementAtIndex(2), "StoneBarricade", stone, 0.9f, new Vector2(0.3f, 0.7f), false);
        so.FindProperty("safeStartBlocks").intValue = 10;
        // Three obstacle types share the road now: a bit denser, rising with the difficulty
        so.FindProperty("townObstacleChance").vector2Value = new Vector2(0.12f, 0.35f);
        so.FindProperty("wildObstacleChance").vector2Value = new Vector2(0.05f, 0.2f);
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(blockManager.gameObject.scene);
        Debug.Log("ObstacleSetupBuilder: 3 obstacle types assigned, Safe Start Blocks = 10. Save the scene to keep it.", blockManager);
    }

    private static void Fill(SerializedProperty element, string name, GameObject prefab, float height, Vector2 weight, bool destructible)
    {
        element.FindPropertyRelative("name").stringValue = name;
        element.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        element.FindPropertyRelative("height").floatValue = height;
        element.FindPropertyRelative("weight").vector2Value = weight;
        element.FindPropertyRelative("destructible").boolValue = destructible;
    }

    // Red barrel: shoot it (bigger bonus) or die on contact
    private static GameObject CreateBarrel()
    {
        GameObject root = CreateRoot("ExplosiveBarrel", BarrelModel);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(BarrelMaterialPath);
        if (material == null)
        {
            Renderer source = root.GetComponentInChildren<Renderer>();
            material = new Material(source.sharedMaterial) { name = "Obstacle_ExplosiveBarrel" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(1f, 0.32f, 0.22f));
            else material.color = new Color(1f, 0.32f, 0.22f);
            AssetDatabase.CreateAsset(material, BarrelMaterialPath);
        }
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = material;
            r.sharedMaterials = mats;
        }

        DestructibleObstacle destructible = root.AddComponent<DestructibleObstacle>();
        SerializedObject so = new SerializedObject(destructible);
        so.FindProperty("health").intValue = 1;
        so.FindProperty("bonusPoints").intValue = 75;
        so.FindProperty("explosionEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionPath);
        so.ApplyModifiedPropertiesWithoutUndo();

        return Save(root, BarrelPath);
    }

    // Stone block: no DestructibleObstacle -> bullets hit it, spark and vanish; must be jumped
    private static GameObject CreateStone()
    {
        return Save(CreateRoot("StoneBarricade", StoneModel), StonePath);
    }

    private static GameObject CreateRoot(string name, string modelPath)
    {
        GameObject root = new GameObject(name);
        root.tag = "Obstacle";
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
        model.transform.SetParent(root.transform, false);
        foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

        // Model centered on the root, standing on y = 0
        Bounds bounds = GetBounds(model);
        model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        bounds = GetBounds(model);

        BoxCollider box = root.AddComponent<BoxCollider>();
        box.center = bounds.center;
        box.size = bounds.size;
        return root;
    }

    private static Bounds GetBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        Bounds b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(go.transform.position, Vector3.one);
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private static GameObject Save(GameObject root, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }
}
