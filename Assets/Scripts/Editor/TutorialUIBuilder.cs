using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Touch-only teaching UI (the game is a mobile game: no keyboard / mouse references anywhere).
//
// Tools > Journey > Build Tutorial UI        (game scene, needs GameUICanvas from "Build Game Over UI")
//   GameUICanvas
//   └─ TutorialOverlay (behind the Game Over card, never blocks touches)
//      ├─ FireHint  left half  (#FFA502 tint): label card with a mini phone (left half lit) + tapping hand
//      └─ JumpHint  right half (#2ED573 tint): label card with a mini phone (right half lit) + tapping hand
//   TutorialManager (wired to BlockManager, Hero and the hints)
//
// Tools > Journey > Build How To Play Panel  (ScoreMenuScene)
//   MainMenu_UI/HowToPlay_Btn "NASIL OYNANIR" (copy of the character button, Quit moves one row down)
//   HowToPlay (HowToPlayPanel) └─ Overlay └─ Card: landscape phone, left half fire / right half jump,
//                                             two thumbs tapping, "EĞİTİMİ SIFIRLA", "TAMAM"
public static class TutorialUIBuilder
{
    private const string IconFolder = "Assets/Textures/UI";
    private const string BoldFontPath = "Assets/Prefabs/UI/Fonts/OpenSans-Bold SDF.asset";
    private const string SemiboldFontPath = "Assets/Prefabs/UI/Fonts/OpenSans-Semibold SDF.asset";

    // Phone sprite layout (fractions of the sprite): the screen sits inside the bezel
    private const float ScreenInsetX = 0.07f;
    private const float ScreenInsetY = 0.12f;

    private static readonly Color Orange = new Color32(0xFF, 0xA5, 0x02, 0xFF);
    private static readonly Color Green = new Color32(0x2E, 0xD5, 0x73, 0xFF);

    // ================================================================ tutorial (game scene)

    [MenuItem("Tools/Journey/Build Tutorial UI")]
    public static void BuildTutorialUI()
    {
        GameObject canvas = GameObject.Find("GameUICanvas");
        BlockManager blockManager = Object.FindFirstObjectByType<BlockManager>(FindObjectsInactive.Include);
        HeroCharacterController hero = Object.FindFirstObjectByType<HeroCharacterController>(FindObjectsInactive.Include);
        if (EditorApplication.isPlaying || canvas == null || blockManager == null || hero == null)
        {
            Debug.LogError("TutorialUIBuilder: open the game scene (not playing) and run 'Build Game Over UI' first.");
            return;
        }

        Undo.SetCurrentGroupName("Build Tutorial UI");
        int group = Undo.GetCurrentGroup();
        DestroyExisting(canvas.transform.Find("TutorialOverlay"));
        TutorialManager oldManager = Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include);
        if (oldManager != null) Undo.DestroyObjectImmediate(oldManager.gameObject);

        Assets assets = LoadAssets();

        RectTransform overlay = GameOverUIBuilder.CreateRect("TutorialOverlay", canvas.transform);
        Undo.RegisterCreatedObjectUndo(overlay.gameObject, "Build Tutorial UI");
        GameOverUIBuilder.Stretch(overlay);
        overlay.SetSiblingIndex(0); // under the Game Over card

        // The fire hand sits towards the outer edge so it never covers the hero (left of center on screen)
        Hint fire = CreateHint(overlay, "FireHint", true, -230f, "EKRANIN SOLUNA DOKUN", "ATEŞ ET", Orange, assets);
        Hint jump = CreateHint(overlay, "JumpHint", false, 0f, "EKRANIN SAĞINA DOKUN", "ZIPLA", Green, assets);

