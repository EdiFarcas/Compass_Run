using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace CompassRun.Survival
{
    /// <summary>
    /// Handles visual and camera feedback when the player reaches critical health (1 HP):
    /// - Lightweight URP Post-Processing Volume (Gaussian Depth of Field + dark-crimson pulsing Vignette)
    /// - VR/XR-safe subtle camera shake applied to an intermediate CameraShakeOffset transform
    ///   between Camera Offset and Main Camera (never conflicts with TrackedPoseDriver).
    /// </summary>
    [DisallowMultipleComponent]
    public class SurvivalCriticalEffects : MonoBehaviour
    {
        [Header("Referințe (auto-detectate dacă sunt goale)")]
        [SerializeField] private SurvivalManager survivalManager;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Volume criticalVolume;

        [Header("Post-Processing (Stare Critică - 1 HP)")]
        [Tooltip("Viteza tranziției line (fade-in / fade-out) pentru efectele de post-procesare și tremurat.")]
        [Min(0.5f)]
        [SerializeField] private float blendSpeed = 2.5f;

        [Tooltip("Culoarea vignetei la 1 HP (roșu închis / crimson).")]
        [SerializeField] private Color vignetteColor = new Color(0.42f, 0.03f, 0.06f, 1f);

        [Range(0f, 0.8f)]
        [SerializeField] private float vignetteBaseIntensity = 0.40f;

        [Range(0f, 0.2f)]
        [SerializeField] private float vignettePulseAmplitude = 0.065f;

        [Min(0.2f)]
        [SerializeField] private float vignettePulseFrequency = 2.2f;

        [Range(0.1f, 1f)]
        [SerializeField] private float vignetteSmoothness = 0.48f;

        [Header("Depth of Field (Gaussian - prietenos cu performanța VR)")]
        [SerializeField] private float dofGaussianStart = 1.6f;
        [SerializeField] private float dofGaussianEnd = 11.0f;
        [Range(0.5f, 1.5f)]
        [SerializeField] private float dofGaussianMaxRadius = 0.68f;

        [Header("Camera Shake VR-Safe (1 HP)")]
        [Tooltip("Activează tremuratul subtil al camerei la 1 HP.")]
        [SerializeField] private bool enableCameraShake = true;

        [Tooltip("Amplitudinea maximă de translație (metri) în modul Desktop/Simulator.")]
        [Range(0f, 0.03f)]
        [SerializeField] private float shakePositionAmplitude = 0.0085f;

        [Tooltip("Amplitudinea maximă de rotație (grade) în modul Desktop/Simulator.")]
        [Range(0f, 2.5f)]
        [SerializeField] private float shakeRotationDegrees = 0.65f;

        [Tooltip("Multiplicator de reducere aplicat automat în cască VR reală pentru confort.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float vrComfortScale = 0.55f;

        [Tooltip("Frecvența oscilației Perlin pentru senzația organică de epuizare.")]
        [Range(0.5f, 10f)]
        [SerializeField] private float shakeFrequency = 3.6f;

        private Transform shakeOffsetTransform;
        private Vignette vignetteOverride;
        private DepthOfField dofOverride;
        private VolumeProfile runtimeProfileInstance;

        private float currentCriticalWeight;
        private float currentDeathDimWeight;
        private float shakeTime;
        private bool isCriticalActive;
        private bool isDeadActive;

        private void Awake()
        {
            ResolveReferences();
            EnsureCameraPostProcessingEnabled();
            EnsureVolumeSetup();
            EnsureVRSafeShakeRig();
        }

        private void OnEnable()
        {
            if (survivalManager == null)
                survivalManager = SurvivalManager.Instance != null ? SurvivalManager.Instance : FindAnyObjectByType<SurvivalManager>();

            if (survivalManager != null)
            {
                survivalManager.OnCriticalStateChanged += HandleCriticalStateChanged;
                survivalManager.OnPlayerDied += HandlePlayerDied;
                survivalManager.OnStateReset += HandleStateReset;

                isCriticalActive = survivalManager.IsCritical;
                isDeadActive = survivalManager.IsDead;
            }
        }

        private void OnDisable()
        {
            if (survivalManager != null)
            {
                survivalManager.OnCriticalStateChanged -= HandleCriticalStateChanged;
                survivalManager.OnPlayerDied -= HandlePlayerDied;
                survivalManager.OnStateReset -= HandleStateReset;
            }

            ResetShakeTransform();
        }

        private void OnDestroy()
        {
            if (runtimeProfileInstance != null)
            {
                Destroy(runtimeProfileInstance);
                runtimeProfileInstance = null;
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            float targetCritical = isCriticalActive && !isDeadActive ? 1f : 0f;
            float targetDeathDim = isDeadActive ? 1f : 0f;

            currentCriticalWeight = Mathf.MoveTowards(currentCriticalWeight, targetCritical, blendSpeed * dt);
            currentDeathDimWeight = Mathf.MoveTowards(currentDeathDimWeight, targetDeathDim, blendSpeed * 0.9f * dt);

            UpdatePostProcessing(dt);
            UpdateCameraShake(dt);
        }

        private void ResolveReferences()
        {
            if (survivalManager == null)
                survivalManager = SurvivalManager.Instance != null ? SurvivalManager.Instance : FindAnyObjectByType<SurvivalManager>();

            if (targetCamera == null)
            {
                XROrigin origin = survivalManager != null && survivalManager.PlayerXROrigin != null
                    ? survivalManager.PlayerXROrigin
                    : FindAnyObjectByType<XROrigin>();

                if (origin != null && origin.Camera != null)
                    targetCamera = origin.Camera;
                else
                    targetCamera = Camera.main;
            }
        }

        private void EnsureCameraPostProcessingEnabled()
        {
            if (targetCamera == null)
                return;

            UniversalAdditionalCameraData cameraData = targetCamera.GetUniversalAdditionalCameraData();
            if (cameraData != null && !cameraData.renderPostProcessing)
            {
                cameraData.renderPostProcessing = true;
            }
        }

        private void EnsureVolumeSetup()
        {
            if (criticalVolume == null)
            {
                GameObject volumeGo = new GameObject("SurvivalCriticalVolume");
                volumeGo.transform.SetParent(transform, false);
                criticalVolume = volumeGo.AddComponent<Volume>();
            }

            criticalVolume.isGlobal = true;
            criticalVolume.priority = 100f;
            criticalVolume.weight = 0f;

            // Creăm o copie runtime a profilului (sau un profil nou) ca să nu murdărim asset-ul din proiect la runtime
            if (criticalVolume.sharedProfile != null)
            {
                runtimeProfileInstance = Instantiate(criticalVolume.sharedProfile);
            }
            else
            {
                runtimeProfileInstance = ScriptableObject.CreateInstance<VolumeProfile>();
                runtimeProfileInstance.name = "SurvivalCriticalRuntimeProfile";
            }

            criticalVolume.profile = runtimeProfileInstance;

            if (!runtimeProfileInstance.TryGet(out vignetteOverride))
                vignetteOverride = runtimeProfileInstance.Add<Vignette>(true);

            vignetteOverride.active = true;
            vignetteOverride.color.overrideState = true;
            vignetteOverride.color.value = vignetteColor;
            vignetteOverride.intensity.overrideState = true;
            vignetteOverride.intensity.value = vignetteBaseIntensity;
            vignetteOverride.smoothness.overrideState = true;
            vignetteOverride.smoothness.value = vignetteSmoothness;
            vignetteOverride.rounded.overrideState = true;
            vignetteOverride.rounded.value = false;

            if (!runtimeProfileInstance.TryGet(out dofOverride))
                dofOverride = runtimeProfileInstance.Add<DepthOfField>(true);

            dofOverride.active = true;
            dofOverride.mode.overrideState = true;
            dofOverride.mode.value = DepthOfFieldMode.Gaussian;
            dofOverride.gaussianStart.overrideState = true;
            dofOverride.gaussianStart.value = dofGaussianStart;
            dofOverride.gaussianEnd.overrideState = true;
            dofOverride.gaussianEnd.value = dofGaussianEnd;
            dofOverride.gaussianMaxRadius.overrideState = true;
            dofOverride.gaussianMaxRadius.value = dofGaussianMaxRadius;

            criticalVolume.enabled = false;
        }

        /// <summary>
        /// Inserează un nod intermediar CameraShakeOffset între Camera Offset și Main Camera.
        /// Astfel, TrackedPoseDriver de pe Main Camera își scrie în continuare localPosition/localRotation
        /// fără niciun conflict, iar controlerele (copii ai lui Camera Offset) rămân complet stabile.
        /// </summary>
        private void EnsureVRSafeShakeRig()
        {
            if (targetCamera == null)
                return;

            Transform camTransform = targetCamera.transform;
            Transform parent = camTransform.parent;

            if (parent != null && parent.name == "CameraShakeOffset")
            {
                shakeOffsetTransform = parent;
                return;
            }

            GameObject shakeGo = new GameObject("CameraShakeOffset");
            shakeOffsetTransform = shakeGo.transform;
            shakeOffsetTransform.SetParent(parent, false);
            shakeOffsetTransform.localPosition = Vector3.zero;
            shakeOffsetTransform.localRotation = Quaternion.identity;
            shakeOffsetTransform.localScale = Vector3.one;

            // Păstrăm transformul local al camerei intact (TrackedPoseDriver va continua să-l actualizeze curat)
            Vector3 prevLocalPos = camTransform.localPosition;
            Quaternion prevLocalRot = camTransform.localRotation;
            Vector3 prevLocalScale = camTransform.localScale;

            camTransform.SetParent(shakeOffsetTransform, false);
            camTransform.localPosition = prevLocalPos;
            camTransform.localRotation = prevLocalRot;
            camTransform.localScale = prevLocalScale;
        }

        private void UpdatePostProcessing(float dt)
        {
            if (criticalVolume == null)
                return;

            float combinedWeight = Mathf.Max(currentCriticalWeight, currentDeathDimWeight * 0.85f);
            bool shouldEnable = combinedWeight > 0.002f;

            if (criticalVolume.enabled != shouldEnable)
                criticalVolume.enabled = shouldEnable;

            if (!shouldEnable)
            {
                criticalVolume.weight = 0f;
                return;
            }

            criticalVolume.weight = combinedWeight;

            if (vignetteOverride != null)
            {
                // Pulsare discretă tip bătăi de inimă la 1 HP; calmă/întunecată la Game Over
                float pulseWave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * vignettePulseFrequency * Mathf.PI);
                float criticalIntensity = vignetteBaseIntensity + pulseWave * vignettePulseAmplitude;
                float targetIntensity = Mathf.Lerp(criticalIntensity, 0.52f, currentDeathDimWeight);

                Color deathColor = new Color(0.14f, 0.02f, 0.03f, 1f);
                vignetteOverride.color.value = Color.Lerp(vignetteColor, deathColor, currentDeathDimWeight);
                vignetteOverride.intensity.value = targetIntensity;
            }

            if (dofOverride != null)
            {
                // Blur subtil la 1 HP, ușor mai calm pe ecranul Game Over pentru lizibilitate maximă a UI-ului
                dofOverride.active = currentCriticalWeight > 0.01f;
                dofOverride.gaussianMaxRadius.value = Mathf.Lerp(0.5f, dofGaussianMaxRadius, currentCriticalWeight);
            }
        }

        private void UpdateCameraShake(float dt)
        {
            if (shakeOffsetTransform == null)
                return;

            if (!enableCameraShake || currentCriticalWeight <= 0.001f)
            {
                ResetShakeTransform();
                return;
            }

            shakeTime += dt * shakeFrequency;
            float comfortMultiplier = XRSettings.isDeviceActive ? vrComfortScale : 1f;
            float strength = currentCriticalWeight * comfortMultiplier;

            // Zgomot Perlin lin pe 3 axe + ușoară oscilație de epuizare (respirat greu)
            float nx = (Mathf.PerlinNoise(shakeTime, 11.3f) - 0.5f) * 2f;
            float ny = (Mathf.PerlinNoise(27.1f, shakeTime) - 0.5f) * 2f;
            float nz = (Mathf.PerlinNoise(shakeTime * 0.7f, 43.7f) - 0.5f) * 2f;

            // Mic tremurat de oboseală la frecvență joasă
            float sway = Mathf.Sin(Time.unscaledTime * 2.1f) * 0.35f;

            Vector3 posOffset = new Vector3(
                (nx + sway * 0.4f) * shakePositionAmplitude * strength,
                ny * shakePositionAmplitude * 0.85f * strength,
                0f
            );

            Quaternion rotOffset = Quaternion.Euler(
                ny * shakeRotationDegrees * 0.6f * strength,
                nx * shakeRotationDegrees * 0.4f * strength,
                (nz + sway * 0.5f) * shakeRotationDegrees * strength
            );

            shakeOffsetTransform.localPosition = posOffset;
            shakeOffsetTransform.localRotation = rotOffset;
        }

        private void ResetShakeTransform()
        {
            if (shakeOffsetTransform == null)
                return;

            shakeOffsetTransform.localPosition = Vector3.zero;
            shakeOffsetTransform.localRotation = Quaternion.identity;
        }

        private void HandleCriticalStateChanged(bool critical)
        {
            isCriticalActive = critical;
        }

        private void HandlePlayerDied()
        {
            isCriticalActive = false;
            isDeadActive = true;
            ResetShakeTransform();
        }

        private void HandleStateReset()
        {
            ResetEffects();
        }

        /// <summary>Resetează imediat toate efectele vizuale și de cameră la starea normală.</summary>
        public void ResetEffects()
        {
            isCriticalActive = false;
            isDeadActive = false;
            currentCriticalWeight = 0f;
            currentDeathDimWeight = 0f;
            shakeTime = 0f;

            ResetShakeTransform();

            if (criticalVolume != null)
            {
                criticalVolume.weight = 0f;
                criticalVolume.enabled = false;
            }
        }
    }
}
