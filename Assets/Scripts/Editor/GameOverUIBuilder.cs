using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Builds the arcade style Game Over card in the open game scene and wires it to GameUIController / HeroManager.
// Menu: Tools > Journey > Build Game Over UI   (re-running replaces the previous GameUICanvas, everything is undoable)
//
// GameUICanvas (Canvas overlay, Scale With Screen Size 1920x1080, GameUIController)
// └─ GameOverPanel (full screen #000000 75%, CanvasGroup)        -> gameOverPanel
//    └─ GameOverCard (600x750, #1A1D2E, rounded)                 -> gameOverWindow
//       ├─ Title "OYUN BİTTİ" (58, #FFA502 -> #FF4757, outline)  -> gameOverTitle
//       ├─ Divider
//       ├─ ScoreLabel "SKOR" / ScoreValue                         -> finalScoreText
//       ├─ NewRecordBadge "YENİ REKOR!"                           -> highScoreBadge / highScoreBadgeText
//       ├─ BestLabel "EN İYİ" / BestValue (#FFD32A)               -> highScoreText
//       ├─ RestartButton "TEKRAR OYNA" (#2ED573)                  -> OnRestartButton
//       └─ MenuButton "ANA MENÜ" (#1E90FF)                        -> OnMenuButton
public static class GameOverUIBuilder
{
    private const string CanvasName = "GameUICanvas";
    private const string SpritePath = "Assets/Textures/UI_RoundedRect.png";
    private const string TitleMaterialPath = "Assets/Materials/UI/GameOverTitle.mat";
    private const string BoldFontPath = "Assets/Prefabs/UI/Fonts/OpenSans-Bold SDF.asset";
    private const string SemiboldFontPath = "Assets/Prefabs/UI/Fonts/OpenSans-Semibold SDF.asset";
    private const int CornerPixels = 24;

    [MenuItem("Tools/Journey/Build Game Over UI")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("GameOverUIBuilder: exit Play Mode first.");
            return;
        }

        HeroManager heroManager = Object.FindFirstObjectByType<HeroManager>(FindObjectsInactive.Include);
        if (heroManager == null)
        {
            Debug.LogError("GameOverUIBuilder: open the game scene (no HeroManager found).");
            return;
        }

        Undo.SetCurrentGroupName("Build Game Over UI");
        int undoGroup = Undo.GetCurrentGroup();

        GameObject previous = GameObject.Find(CanvasName);
        if (previous != null)
        {
            Undo.DestroyObjectImmediate(previous);
        }

