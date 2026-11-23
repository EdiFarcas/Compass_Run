using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CompassRun.Survival
{
    /// <summary>
    /// Controls the modern dark-translucent glassmorphism Survival HUD in the bottom-left corner:
    /// - Health: 5 distinct crimson segments with smooth transitions & discreet pulse at 1 HP
    /// - Stamina: 3 distinct luminous cyan segments with smooth continuous fill/drain & sprint glow
    /// - Hunger: 3 distinct warm amber-orange segments with smooth transitions & empty warning accent
    /// </summary>
    [DisallowMultipleComponent]
    public class SurvivalHUDController : MonoBehaviour
    {
        [Header("Referință SurvivalManager (auto-detectată dacă este goală)")]
        [SerializeField] private SurvivalManager survivalManager;

        [Header("Referințe Card Glassmorphism")]
        [SerializeField] private CanvasGroup hudCanvasGroup;
        [SerializeField] private Image cardBorderImage;

        [Header("Segmente UI (Fill Images în ordine de la stânga la dreapta)")]
        [SerializeField] private List<Image> healthSegmentFills = new List<Image>();
        [SerializeField] private List<Image> staminaSegmentFills = new List<Image>();
        [SerializeField] private List<Image> hungerSegmentFills = new List<Image>();

        [Header("Iconițe & Etichete (opțional pentru accente de stare)")]
        [SerializeField] private Image healthIconImage;
        [SerializeField] private Image staminaIconImage;
        [SerializeField] private Image hungerIconImage;
        [SerializeField] private TextMeshProUGUI healthValueLabel;
        [SerializeField] private TextMeshProUGUI staminaValueLabel;
        [SerializeField] private TextMeshProUGUI hungerValueLabel;

        [Header("Paletă Modernă Outdoors / Forest Glassmorphism")]
        [SerializeField] private Color healthActiveColor = new Color(0.898f, 0.282f, 0.302f, 1f);        // #E5484D Modern Crimson
        [SerializeField] private Color healthCriticalPulseColor = new Color(1.000f, 0.420f, 0.455f, 1f); // Bright Crimson Glow
        [SerializeField] private Color staminaActiveColor = new Color(0.220f, 0.741f, 0.973f, 1f);       // #38BDF8 Luminous Cyan
        [SerializeField] private Color staminaSprintBoostColor = new Color(0.486f, 0.859f, 1.000f, 1f);  // Bright Sprint Cyan
        [SerializeField] private Color hungerActiveColor = new Color(0.961f, 0.620f, 0.043f, 1f);        // #F59E0B Warm Amber-Orange
        [SerializeField] private Color hungerStarvingIconColor = new Color(0.920f, 0.320f, 0.250f, 1f);  // Warning Coral-Red
        [SerializeField] private Color segmentEmptyColor = new Color(0.110f, 0.145f, 0.165f, 0.38f);     // Translucent Dark Slate
        [SerializeField] private Color cardBorderNormalColor = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color cardBorderCriticalColor = new Color(0.94f, 0.27f, 0.31f, 0.58f);

        [Header("Animații & Tranziții Line")]
        [Min(1f)]
        [SerializeField] private float segmentTransitionSpeed = 8.5f;

        [Min(0.5f)]
        [SerializeField] private float criticalPulseSpeed = 3.6f;

        [Range(0f, 0.25f)]
        [SerializeField] private float criticalPulseScaleBoost = 0.12f;

        private float[] healthCurrentLevels;
        private float[] staminaCurrentLevels;
        private float[] hungerCurrentLevels;

        private int targetHealth = 5;
        private int targetHunger = 3;
        private float targetStamina = 3f;
        private bool isSprinting;
        private bool isCritical;
        private bool isDead;

        private void Awake()
        {
            InitializeLevelArrays();
        }

        private void OnEnable()
        {
            ResolveManager();
            Subscribe();
            SyncImmediatelyFromManager();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            UpdateHudVisibility(dt);
            UpdateHealthSegments(dt);
            UpdateStaminaSegments(dt);
            UpdateHungerSegments(dt);
            UpdateBorderAccent(dt);
        }

        private void InitializeLevelArrays()
        {
            healthCurrentLevels = new float[healthSegmentFills.Count];
            for (int i = 0; i < healthCurrentLevels.Length; i++) healthCurrentLevels[i] = 1f;

            staminaCurrentLevels = new float[staminaSegmentFills.Count];
            for (int i = 0; i < staminaCurrentLevels.Length; i++) staminaCurrentLevels[i] = 1f;

            hungerCurrentLevels = new float[hungerSegmentFills.Count];
            for (int i = 0; i < hungerCurrentLevels.Length; i++) hungerCurrentLevels[i] = 1f;
        }

        private void ResolveManager()
        {
            if (survivalManager == null)
                survivalManager = SurvivalManager.Instance != null ? SurvivalManager.Instance : FindAnyObjectByType<SurvivalManager>();
        }

        private void Subscribe()
        {
            if (survivalManager == null)
                return;

            survivalManager.OnHealthChanged += HandleHealthChanged;
            survivalManager.OnHungerChanged += HandleHungerChanged;
            survivalManager.OnStaminaChanged += HandleStaminaChanged;
            survivalManager.OnSprintStateChanged += HandleSprintStateChanged;
            survivalManager.OnCriticalStateChanged += HandleCriticalStateChanged;
            survivalManager.OnPlayerDied += HandlePlayerDied;
            survivalManager.OnStateReset += HandleStateReset;
        }

        private void Unsubscribe()
        {
            if (survivalManager == null)
                return;

            survivalManager.OnHealthChanged -= HandleHealthChanged;
            survivalManager.OnHungerChanged -= HandleHungerChanged;
            survivalManager.OnStaminaChanged -= HandleStaminaChanged;
            survivalManager.OnSprintStateChanged -= HandleSprintStateChanged;
            survivalManager.OnCriticalStateChanged -= HandleCriticalStateChanged;
            survivalManager.OnPlayerDied -= HandlePlayerDied;
            survivalManager.OnStateReset -= HandleStateReset;
        }

        private void SyncImmediatelyFromManager()
        {
            if (survivalManager == null)
                return;

            targetHealth = survivalManager.CurrentHealth;
            targetHunger = survivalManager.CurrentHunger;
            targetStamina = survivalManager.CurrentStamina;
            isSprinting = survivalManager.IsSprinting;
            isCritical = survivalManager.IsCritical;
            isDead = survivalManager.IsDead;

            RefreshTextLabels();
        }

        private void HandleHealthChanged(int current, int max)
        {
            targetHealth = current;
            RefreshTextLabels();
        }

        private void HandleHungerChanged(int current, int max)
        {
            targetHunger = current;
            RefreshTextLabels();
        }

        private void HandleStaminaChanged(float current, int max)
        {
            targetStamina = current;
            RefreshTextLabels();
        }

        private void HandleSprintStateChanged(bool sprinting)
        {
            isSprinting = sprinting;
        }

        private void HandleCriticalStateChanged(bool critical)
        {
            isCritical = critical;
        }

        private void HandlePlayerDied()
        {
            isDead = true;
            isCritical = false;
            isSprinting = false;
        }

        private void HandleStateReset()
        {
            SyncImmediatelyFromManager();
        }

        private void RefreshTextLabels()
        {
            if (healthValueLabel != null)
            {
                int maxH = survivalManager != null ? survivalManager.MaxHealth : healthSegmentFills.Count;
                healthValueLabel.text = $"{targetHealth}/{maxH}";
            }

            if (hungerValueLabel != null)
            {
                int maxF = survivalManager != null ? survivalManager.MaxHunger : hungerSegmentFills.Count;
                hungerValueLabel.text = $"{targetHunger}/{maxF}";
            }

            if (staminaValueLabel != null)
            {
                int maxS = survivalManager != null ? survivalManager.MaxStamina : staminaSegmentFills.Count;
                int filledSegments = Mathf.CeilToInt(targetStamina - 0.05f);
                filledSegments = Mathf.Clamp(filledSegments, 0, maxS);
                staminaValueLabel.text = $"{filledSegments}/{maxS}";
            }
        }

        private void UpdateHudVisibility(float dt)
        {
            if (hudCanvasGroup == null)
                return;

            float targetAlpha = isDead ? 0f : 1f;
            hudCanvasGroup.alpha = Mathf.MoveTowards(hudCanvasGroup.alpha, targetAlpha, 4.5f * dt);
        }

        private void UpdateHealthSegments(float dt)
        {
            if (healthCurrentLevels == null || healthCurrentLevels.Length != healthSegmentFills.Count)
                InitializeLevelArrays();

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * criticalPulseSpeed * Mathf.PI);

            for (int i = 0; i < healthSegmentFills.Count; i++)
            {
                Image fillImg = healthSegmentFills[i];
                if (fillImg == null)
                    continue;

                float desired = i < targetHealth ? 1f : 0f;
                healthCurrentLevels[i] = Mathf.MoveTowards(healthCurrentLevels[i], desired, segmentTransitionSpeed * dt);
                float level = healthCurrentLevels[i];

                // Ultimul segment de viață (index 0) pulsează discret la 1 HP
                bool pulseThisSegment = isCritical && !isDead && i == 0;
                Color activeColor = pulseThisSegment
                    ? Color.Lerp(healthActiveColor, healthCriticalPulseColor, pulse)
                    : healthActiveColor;

                fillImg.color = Color.Lerp(segmentEmptyColor, activeColor, level);
                fillImg.fillAmount = 1f;

                // Ușoară animație de scalare pe segmentul care scade/crește sau pulsează
                float baseScale = Mathf.Lerp(0.92f, 1.0f, level);
                float pulseScale = pulseThisSegment ? (1f + pulse * criticalPulseScaleBoost) : 1f;
                fillImg.rectTransform.localScale = new Vector3(baseScale * pulseScale, baseScale * (pulseThisSegment ? (1f + pulse * criticalPulseScaleBoost * 1.4f) : 1f), 1f);
            }

            if (healthIconImage != null)
            {
                Color iconTarget = isCritical && !isDead
                    ? Color.Lerp(healthActiveColor, healthCriticalPulseColor, pulse)
                    : healthActiveColor;
                healthIconImage.color = Color.Lerp(healthIconImage.color, iconTarget, 10f * dt);
                float iconScale = isCritical && !isDead ? (1f + pulse * 0.14f) : 1f;
                healthIconImage.rectTransform.localScale = Vector3.one * iconScale;
            }
        }

        private void UpdateStaminaSegments(float dt)
        {
            if (staminaCurrentLevels == null || staminaCurrentLevels.Length != staminaSegmentFills.Count)
                InitializeLevelArrays();

            Color activeCol = isSprinting ? staminaSprintBoostColor : staminaActiveColor;

            for (int i = 0; i < staminaSegmentFills.Count; i++)
            {
                Image fillImg = staminaSegmentFills[i];
                if (fillImg == null)
                    continue;

                float desiredFill = Mathf.Clamp01(targetStamina - i);
                staminaCurrentLevels[i] = Mathf.MoveTowards(staminaCurrentLevels[i], desiredFill, segmentTransitionSpeed * 1.4f * dt);
                float level = staminaCurrentLevels[i];

                if (fillImg.type == Image.Type.Filled)
                {
                    fillImg.fillAmount = level;
                    fillImg.color = Color.Lerp(segmentEmptyColor, activeCol, Mathf.Clamp01(level * 1.35f + 0.15f));
                }
                else
                {
                    fillImg.color = Color.Lerp(segmentEmptyColor, activeCol, level);
                }

                float scaleY = isSprinting && level > 0.05f ? 1.08f : 1.0f;
                Vector3 currentScale = fillImg.rectTransform.localScale;
                fillImg.rectTransform.localScale = Vector3.Lerp(currentScale, new Vector3(1f, scaleY, 1f), 10f * dt);
            }

            if (staminaIconImage != null)
            {
                Color targetIconCol = targetStamina <= 0.05f
                    ? new Color(staminaActiveColor.r, staminaActiveColor.g, staminaActiveColor.b, 0.45f)
                    : activeCol;
                staminaIconImage.color = Color.Lerp(staminaIconImage.color, targetIconCol, 10f * dt);
            }
        }

        private void UpdateHungerSegments(float dt)
        {
            if (hungerCurrentLevels == null || hungerCurrentLevels.Length != hungerSegmentFills.Count)
                InitializeLevelArrays();

            bool starving = targetHunger <= 0 && !isDead;
            float starvePulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f * Mathf.PI);

            for (int i = 0; i < hungerSegmentFills.Count; i++)
            {
                Image fillImg = hungerSegmentFills[i];
                if (fillImg == null)
                    continue;

                float desired = i < targetHunger ? 1f : 0f;
                hungerCurrentLevels[i] = Mathf.MoveTowards(hungerCurrentLevels[i], desired, segmentTransitionSpeed * dt);
                float level = hungerCurrentLevels[i];

                Color emptyCol = starving
                    ? Color.Lerp(segmentEmptyColor, new Color(hungerStarvingIconColor.r, hungerStarvingIconColor.g, hungerStarvingIconColor.b, 0.25f), starvePulse)
                    : segmentEmptyColor;

                fillImg.color = Color.Lerp(emptyCol, hungerActiveColor, level);
                float scale = Mathf.Lerp(0.92f, 1.0f, level);
                fillImg.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }

            if (hungerIconImage != null)
            {
                Color targetIcon = starving
                    ? Color.Lerp(hungerActiveColor, hungerStarvingIconColor, starvePulse)
                    : hungerActiveColor;
                hungerIconImage.color = Color.Lerp(hungerIconImage.color, targetIcon, 10f * dt);
            }
        }

        private void UpdateBorderAccent(float dt)
        {
            if (cardBorderImage == null)
                return;

            if (isCritical && !isDead)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * criticalPulseSpeed * Mathf.PI);
                Color target = Color.Lerp(cardBorderNormalColor, cardBorderCriticalColor, 0.35f + 0.65f * pulse);
                cardBorderImage.color = Color.Lerp(cardBorderImage.color, target, 8f * dt);
            }
            else
            {
                cardBorderImage.color = Color.Lerp(cardBorderImage.color, cardBorderNormalColor, 6f * dt);
            }
        }
    }
}
