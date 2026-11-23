using System.Collections.Generic;
using System.IO;
using System.Linq;
using CompassRun.Survival;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace CompassRun.Editor
{
    /// <summary>
    /// Builds all assets for the Survival HUD & System (crisp anti-aliased 9-slice UI sprites,
    /// URP Critical VolumeProfile, Assets/Prefabs/UI/SurvivalHUD.prefab) and cleanly integrates
    /// everything into Assets/Scenes/SimulareVR.unity without modifying Starter Assets prefabs.
    /// </summary>
    [InitializeOnLoad]
    public static class SetupSurvivalSystem
    {
        private const string ScenePath = "Assets/Scenes/SimulareVR.unity";
        private const string UiPrefabFolder = "Assets/Prefabs/UI";
        private const string SpritesFolder = "Assets/Prefabs/UI/Sprites";
        private const string HudPrefabPath = "Assets/Prefabs/UI/SurvivalHUD.prefab";
        private const string VolumeProfilePath = "Assets/Prefabs/UI/SurvivalCriticalVolumeProfile.asset";
        private const string TmpFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        static SetupSurvivalSystem()
        {
            // Rulează automat o singură dată la prima compilare dacă prefab-ul nu există încă
            EditorApplication.delayCall += AutoBuildIfMissing;
        }

        private static void AutoBuildIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
                return;

            if (File.Exists(HudPrefabPath))
                return;

            RunSetup(interactive: false);
        }

        [MenuItem("Tools/Compass Run/Setup Survival System")]
        public static void RunSetupFromMenu()
        {
            RunSetup(interactive: true);
        }

        public static void RunSetup(bool interactive)
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[SetupSurvivalSystem] Oprește Play Mode înainte de a rula setup-ul.");
                return;
            }

            EnsureFolders();

            // 1. Generăm sprite-uri moderne anti-aliased (colțuri rotunjite, bordură fină, pilule, iconițe)
            Sprite cardBgSprite = CreateAndImportSprite(
                SpritesFolder + "/HUD_Card_Rounded.png",
                SurvivalUISpriteGenerator.CreateRoundedRectTexture(128, 24f),
                new Vector4(30f, 30f, 30f, 30f));

            Sprite cardBorderSprite = CreateAndImportSprite(
                SpritesFolder + "/HUD_Card_Border.png",
                SurvivalUISpriteGenerator.CreateRoundedRectTexture(128, 24f, 2.4f, outlineOnly: true),
                new Vector4(30f, 30f, 30f, 30f));

            Sprite pillSprite = CreateAndImportSprite(
                SpritesFolder + "/HUD_Segment_Pill.png",
                SurvivalUISpriteGenerator.CreateRoundedRectTexture(64, 14f),
                new Vector4(16f, 12f, 16f, 12f));

            Sprite staminaPillSprite = CreateAndImportSprite(
                SpritesFolder + "/HUD_Stamina_Pill.png",
                SurvivalUISpriteGenerator.CreateRoundedRectTexture(128, 28, 12f),
                Vector4.zero);

            Sprite buttonSprite = CreateAndImportSprite(
                SpritesFolder + "/HUD_Button_Rounded.png",
                SurvivalUISpriteGenerator.CreateRoundedRectTexture(128, 20f),
                new Vector4(26f, 22f, 26f, 22f));

            Sprite heartSprite = CreateAndImportSprite(
                SpritesFolder + "/Icon_Health_Heart.png",
                SurvivalUISpriteGenerator.CreateHeartIconTexture(64),
                Vector4.zero);

            Sprite boltSprite = CreateAndImportSprite(
                SpritesFolder + "/Icon_Stamina_Bolt.png",
                SurvivalUISpriteGenerator.CreateBoltIconTexture(64),
                Vector4.zero);

            Sprite hungerSprite = CreateAndImportSprite(
                SpritesFolder + "/Icon_Hunger_Fruit.png",
                SurvivalUISpriteGenerator.CreateHungerIconTexture(64),
                Vector4.zero);

            // 2. Creăm/actualizăm profilul URP Post-Processing pentru starea critică (1 HP)
            VolumeProfile criticalProfile = CreateOrUpdateVolumeProfile();

            // 3. Construim și salvăm prefab-ul modern Assets/Prefabs/UI/SurvivalHUD.prefab
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
            if (fontAsset == null)
                fontAsset = TMP_Settings.defaultFontAsset;

            GameObject hudPrefab = BuildAndSaveHudPrefab(
                fontAsset, cardBgSprite, cardBorderSprite, pillSprite, staminaPillSprite, buttonSprite,
                heartSprite, boltSprite, hungerSprite);

            // 4. Adăugăm SimulareVR.unity în Build Settings pentru reload sigur la "Reia jocul"
            EnsureSceneInBuildSettings(ScenePath);

            // 5. Integrăm curat în scena SimulareVR.unity
            IntegrateIntoSimulareVR(hudPrefab, criticalProfile, interactive);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SetupSurvivalSystem] Sistemul Survival, HUD-ul modern (Assets/Prefabs/UI/SurvivalHUD.prefab) și scena SimulareVR au fost configurate cu succes!");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(UiPrefabFolder);
            EnsureFolder(SpritesFolder);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
            {
                if (!AssetDatabase.IsValidFolder(parent))
                    EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static Sprite CreateAndImportSprite(string assetPath, Texture2D generatedTex, Vector4 border)
        {
            byte[] pngBytes = generatedTex.EncodeToPNG();
            Object.DestroyImmediate(generatedTex);

            File.WriteAllBytes(assetPath, pngBytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.spriteBorder = border;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static VolumeProfile CreateOrUpdateVolumeProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }

            if (!profile.TryGet(out Vignette vignette))
            {
                vignette = profile.Add<Vignette>(true);
                AssetDatabase.AddObjectToAsset(vignette, profile);
            }
            vignette.active = true;
            vignette.color.overrideState = true;
            vignette.color.value = new Color(0.42f, 0.03f, 0.06f, 1f);
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.40f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.48f;
            vignette.rounded.overrideState = true;
            vignette.rounded.value = false;

            if (!profile.TryGet(out DepthOfField dof))
            {
                dof = profile.Add<DepthOfField>(true);
                AssetDatabase.AddObjectToAsset(dof, profile);
            }
            dof.active = true;
            dof.mode.overrideState = true;
            dof.mode.value = DepthOfFieldMode.Gaussian;
            dof.gaussianStart.overrideState = true;
            dof.gaussianStart.value = 1.6f;
            dof.gaussianEnd.overrideState = true;
            dof.gaussianEnd.value = 11.0f;
            dof.gaussianMaxRadius.overrideState = true;
            dof.gaussianMaxRadius.value = 0.68f;

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static GameObject BuildAndSaveHudPrefab(
            TMP_FontAsset font,
            Sprite cardBg,
            Sprite cardBorder,
            Sprite pill,
            Sprite staminaPill,
            Sprite buttonSprite,
            Sprite heartIcon,
            Sprite boltIcon,
            Sprite hungerIcon)
        {
            GameObject root = new GameObject("SurvivalHUD");

            // ==================== 1. HUD STÂNGA-JOS (GLASSMORPHISM) ====================
            GameObject hudCanvasGo = new GameObject("BottomLeftHUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            hudCanvasGo.layer = 5; // UI
            hudCanvasGo.transform.SetParent(root.transform, false);

            Canvas hudCanvas = hudCanvasGo.GetComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.WorldSpace;
            hudCanvas.sortingOrder = 10;

            RectTransform hudRect = hudCanvasGo.GetComponent<RectTransform>();
            hudRect.sizeDelta = new Vector2(328f, 136f);
            hudRect.localScale = Vector3.one * 0.00072f;

            CanvasGroup hudGroup = hudCanvasGo.GetComponent<CanvasGroup>();
            hudGroup.alpha = 1f;
            hudGroup.interactable = false;
            hudGroup.blocksRaycasts = false;

            hudCanvasGo.AddComponent<SurvivalHUDAnchor>();

            // Fundal dark translucent glassmorphism
            Image glassBg = CreateImage("GlassCardBackground", hudCanvasGo.transform, cardBg, new Color(0.055f, 0.075f, 0.088f, 0.80f), Image.Type.Sliced);
            StretchFull(glassBg.rectTransform);

            // Reflexie discretă superioară (efect modern de sticlă mată)
            Image topSheen = CreateImage("TopGlassSheen", glassBg.transform, cardBg, new Color(0.85f, 0.94f, 0.98f, 0.045f), Image.Type.Sliced);
            topSheen.rectTransform.anchorMin = new Vector2(0.01f, 0.56f);
            topSheen.rectTransform.anchorMax = new Vector2(0.99f, 0.985f);
            topSheen.rectTransform.offsetMin = Vector2.zero;
            topSheen.rectTransform.offsetMax = Vector2.zero;

            // Bordură fină translucidă
            Image borderImg = CreateImage("GlassCardBorder", hudCanvasGo.transform, cardBorder, new Color(1f, 1f, 1f, 0.16f), Image.Type.Sliced);
            StretchFull(borderImg.rectTransform);

            // Container vertical pentru cele 3 bare segmentate
            GameObject rowsGo = new GameObject("StatsContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            rowsGo.layer = 5;
            rowsGo.transform.SetParent(hudCanvasGo.transform, false);
            RectTransform rowsRect = rowsGo.GetComponent<RectTransform>();
            StretchFull(rowsRect, 15f, 15f, 13f, 13f);

            VerticalLayoutGroup vlg = rowsGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = true;

            Color crimson = new Color(0.898f, 0.282f, 0.302f, 1f);
            Color cyan = new Color(0.220f, 0.741f, 0.973f, 1f);
            Color amber = new Color(0.961f, 0.620f, 0.043f, 1f);

            List<Image> healthFills = BuildStatRow(
                rowsGo.transform, "HealthRow", "VIAȚĂ", 5, heartIcon, pill, pill, font, crimson,
                useFilledBar: false, out Image healthIconImg, out TextMeshProUGUI healthValLabel);

            List<Image> staminaFills = BuildStatRow(
                rowsGo.transform, "StaminaRow", "STAMINĂ", 3, boltIcon, pill, staminaPill, font, cyan,
                useFilledBar: true, out Image staminaIconImg, out TextMeshProUGUI staminaValLabel);

            List<Image> hungerFills = BuildStatRow(
                rowsGo.transform, "HungerRow", "FOAME", 3, hungerIcon, pill, pill, font, amber,
                useFilledBar: false, out Image hungerIconImg, out TextMeshProUGUI hungerValLabel);

            // Atașăm și legăm SurvivalHUDController pe root
            SurvivalHUDController hudController = root.AddComponent<SurvivalHUDController>();
            SerializedObject hudSo = new SerializedObject(hudController);
            hudSo.FindProperty("hudCanvasGroup").objectReferenceValue = hudGroup;
            hudSo.FindProperty("cardBorderImage").objectReferenceValue = borderImg;
            hudSo.FindProperty("healthIconImage").objectReferenceValue = healthIconImg;
            hudSo.FindProperty("staminaIconImage").objectReferenceValue = staminaIconImg;
            hudSo.FindProperty("hungerIconImage").objectReferenceValue = hungerIconImg;
            hudSo.FindProperty("healthValueLabel").objectReferenceValue = healthValLabel;
            hudSo.FindProperty("staminaValueLabel").objectReferenceValue = staminaValLabel;
            hudSo.FindProperty("hungerValueLabel").objectReferenceValue = hungerValLabel;
            AssignImageList(hudSo.FindProperty("healthSegmentFills"), healthFills);
            AssignImageList(hudSo.FindProperty("staminaSegmentFills"), staminaFills);
            AssignImageList(hudSo.FindProperty("hungerSegmentFills"), hungerFills);
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            // ==================== 2. ECRAN GAME OVER (WORLD SPACE VR + MOUSE) ====================
            GameObject goCanvasObj = new GameObject("GameOverCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(TrackedDeviceGraphicRaycaster));
            goCanvasObj.layer = 5;
            goCanvasObj.transform.SetParent(root.transform, false);

            Canvas goCanvas = goCanvasObj.GetComponent<Canvas>();
            goCanvas.renderMode = RenderMode.WorldSpace;
            goCanvas.sortingOrder = 50;
            goCanvas.enabled = false;

            RectTransform goRect = goCanvasObj.GetComponent<RectTransform>();
            goRect.sizeDelta = new Vector2(460f, 276f);
            goRect.localScale = Vector3.one * 0.00145f;

            CanvasGroup goGroup = goCanvasObj.GetComponent<CanvasGroup>();
            goGroup.alpha = 0f;
            goGroup.interactable = false;
            goGroup.blocksRaycasts = false;

            // Panou central animat
            GameObject panelGo = new GameObject("GameOverPanel", typeof(RectTransform));
            panelGo.layer = 5;
            panelGo.transform.SetParent(goCanvasObj.transform, false);
            RectTransform panelRect = panelGo.GetComponent<RectTransform>();
            StretchFull(panelRect);

            Image goBg = CreateImage("BackdropCard", panelGo.transform, cardBg, new Color(0.048f, 0.062f, 0.075f, 0.92f), Image.Type.Sliced);
            StretchFull(goBg.rectTransform);

            Image goBorder = CreateImage("CardBorder", panelGo.transform, cardBorder, new Color(0.898f, 0.282f, 0.302f, 0.48f), Image.Type.Sliced);
            StretchFull(goBorder.rectTransform);

            // Bandă fină de accent crimson sus
            Image accentBar = CreateImage("TopAccentPill", panelGo.transform, pill, crimson, Image.Type.Sliced);
            accentBar.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            accentBar.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            accentBar.rectTransform.pivot = new Vector2(0.5f, 1f);
            accentBar.rectTransform.sizeDelta = new Vector2(88f, 5f);
            accentBar.rectTransform.anchoredPosition = new Vector2(0f, -22f);

            // Titlu "Ai pierdut"
            TextMeshProUGUI titleTmp = CreateTmpText("TitleText", panelGo.transform, "Ai pierdut", font, 44f, FontStyles.Bold, new Color(0.973f, 0.980f, 0.988f, 1f), TextAlignmentOptions.Center);
            titleTmp.rectTransform.anchorMin = new Vector2(0.08f, 0.56f);
            titleTmp.rectTransform.anchorMax = new Vector2(0.92f, 0.84f);
            titleTmp.rectTransform.offsetMin = Vector2.zero;
            titleTmp.rectTransform.offsetMax = Vector2.zero;

            // Subtitlu curat
            TextMeshProUGUI subTmp = CreateTmpText("SubtitleText", panelGo.transform, "Ai rămas fără viață în pădure", font, 17f, FontStyles.Normal, new Color(0.76f, 0.82f, 0.87f, 0.90f), TextAlignmentOptions.Center);
            subTmp.rectTransform.anchorMin = new Vector2(0.10f, 0.42f);
            subTmp.rectTransform.anchorMax = new Vector2(0.90f, 0.56f);
            subTmp.rectTransform.offsetMin = Vector2.zero;
            subTmp.rectTransform.offsetMax = Vector2.zero;

            // Buton interactiv "Reia jocul"
            GameObject btnGo = new GameObject("RestartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.layer = 5;
            btnGo.transform.SetParent(panelGo.transform, false);
            RectTransform btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0f);
            btnRect.anchorMax = new Vector2(0.5f, 0f);
            btnRect.pivot = new Vector2(0.5f, 0f);
            btnRect.sizeDelta = new Vector2(236f, 56f);
            btnRect.anchoredPosition = new Vector2(0f, 34f);

            Image btnImg = btnGo.GetComponent<Image>();
            btnImg.sprite = buttonSprite;
            btnImg.type = Image.Type.Sliced;
            btnImg.color = Color.white;
            btnImg.raycastTarget = true;

            Button restartBtn = btnGo.GetComponent<Button>();
            restartBtn.targetGraphic = btnImg;
            ColorBlock colors = restartBtn.colors;
            colors.normalColor = new Color(0.84f, 0.22f, 0.25f, 1f);
            colors.highlightedColor = new Color(0.96f, 0.30f, 0.33f, 1f);
            colors.pressedColor = new Color(0.68f, 0.15f, 0.18f, 1f);
            colors.selectedColor = new Color(0.94f, 0.28f, 0.31f, 1f);
            colors.fadeDuration = 0.12f;
            restartBtn.colors = colors;

            TextMeshProUGUI btnTmp = CreateTmpText("ButtonLabel", btnGo.transform, "Reia jocul", font, 21f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            StretchFull(btnTmp.rectTransform);
            btnTmp.raycastTarget = false;

            // Atașăm SurvivalGameOverUI pe GameOverCanvas
            SurvivalGameOverUI gameOverUi = goCanvasObj.AddComponent<SurvivalGameOverUI>();
            SerializedObject goSo = new SerializedObject(gameOverUi);
            goSo.FindProperty("gameOverCanvas").objectReferenceValue = goCanvas;
            goSo.FindProperty("canvasGroup").objectReferenceValue = goGroup;
            goSo.FindProperty("panelTransform").objectReferenceValue = panelRect;
            goSo.FindProperty("titleText").objectReferenceValue = titleTmp;
            goSo.FindProperty("subtitleText").objectReferenceValue = subTmp;
            goSo.FindProperty("restartButton").objectReferenceValue = restartBtn;
            goSo.ApplyModifiedPropertiesWithoutUndo();

            // Salvăm prefab-ul în Assets/Prefabs/UI/SurvivalHUD.prefab
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            Object.DestroyImmediate(root);
            return savedPrefab;
        }

        private static List<Image> BuildStatRow(
            Transform parent,
            string rowName,
            string labelText,
            int segmentCount,
            Sprite iconSprite,
            Sprite trackPillSprite,
            Sprite fillPillSprite,
            TMP_FontAsset font,
            Color accentColor,
            bool useFilledBar,
            out Image iconImage,
            out TextMeshProUGUI valueLabel)
        {
            GameObject rowGo = new GameObject(rowName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGo.layer = 5;
            rowGo.transform.SetParent(parent, false);

            HorizontalLayoutGroup hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Iconiță modernă
            GameObject iconContainer = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconContainer.layer = 5;
            iconContainer.transform.SetParent(rowGo.transform, false);
            iconImage = iconContainer.GetComponent<Image>();
            iconImage.sprite = iconSprite;
            iconImage.preserveAspect = true;
            iconImage.color = accentColor;
            iconImage.raycastTarget = false;

            LayoutElement iconLe = iconContainer.GetComponent<LayoutElement>();
            iconLe.preferredWidth = 20f;
            iconLe.preferredHeight = 20f;
            iconLe.flexibleWidth = 0f;

            // Etichetă clară TextMeshPro
            TextMeshProUGUI labelTmp = CreateTmpText("Label", rowGo.transform, labelText, font, 11.5f, FontStyles.Bold, new Color(0.90f, 0.93f, 0.95f, 0.94f), TextAlignmentOptions.MidlineLeft);
            labelTmp.characterSpacing = 3.5f;
            labelTmp.raycastTarget = false;
            LayoutElement labelLe = labelTmp.gameObject.AddComponent<LayoutElement>();
            labelLe.preferredWidth = 66f;
            labelLe.preferredHeight = 22f;
            labelLe.flexibleWidth = 0f;

            // Grup segmente distincte
            GameObject segGroupGo = new GameObject("Segments", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            segGroupGo.layer = 5;
            segGroupGo.transform.SetParent(rowGo.transform, false);

            LayoutElement segGroupLe = segGroupGo.GetComponent<LayoutElement>();
            segGroupLe.flexibleWidth = 1f;
            segGroupLe.preferredHeight = 14f;

            HorizontalLayoutGroup segHlg = segGroupGo.GetComponent<HorizontalLayoutGroup>();
            segHlg.spacing = segmentCount >= 5 ? 5f : 7f;
            segHlg.childAlignment = TextAnchor.MiddleCenter;
            segHlg.childControlWidth = true;
            segHlg.childControlHeight = true;
            segHlg.childForceExpandWidth = true;
            segHlg.childForceExpandHeight = true;

            var fills = new List<Image>(segmentCount);
            for (int i = 0; i < segmentCount; i++)
            {
                // Fundal segment (recessed dark track)
                Image trackImg = CreateImage($"Segment_{i + 1}", segGroupGo.transform, trackPillSprite, new Color(0.03f, 0.045f, 0.055f, 0.72f), Image.Type.Sliced);
                trackImg.raycastTarget = false;

                // Umplere segment
                Image fillImg = CreateImage(
                    "Fill",
                    trackImg.transform,
                    fillPillSprite,
                    accentColor,
                    useFilledBar ? Image.Type.Filled : Image.Type.Sliced);

                fillImg.raycastTarget = false;
                if (useFilledBar)
                {
                    fillImg.fillMethod = Image.FillMethod.Horizontal;
                    fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
                    fillImg.fillAmount = 1f;
                }

                StretchFull(fillImg.rectTransform, 1.5f, 1.5f, 1.5f, 1.5f);
                fills.Add(fillImg);
            }

            // Indicator numeric discret (ex. 5/5)
            valueLabel = CreateTmpText("Value", rowGo.transform, $"{segmentCount}/{segmentCount}", font, 10.5f, FontStyles.Bold, new Color(0.62f, 0.70f, 0.76f, 0.88f), TextAlignmentOptions.MidlineRight);
            valueLabel.raycastTarget = false;
            LayoutElement valLe = valueLabel.gameObject.AddComponent<LayoutElement>();
            valLe.preferredWidth = 26f;
            valLe.preferredHeight = 20f;
            valLe.flexibleWidth = 0f;

            return fills;
        }

        private static void IntegrateIntoSimulareVR(GameObject hudPrefab, VolumeProfile criticalProfile, bool interactive)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            bool needToOpenSimulareVR = activeScene.path != ScenePath;

            if (needToOpenSimulareVR)
            {
                if (activeScene.isDirty && !interactive)
                {
                    Debug.LogWarning("[SetupSurvivalSystem] Scena curentă are modificări nesalvate. Deschide SimulareVR.unity și rulează Tools > Compass Run > Setup Survival System.");
                    return;
                }

                if (activeScene.isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;

                activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            XROrigin xrOrigin = Object.FindAnyObjectByType<XROrigin>();
            ContinuousMoveProvider moveProvider = xrOrigin != null
                ? xrOrigin.GetComponentInChildren<ContinuousMoveProvider>(true)
                : Object.FindAnyObjectByType<ContinuousMoveProvider>();
            Camera mainCam = xrOrigin != null && xrOrigin.Camera != null ? xrOrigin.Camera : Camera.main;

            // 1. Activăm Post-Processing pe Main Camera (URP)
            if (mainCam != null)
            {
                UniversalAdditionalCameraData camData = mainCam.GetComponent<UniversalAdditionalCameraData>();
                if (camData == null)
                    camData = mainCam.gameObject.AddComponent<UniversalAdditionalCameraData>();

                if (!camData.renderPostProcessing)
                {
                    camData.renderPostProcessing = true;
                    EditorUtility.SetDirty(camData);
                }
            }

            // 2. Asigurăm un EventSystem compatibil VR Raycast + Mouse Click (XRUIInputModule)
            EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject esGo = new GameObject("EventSystem");
                eventSystem = esGo.AddComponent<EventSystem>();
                XRUIInputModule xrUiModule = esGo.AddComponent<XRUIInputModule>();
                xrUiModule.enableXRInput = true;
                xrUiModule.enableMouseInput = true;
                xrUiModule.enableTouchInput = true;
                xrUiModule.enableBuiltinActionsAsFallback = true;
            }
            else if (eventSystem.GetComponent<XRUIInputModule>() == null)
            {
                foreach (BaseInputModule mod in eventSystem.GetComponents<BaseInputModule>())
                    mod.enabled = false;

                XRUIInputModule xrUiModule = eventSystem.gameObject.AddComponent<XRUIInputModule>();
                xrUiModule.enableXRInput = true;
                xrUiModule.enableMouseInput = true;
                xrUiModule.enableTouchInput = true;
                xrUiModule.enableBuiltinActionsAsFallback = true;
            }

            // 3. Creăm sau actualizăm obiectul dedicat SurvivalSystem (Manager + CriticalEffects + Volume)
            GameObject survivalRoot = GameObject.Find("SurvivalSystem");
            if (survivalRoot == null)
                survivalRoot = new GameObject("SurvivalSystem");

            SurvivalManager manager = survivalRoot.GetComponent<SurvivalManager>();
            if (manager == null)
                manager = survivalRoot.AddComponent<SurvivalManager>();

            SerializedObject managerSo = new SerializedObject(manager);
            managerSo.FindProperty("xrOrigin").objectReferenceValue = xrOrigin;
            managerSo.FindProperty("moveProvider").objectReferenceValue = moveProvider;
            managerSo.FindProperty("playerCamera").objectReferenceValue = mainCam;
            managerSo.FindProperty("staminaDrainPerSecond").floatValue = 0.1f;
            managerSo.FindProperty("staminaRegenPerSecond").floatValue = 0.3f;
            managerSo.FindProperty("staminaRegenDelay").floatValue = 0f;
            managerSo.FindProperty("minStaminaToStartSprint").floatValue = 0.05f;
            managerSo.FindProperty("movementDetectionThreshold").floatValue = 0.05f;
            managerSo.FindProperty("sprintSpeedMultiplier").floatValue = 1.8f;
            managerSo.ApplyModifiedPropertiesWithoutUndo();

            Transform volTransform = survivalRoot.transform.Find("SurvivalCriticalVolume");
            GameObject volGo = volTransform != null ? volTransform.gameObject : new GameObject("SurvivalCriticalVolume");
            volGo.transform.SetParent(survivalRoot.transform, false);

            Volume criticalVolume = volGo.GetComponent<Volume>();
            if (criticalVolume == null)
                criticalVolume = volGo.AddComponent<Volume>();
            criticalVolume.isGlobal = true;
            criticalVolume.priority = 100f;
            criticalVolume.weight = 0f;
            criticalVolume.sharedProfile = criticalProfile;
            criticalVolume.enabled = false;

            SurvivalCriticalEffects criticalEffects = survivalRoot.GetComponent<SurvivalCriticalEffects>();
            if (criticalEffects == null)
                criticalEffects = survivalRoot.AddComponent<SurvivalCriticalEffects>();

            SerializedObject effectsSo = new SerializedObject(criticalEffects);
            effectsSo.FindProperty("survivalManager").objectReferenceValue = manager;
            effectsSo.FindProperty("targetCamera").objectReferenceValue = mainCam;
            effectsSo.FindProperty("criticalVolume").objectReferenceValue = criticalVolume;
            effectsSo.ApplyModifiedPropertiesWithoutUndo();

            // 4. Instanțiem (sau înlocuim curat) prefab-ul Assets/Prefabs/UI/SurvivalHUD.prefab în scenă
            GameObject existingHud = GameObject.Find("SurvivalHUD");
            if (existingHud != null)
                Object.DestroyImmediate(existingHud);

            GameObject hudInstance = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab, activeScene);
            hudInstance.name = "SurvivalHUD";

            SurvivalHUDController hudCtrl = hudInstance.GetComponent<SurvivalHUDController>();
            if (hudCtrl != null)
            {
                SerializedObject hudCtrlSo = new SerializedObject(hudCtrl);
                hudCtrlSo.FindProperty("survivalManager").objectReferenceValue = manager;
                hudCtrlSo.ApplyModifiedPropertiesWithoutUndo();
            }

            SurvivalHUDAnchor anchor = hudInstance.GetComponentInChildren<SurvivalHUDAnchor>(true);
            if (anchor != null)
            {
                SerializedObject anchorSo = new SerializedObject(anchor);
                anchorSo.FindProperty("targetCamera").objectReferenceValue = mainCam;
                anchorSo.ApplyModifiedPropertiesWithoutUndo();
            }

            SurvivalGameOverUI gameOverUi = hudInstance.GetComponentInChildren<SurvivalGameOverUI>(true);
            if (gameOverUi != null)
            {
                SerializedObject goUiSo = new SerializedObject(gameOverUi);
                goUiSo.FindProperty("survivalManager").objectReferenceValue = manager;
                goUiSo.FindProperty("criticalEffects").objectReferenceValue = criticalEffects;
                goUiSo.FindProperty("targetCamera").objectReferenceValue = mainCam;
                goUiSo.ApplyModifiedPropertiesWithoutUndo();

                Canvas goCanvas = gameOverUi.GetComponent<Canvas>();
                if (goCanvas != null && mainCam != null)
                    goCanvas.worldCamera = mainCam;
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }

        private static void EnsureSceneInBuildSettings(string path)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path))
                return;

            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color, Image.Type type)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = type;
            return img;
        }

        private static TextMeshProUGUI CreateTmpText(
            string name,
            Transform parent,
            string text,
            TMP_FontAsset font,
            float fontSize,
            FontStyles style,
            Color color,
            TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            if (font != null)
                tmp.font = font;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        private static void StretchFull(RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private static void AssignImageList(SerializedProperty listProp, List<Image> images)
        {
            listProp.arraySize = images.Count;
            for (int i = 0; i < images.Count; i++)
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = images[i];
        }
    }
}