        Sprite rounded = GetRoundedSprite();
        TMP_FontAsset bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
        TMP_FontAsset semibold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SemiboldFontPath);
        if (bold == null) bold = TMP_Settings.defaultFontAsset;
        if (semibold == null) semibold = bold;
        Material titleMaterial = GetTitleMaterial(bold);

        // ---- canvas
        GameObject canvasGo = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGo, "Build Game Over UI");
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // ---- dark overlay (also swallows clicks to the game)
        RectTransform panel = CreateRect("GameOverPanel", canvasGo.transform);
        Stretch(panel);
        Image overlay = panel.gameObject.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.75f);
        CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();

        // ---- card
        RectTransform card = CreateRect("GameOverCard", panel);
        card.sizeDelta = new Vector2(600f, 750f);
        Image cardImage = AddRounded(card.gameObject, rounded, Hex("#1A1D2E"));
        AddShadow(card.gameObject, new Color(0f, 0f, 0f, 0.55f), new Vector2(0f, -10f));
        Outline border = card.gameObject.AddComponent<Outline>();
        border.effectColor = Hex("#2F3555");
        border.effectDistance = new Vector2(2f, -2f);
        cardImage.raycastTarget = true;

        // ---- title
        TextMeshProUGUI title = CreateText("Title", card, "OYUN BİTTİ", bold, 58f, Color.white, new Vector2(0f, -85f), new Vector2(560f, 100f));
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 4f;
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(Hex("#FFA502"), Hex("#FFA502"), Hex("#FF4757"), Hex("#FF4757"));
        if (titleMaterial != null) title.fontSharedMaterial = titleMaterial;

        RectTransform divider = CreateRect("Divider", card);
        TopCenter(divider, new Vector2(0f, -150f), new Vector2(480f, 4f));
        divider.gameObject.AddComponent<Image>().color = Hex("#2F3555");

        // ---- scores
        CreateText("ScoreLabel", card, "SKOR", semibold, 30f, Hex("#A4B0BE"), new Vector2(0f, -192f), new Vector2(400f, 44f)).characterSpacing = 6f;
        TextMeshProUGUI scoreValue = CreateText("ScoreValue", card, "0", bold, 88f, Color.white, new Vector2(0f, -265f), new Vector2(520f, 110f));
        CreateText("BestLabel", card, "EN İYİ", semibold, 26f, Hex("#A4B0BE"), new Vector2(0f, -352f), new Vector2(400f, 40f)).characterSpacing = 6f;
        TextMeshProUGUI bestValue = CreateText("BestValue", card, "0", bold, 50f, Hex("#FFD32A"), new Vector2(0f, -402f), new Vector2(520f, 70f));

        // ---- new record badge (tilted sticker next to the score)
        RectTransform badge = CreateRect("NewRecordBadge", card);
        TopCenter(badge, new Vector2(185f, -218f), new Vector2(230f, 56f));
        badge.localRotation = Quaternion.Euler(0f, 0f, -8f);
        AddRounded(badge.gameObject, rounded, Hex("#FF4757")).raycastTarget = false;
        AddShadow(badge.gameObject, new Color(0f, 0f, 0f, 0.5f), new Vector2(0f, -4f));
        TextMeshProUGUI badgeText = CreateText("Label", badge, "YENİ REKOR!", bold, 28f, Color.white, Vector2.zero, Vector2.zero);
        Stretch(badgeText.rectTransform);
        badgeText.fontStyle = FontStyles.Bold;
        badge.gameObject.SetActive(false);

        // ---- buttons
        Button restart = CreateButton("RestartButton", card, "TEKRAR OYNA", bold, rounded, Hex("#2ED573"), Hex("#1E9E55"), 175f);
        Button menu = CreateButton("MenuButton", card, "ANA MENÜ", bold, rounded, Hex("#1E90FF"), Hex("#1565C0"), 60f);

        // ---- controller
        GameUIController controller = canvasGo.AddComponent<GameUIController>();
        Events sceneEvents = Object.FindFirstObjectByType<Events>(FindObjectsInactive.Include);
        Score score = Object.FindFirstObjectByType<Score>(FindObjectsInactive.Include);
        Object hudText = score != null ? new SerializedObject(score).FindProperty("score").objectReferenceValue : null;

        SerializedObject ui = new SerializedObject(controller);
        ui.FindProperty("hudRoot").objectReferenceValue = hudText != null ? ((Component)hudText).gameObject : null;
        ui.FindProperty("gameOverPanel").objectReferenceValue = group;
        ui.FindProperty("gameOverWindow").objectReferenceValue = card;
        ui.FindProperty("gameOverTitle").objectReferenceValue = title.rectTransform;
        ui.FindProperty("finalScoreText").objectReferenceValue = scoreValue;
        ui.FindProperty("highScoreText").objectReferenceValue = bestValue;
        ui.FindProperty("highScoreBadge").objectReferenceValue = badge;
        ui.FindProperty("highScoreBadgeText").objectReferenceValue = badgeText;
        ui.FindProperty("sceneEvents").objectReferenceValue = sceneEvents;
        ui.FindProperty("scorePrefix").stringValue = "";
        ui.FindProperty("bestPrefix").stringValue = "";
        ui.FindProperty("titleDropHeight").floatValue = 120f;
        ui.FindProperty("titleStartScale").floatValue = 1.4f;
        ui.ApplyModifiedPropertiesWithoutUndo();

        UnityEventTools.AddPersistentListener(restart.onClick, controller.OnRestartButton);
        UnityEventTools.AddPersistentListener(menu.onClick, controller.OnMenuButton);

        // ---- HeroManager: only the new card, the old panel is no longer opened
        Undo.RecordObject(heroManager, "Build Game Over UI");
        SerializedObject hm = new SerializedObject(heroManager);
        hm.FindProperty("gameUI").objectReferenceValue = controller;
        hm.FindProperty("gameOverPanel").objectReferenceValue = null;
        hm.FindProperty("finalScoreText").objectReferenceValue = null;
        hm.FindProperty("highScoreText").objectReferenceValue = null;
        hm.ApplyModifiedProperties();

        if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Build Game Over UI");
        }

        // Hidden in the editor; GameUIController shows it on game over
        panel.gameObject.SetActive(false);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(canvasGo.scene);
        Selection.activeGameObject = canvasGo;
        Debug.Log("GameOverUIBuilder: Game Over card built and wired. Save the scene (Ctrl+S) to keep it.", canvasGo);
    }

    // ---------------------------------------------------------------- helpers

    internal static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    internal static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    internal static void TopCenter(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    internal static TextMeshProUGUI CreateText(string name, Transform parent, string text, TMP_FontAsset font, float size, Color color, Vector2 position, Vector2 rectSize)
    {
        RectTransform rect = CreateRect(name, parent);
        TopCenter(rect, position, rectSize);
        TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    internal static Image AddRounded(GameObject go, Sprite sprite, Color color)
    {
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 2f; // 24px sprite corners -> 12px on the canvas
        image.color = color;
        return image;
    }

    internal static void AddShadow(GameObject go, Color color, Vector2 distance)
    {
        Shadow shadow = go.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = distance;
    }

    internal static Button CreateButton(string name, Transform card, string label, TMP_FontAsset font, Sprite sprite, Color color, Color shadowColor, float bottomY)
    {
        RectTransform rect = CreateRect(name, card);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, bottomY);
        rect.sizeDelta = new Vector2(520f, 96f);

        Image image = AddRounded(rect.gameObject, sprite, color);
        AddShadow(rect.gameObject, shadowColor, new Vector2(0f, -7f));

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.05f;
        button.colors = colors;

        rect.gameObject.AddComponent<UIButtonPunch>();

        TextMeshProUGUI text = CreateText("Label", rect, label, font, 40f, Color.white, Vector2.zero, Vector2.zero);
        Stretch(text.rectTransform);
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = 3f;
        return button;
    }

    internal static Color Hex(string hex)
    {
        Color color;
        ColorUtility.TryParseHtmlString(hex, out color);
        return color;
    }

    // White rounded rectangle, 9-sliced, generated once
    internal static Sprite GetRoundedSprite()
    {
        if (!File.Exists(SpritePath))
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance to the rounded rectangle edge, 1px anti-aliasing
                    float cx = Mathf.Clamp(x + 0.5f, CornerPixels, size - CornerPixels);
                    float cy = Mathf.Clamp(y + 0.5f, CornerPixels, size - CornerPixels);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(CornerPixels - d + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(SpritePath));
            File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SpritePath);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(CornerPixels, CornerPixels, CornerPixels, CornerPixels);
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    // Font material preset: black outline + soft drop shadow
    private static Material GetTitleMaterial(TMP_FontAsset font)
    {
        if (font == null || font.material == null) return null;

        Material material = AssetDatabase.LoadAssetAtPath<Material>(TitleMaterialPath);
        if (material == null)
        {
            material = new Material(font.material);
            Directory.CreateDirectory(Path.GetDirectoryName(TitleMaterialPath));
            AssetDatabase.CreateAsset(material, TitleMaterialPath);
        }

        material.CopyPropertiesFromMaterial(font.material);
        material.shaderKeywords = font.material.shaderKeywords;
        material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.1f);
        material.EnableKeyword("OUTLINE_ON");
        material.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.7f));
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.5f);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.8f);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.3f);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }
}