        GameObject managerGo = new GameObject("TutorialManager");
        Undo.RegisterCreatedObjectUndo(managerGo, "Build Tutorial UI");
        TutorialManager manager = managerGo.AddComponent<TutorialManager>();
        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("blockManager").objectReferenceValue = blockManager;
        so.FindProperty("hero").objectReferenceValue = hero;
        so.FindProperty("fireHint").objectReferenceValue = fire.root;
        so.FindProperty("jumpHint").objectReferenceValue = jump.root;
        so.FindProperty("fireHand").objectReferenceValue = fire.hand;
        so.FindProperty("jumpHand").objectReferenceValue = jump.hand;
        so.FindProperty("fireRipple").objectReferenceValue = fire.ripple;
        so.FindProperty("jumpRipple").objectReferenceValue = jump.ripple;
        so.ApplyModifiedPropertiesWithoutUndo();

        fire.root.SetActive(false);
        jump.root.SetActive(false);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(canvas.scene);
        Selection.activeGameObject = managerGo;
        Debug.Log("TutorialUIBuilder: touch tutorial hints + TutorialManager built. Save the scene to keep them.", managerGo);
    }

    private struct Hint
    {
        public GameObject root;
        public RectTransform hand;
        public RectTransform ripple;
    }

    private struct Assets
    {
        public TMP_FontAsset bold;
        public TMP_FontAsset semibold;
        public Sprite rounded;
        public Sprite hand;
        public Sprite ring;
        public Sprite phone;
        public Sprite gun;
        public Sprite jump;
    }

    // One screen half: tinted zone, label card (mini phone + text) on top, tapping hand in the middle.
    // Nothing here is a raycast target: touches must reach ScreenTouchController.
    private static Hint CreateHint(RectTransform parent, string name, bool leftHalf, float handX, string line1, string line2,
        Color accent, Assets assets)
    {
        RectTransform root = GameOverUIBuilder.CreateRect(name, parent);
        float anchorX = leftHalf ? 0f : 0.5f;
        root.anchorMin = new Vector2(anchorX, 0f);
        root.anchorMax = new Vector2(anchorX + 0.5f, 1f);
        root.offsetMin = root.offsetMax = Vector2.zero;
        Image zone = root.gameObject.AddComponent<Image>();
        zone.color = new Color(accent.r, accent.g, accent.b, 0.14f);
        zone.raycastTarget = false;

        RectTransform card = GameOverUIBuilder.CreateRect("Label", root);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = new Vector2(0f, 200f);
        card.sizeDelta = new Vector2(720f, 220f);
        GameOverUIBuilder.AddRounded(card.gameObject, assets.rounded, new Color(0.10f, 0.11f, 0.18f, 0.9f)).raycastTarget = false;

        // Mini phone: which half to touch
        RectTransform phone = CreatePhone(card, new Vector2(210f, 105f), assets);
        phone.anchorMin = phone.anchorMax = new Vector2(0f, 0.5f);
        phone.anchoredPosition = new Vector2(145f, 0f);
        RectTransform lit = CreateScreenHalf(phone, leftHalf, accent, 0.9f);
        CreateScreenHalf(phone, !leftHalf, Color.white, 0.12f);
        RectTransform miniHand = GameOverUIBuilder.CreateRect("Finger", lit);
        miniHand.anchorMin = miniHand.anchorMax = new Vector2(0.5f, 0.5f);
        miniHand.pivot = new Vector2(0.5f, 0.92f);
        miniHand.anchoredPosition = Vector2.zero;
        miniHand.sizeDelta = new Vector2(64f, 64f);
        Image miniHandImage = miniHand.gameObject.AddComponent<Image>();
        miniHandImage.sprite = assets.hand;
        miniHandImage.raycastTarget = false;
        AddOutline(miniHand.gameObject, 2f);

        TextMeshProUGUI title = GameOverUIBuilder.CreateText("Line1", card, line1, assets.semibold, 30f, Color.white, Vector2.zero, new Vector2(430f, 46f));
        LeftColumn(title.rectTransform, 270f, -62f);
        title.characterSpacing = 2f;
        TextMeshProUGUI action = GameOverUIBuilder.CreateText("Line2", card, line2, assets.bold, 70f, accent, Vector2.zero, new Vector2(430f, 90f));
        LeftColumn(action.rectTransform, 270f, -140f);
        action.fontStyle = FontStyles.Bold;
        action.characterSpacing = 4f;

        // Big tapping hand + ripple inside the half to touch
        RectTransform ripple = GameOverUIBuilder.CreateRect("Ripple", root);
        ripple.anchorMin = ripple.anchorMax = new Vector2(0.5f, 0.5f);
        ripple.anchoredPosition = new Vector2(handX, -70f);
        ripple.sizeDelta = new Vector2(220f, 220f);
        Image rippleImage = ripple.gameObject.AddComponent<Image>();
        rippleImage.sprite = assets.ring;
        rippleImage.color = accent;
        rippleImage.raycastTarget = false;

        RectTransform handRect = GameOverUIBuilder.CreateRect("Hand", root);
        handRect.anchorMin = handRect.anchorMax = new Vector2(0.5f, 0.5f);
        handRect.pivot = new Vector2(0.5f, 0.92f); // fingertip on the ripple center
        handRect.anchoredPosition = new Vector2(handX, -70f);
        handRect.sizeDelta = new Vector2(190f, 190f);
        Image handImage = handRect.gameObject.AddComponent<Image>();
        handImage.sprite = assets.hand;
        handImage.raycastTarget = false;
        AddOutline(handRect.gameObject, 4f);
        GameOverUIBuilder.AddShadow(handRect.gameObject, new Color(0f, 0f, 0f, 0.45f), new Vector2(0f, -10f));

        return new Hint { root = root.gameObject, hand = handRect, ripple = ripple };
    }

    // Left-aligned text column starting at x (from the card's left edge), y from the card's top
    private static void LeftColumn(RectTransform rect, float x, float y)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Left;
    }

    // ================================================================ how to play (menu scene)

    [MenuItem("Tools/Journey/Build How To Play Panel")]
    public static void BuildHowToPlay()
    {
        Canvas canvas = null;
        Transform menu = null;
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Transform m = c.transform.Find("MainMenu_UI");
            if (m != null) { canvas = c; menu = m; break; }
        }
        Transform characterButton = menu != null ? menu.Find("Character_Btn") : null;
        Transform quitButton = menu != null ? menu.Find("Quit_Btn") : null;
        if (EditorApplication.isPlaying || canvas == null || characterButton == null)
        {
            Debug.LogError("TutorialUIBuilder: open ScoreMenuScene (not playing); MainMenu_UI/Character_Btn not found.");
            return;
        }

        Undo.SetCurrentGroupName("Build How To Play Panel");
        int group = Undo.GetCurrentGroup();
        DestroyExisting(canvas.transform.Find("HowToPlay"));

        Assets assets = LoadAssets();

        // ---- popup
        RectTransform holder = GameOverUIBuilder.CreateRect("HowToPlay", canvas.transform);
        Undo.RegisterCreatedObjectUndo(holder.gameObject, "Build How To Play Panel");
        GameOverUIBuilder.Stretch(holder);
        holder.SetAsLastSibling();
        HowToPlayPanel panel = holder.gameObject.AddComponent<HowToPlayPanel>();

        RectTransform overlay = GameOverUIBuilder.CreateRect("Overlay", holder);
        GameOverUIBuilder.Stretch(overlay);
        Image dim = overlay.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.75f);
        Button dimButton = overlay.gameObject.AddComponent<Button>(); // tap outside the card closes it
        dimButton.transition = Selectable.Transition.None;
        CanvasGroup groupComponent = overlay.gameObject.AddComponent<CanvasGroup>();

        RectTransform card = GameOverUIBuilder.CreateRect("Card", overlay);
        card.sizeDelta = new Vector2(1060f, 800f);
        GameOverUIBuilder.AddRounded(card.gameObject, assets.rounded, GameOverUIBuilder.Hex("#1A1D2E"));
        GameOverUIBuilder.AddShadow(card.gameObject, new Color(0f, 0f, 0f, 0.55f), new Vector2(0f, -10f));
        Outline border = card.gameObject.AddComponent<Outline>();
        border.effectColor = GameOverUIBuilder.Hex("#2F3555");
        border.effectDistance = new Vector2(2f, -2f);

        TextMeshProUGUI title = GameOverUIBuilder.CreateText("Title", card, "NASIL OYNANIR", assets.bold, 54f, Color.white, new Vector2(0f, -66f), new Vector2(900f, 80f));
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 4f;
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(GameOverUIBuilder.Hex("#FFD32A"), GameOverUIBuilder.Hex("#FFD32A"),
            GameOverUIBuilder.Hex("#FFA502"), GameOverUIBuilder.Hex("#FFA502"));

        // ---- landscape phone held with two thumbs
        RectTransform phone = CreatePhone(card, new Vector2(760f, 380f), assets);
        GameOverUIBuilder.TopCenter(phone, new Vector2(0f, -320f), new Vector2(760f, 380f));
        RectTransform left = CreateScreenHalf(phone, true, Orange, 0.3f);
        RectTransform right = CreateScreenHalf(phone, false, Green, 0.3f);
        FillHalf(left, assets.gun, Orange, "ATEŞ ET", "Engelleri patlat", assets);
        FillHalf(right, assets.jump, Green, "ZIPLA", "Boşlukları ve taşları aş", assets);

        RectTransform split = GameOverUIBuilder.CreateRect("Split", phone);
        split.anchorMin = new Vector2(0.5f, ScreenInsetY);
        split.anchorMax = new Vector2(0.5f, 1f - ScreenInsetY);
        split.sizeDelta = new Vector2(4f, 0f);
        Image splitImage = split.gameObject.AddComponent<Image>();
        splitImage.color = new Color(1f, 1f, 1f, 0.5f);
        splitImage.raycastTarget = false;

        CreateThumb(phone, left);
        CreateThumb(phone, right);

        TextMeshProUGUI caption = GameOverUIBuilder.CreateText("Caption", card, "Telefonu yatay tut, iki başparmağınla oyna",
            assets.semibold, 26f, GameOverUIBuilder.Hex("#A4B0BE"), new Vector2(0f, -585f), new Vector2(900f, 40f));
        caption.characterSpacing = 1f;

        TextMeshProUGUI status = GameOverUIBuilder.CreateText("Status", card, "", assets.semibold, 24f, GameOverUIBuilder.Hex("#FFD32A"), new Vector2(0f, -628f), new Vector2(900f, 40f));

        Button reset = GameOverUIBuilder.CreateButton("ResetTutorialButton", card, "EĞİTİMİ SIFIRLA", assets.bold, assets.rounded,
            GameOverUIBuilder.Hex("#FF4757"), GameOverUIBuilder.Hex("#B8323E"), 40f);
        Button close = GameOverUIBuilder.CreateButton("CloseButton", card, "TAMAM", assets.bold, assets.rounded,
            Green, GameOverUIBuilder.Hex("#1E9E55"), 40f);
        SideBySide(reset, -215f);
        SideBySide(close, 215f);
        reset.GetComponentInChildren<TMP_Text>().fontSize = 34f;

        SerializedObject so = new SerializedObject(panel);
        so.FindProperty("panel").objectReferenceValue = groupComponent;
        so.FindProperty("card").objectReferenceValue = card;
        so.FindProperty("statusText").objectReferenceValue = status;
        so.ApplyModifiedPropertiesWithoutUndo();

        UnityEventTools.AddPersistentListener(reset.onClick, panel.ResetTutorial);
        UnityEventTools.AddPersistentListener(close.onClick, panel.Close);
        UnityEventTools.AddPersistentListener(dimButton.onClick, panel.Close);
        overlay.gameObject.SetActive(false);

        // ---- menu button (same look as "KARAKTER SEÇ"), Quit moves one row down
        Transform existing = menu.Find("HowToPlay_Btn");
        GameObject buttonGo;
        if (existing != null)
        {
            buttonGo = existing.gameObject;
            Undo.RecordObject(buttonGo.GetComponent<Button>(), "Build How To Play Panel");
        }
        else
        {
            buttonGo = Object.Instantiate(characterButton.gameObject, menu);
            Undo.RegisterCreatedObjectUndo(buttonGo, "Build How To Play Panel");
            buttonGo.name = "HowToPlay_Btn";
            buttonGo.transform.SetSiblingIndex(characterButton.GetSiblingIndex() + 1);
            RectTransform characterRect = (RectTransform)characterButton;
            RectTransform rect = (RectTransform)buttonGo.transform;
            float row = 105f;
            rect.anchoredPosition = characterRect.anchoredPosition - new Vector2(0f, row);
            if (quitButton != null)
            {
                Undo.RecordObject(quitButton, "Build How To Play Panel");
                ((RectTransform)quitButton).anchoredPosition = rect.anchoredPosition - new Vector2(0f, row);
            }
        }
        TMP_Text label = buttonGo.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            Undo.RecordObject(label, "Build How To Play Panel");
            label.text = "NASIL OYNANIR";
        }
        Button howToButton = buttonGo.GetComponent<Button>();
        howToButton.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddPersistentListener(howToButton.onClick, panel.Open);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = holder.gameObject;
        Debug.Log("TutorialUIBuilder: 'NASIL OYNANIR' button + panel built. Save the scene to keep them.", holder);
    }

    // Icon circle + action + hint inside one screen half
    private static void FillHalf(RectTransform half, Sprite icon, Color accent, string action, string hint, Assets assets)
    {
        RectTransform badge = GameOverUIBuilder.CreateRect("Icon", half);
        badge.anchorMin = badge.anchorMax = new Vector2(0.5f, 0.5f);
        badge.anchoredPosition = new Vector2(0f, 52f);
        badge.sizeDelta = new Vector2(110f, 110f);
        Image circle = badge.gameObject.AddComponent<Image>();
        circle.sprite = assets.rounded;
        circle.type = Image.Type.Sliced;
        circle.pixelsPerUnitMultiplier = 0.45f; // 24px corners -> ~53px: a circle at 110px
        circle.color = accent;
        circle.raycastTarget = false;
        GameOverUIBuilder.AddShadow(badge.gameObject, new Color(0f, 0f, 0f, 0.35f), new Vector2(0f, -5f));

        RectTransform glyph = GameOverUIBuilder.CreateRect("Glyph", badge);
        glyph.sizeDelta = new Vector2(74f, 74f);
        Image glyphImage = glyph.gameObject.AddComponent<Image>();
        glyphImage.sprite = icon;
        glyphImage.raycastTarget = false;

        TextMeshProUGUI head = GameOverUIBuilder.CreateText("Action", half, action, assets.bold, 40f, Color.white, Vector2.zero, new Vector2(330f, 52f));
        Centered(head.rectTransform, new Vector2(0f, -36f));
        head.fontStyle = FontStyles.Bold;
        head.characterSpacing = 3f;
        TextMeshProUGUI sub = GameOverUIBuilder.CreateText("Hint", half, hint, assets.semibold, 22f, new Color(1f, 1f, 1f, 0.8f), Vector2.zero, new Vector2(330f, 34f));
        Centered(sub.rectTransform, new Vector2(0f, -76f));
    }

    // Thumb tapping the lower part of the half, coming in from below the phone
    private static void CreateThumb(RectTransform phone, RectTransform half)
    {
        RectTransform thumb = GameOverUIBuilder.CreateRect("Thumb_" + half.name, phone);
        bool left = half.anchorMin.x < 0.5f;
        float x = left ? 0.29f : 0.71f;
        thumb.anchorMin = thumb.anchorMax = new Vector2(x, 0f);
        thumb.pivot = new Vector2(0.5f, 0.92f);   // fingertip
        thumb.anchoredPosition = new Vector2(0f, 70f);
        thumb.sizeDelta = new Vector2(130f, 130f);
        thumb.localRotation = Quaternion.Euler(0f, 0f, left ? -18f : 18f);
        Image image = thumb.gameObject.AddComponent<Image>();
        image.sprite = GetIcon("Icon_Hand", DrawHand);
        image.raycastTarget = false;
        AddOutline(thumb.gameObject, 3f);
    }

    private static void Centered(RectTransform rect, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
    }

    private static void SideBySide(Button button, float x)
    {
        RectTransform rect = (RectTransform)button.transform;
        rect.sizeDelta = new Vector2(400f, 96f);
        rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
    }

    // ================================================================ phone diagram

    // Landscape phone frame; the screen area is filled by CreateScreenHalf
    private static RectTransform CreatePhone(RectTransform parent, Vector2 size, Assets assets)
    {
        RectTransform phone = GameOverUIBuilder.CreateRect("Phone", parent);
        phone.sizeDelta = size;

        RectTransform screen = GameOverUIBuilder.CreateRect("Screen", phone);
        screen.anchorMin = new Vector2(ScreenInsetX, ScreenInsetY);
        screen.anchorMax = new Vector2(1f - ScreenInsetX, 1f - ScreenInsetY);
        screen.offsetMin = screen.offsetMax = Vector2.zero;
        Image screenImage = screen.gameObject.AddComponent<Image>();
        screenImage.color = GameOverUIBuilder.Hex("#11131F");
        screenImage.raycastTarget = false;

        RectTransform frame = GameOverUIBuilder.CreateRect("Frame", phone);
        GameOverUIBuilder.Stretch(frame);
        Image frameImage = frame.gameObject.AddComponent<Image>();
        frameImage.sprite = assets.phone;
        frameImage.color = GameOverUIBuilder.Hex("#DFE4EA");
        frameImage.raycastTarget = false;
        return phone;
    }

    private static RectTransform CreateScreenHalf(RectTransform phone, bool leftHalf, Color color, float alpha)
    {
        RectTransform half = GameOverUIBuilder.CreateRect(leftHalf ? "LeftHalf" : "RightHalf", phone);
        half.anchorMin = new Vector2(leftHalf ? ScreenInsetX : 0.5f, ScreenInsetY);
        half.anchorMax = new Vector2(leftHalf ? 0.5f : 1f - ScreenInsetX, 1f - ScreenInsetY);
        half.offsetMin = half.offsetMax = Vector2.zero;
        half.SetSiblingIndex(1); // above the screen, below the frame
        Image image = half.gameObject.AddComponent<Image>();
        image.color = new Color(color.r, color.g, color.b, alpha);
        image.raycastTarget = false;
        return half;
    }

    // ================================================================ shared

    private static void AddOutline(GameObject go, float distance)
    {
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = GameOverUIBuilder.Hex("#1A1D2E");
        outline.effectDistance = new Vector2(distance, -distance);
    }

    private static void DestroyExisting(Transform previous)
    {
        if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
    }

    private static Assets LoadAssets()
    {
        TMP_FontAsset bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
        TMP_FontAsset semibold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SemiboldFontPath);
        if (bold == null) bold = TMP_Settings.defaultFontAsset;
        if (semibold == null) semibold = bold;
        return new Assets
        {
            bold = bold,
            semibold = semibold,
            rounded = GameOverUIBuilder.GetRoundedSprite(),
            hand = GetIcon("Icon_Hand", DrawHand),
            ring = GetIcon("Icon_Ring", DrawRing),
            phone = GetIcon("Icon_PhoneLandscape", 256, 128, DrawPhone),
            gun = GetIcon("Icon_Gun", DrawGun),
            jump = GetIcon("Icon_Jump", DrawJump)
        };
    }

    // ---- procedural icons (white, anti-aliased by 4x4 supersampling, generated once)

    private static Sprite GetIcon(string name, Func<float, float, bool> inside)
    {
        return GetIcon(name, 128, 128, inside);
    }

    // inside(x, y): y in 0..1 (height), x in 0..width/height, so shapes keep their proportions
    private static Sprite GetIcon(string name, int width, int height, Func<float, float, bool> inside)
    {
        string path = IconFolder + "/" + name + ".png";
        if (!File.Exists(path))
        {
            const int samples = 4;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < samples; sy++)
                        for (int sx = 0; sx < samples; sx++)
                            if (inside((x + (sx + 0.5f) / samples) / height, (y + (sy + 0.5f) / samples) / height)) hits++;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, hits / (float)(samples * samples)));
                }
            }
            Directory.CreateDirectory(IconFolder);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static bool RoundRect(float x, float y, float x0, float y0, float x1, float y1, float r)
    {
        float cx = Mathf.Clamp(x, x0 + r, x1 - r);
        float cy = Mathf.Clamp(y, y0 + r, y1 - r);
        return x >= x0 && x <= x1 && y >= y0 && y <= y1 && (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;
    }

    private static bool Triangle(float x, float y, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = (x - b.x) * (a.y - b.y) - (a.x - b.x) * (y - b.y);
        float d2 = (x - c.x) * (b.y - c.y) - (b.x - c.x) * (y - c.y);
        float d3 = (x - a.x) * (c.y - a.y) - (c.x - a.x) * (y - a.y);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0;
        bool pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    // Pointing hand: index finger up (fingertip at the top center), palm, thumb, folded fingers
    private static bool DrawHand(float x, float y)
    {
        return RoundRect(x, y, 0.40f, 0.42f, 0.58f, 0.98f, 0.09f)
            || RoundRect(x, y, 0.26f, 0.06f, 0.80f, 0.56f, 0.15f)
            || RoundRect(x, y, 0.12f, 0.28f, 0.34f, 0.47f, 0.09f)
            || RoundRect(x, y, 0.56f, 0.40f, 0.80f, 0.60f, 0.08f);
    }

    private static bool DrawRing(float x, float y)
    {
        float d = Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.5f));
        return d <= 0.47f && d >= 0.40f;
    }

    // Landscape phone bezel (x 0..2): rounded body minus the screen, small camera hole on the left
    private static bool DrawPhone(float x, float y)
    {
        bool body = RoundRect(x, y, 0.02f, 0.03f, 1.98f, 0.97f, 0.16f);
        bool screen = RoundRect(x, y, 2f * ScreenInsetX, ScreenInsetY, 2f - 2f * ScreenInsetX, 1f - ScreenInsetY, 0.03f);
        bool camera = (x - 0.075f) * (x - 0.075f) + (y - 0.5f) * (y - 0.5f) <= 0.022f * 0.022f;
        return body && !screen && !camera;
    }

    // Side view blaster: body, barrel, sight, grip
    private static bool DrawGun(float x, float y)
    {
        return RoundRect(x, y, 0.06f, 0.50f, 0.76f, 0.72f, 0.05f)
            || RoundRect(x, y, 0.72f, 0.55f, 0.95f, 0.67f, 0.03f)
            || RoundRect(x, y, 0.62f, 0.70f, 0.70f, 0.79f, 0.02f)
            || RoundRect(x, y, 0.14f, 0.16f, 0.34f, 0.56f, 0.05f)
            || RoundRect(x, y, 0.34f, 0.40f, 0.44f, 0.52f, 0.03f);
    }

    // Up arrow over the ground line
    private static bool DrawJump(float x, float y)
    {
        return RoundRect(x, y, 0.41f, 0.20f, 0.59f, 0.62f, 0.03f)
            || Triangle(x, y, new Vector2(0.16f, 0.58f), new Vector2(0.84f, 0.58f), new Vector2(0.5f, 0.95f))
            || RoundRect(x, y, 0.08f, 0.04f, 0.92f, 0.11f, 0.03f);
    }
}
