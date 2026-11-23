using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace CompassRun.Survival
{
    /// <summary>
    /// Displays a clean, modern glassmorphism Game Over screen ("Ai pierdut" + "Reia jocul")
    /// in front of the player when health reaches 0. Works with both mouse clicks in XR Device Simulator
    /// and VR controller raycast (TrackedDeviceGraphicRaycaster + XRUIInputModule).
    /// </summary>
    [DisallowMultipleComponent]
    public class SurvivalGameOverUI : MonoBehaviour
    {
        [Header("Referințe (auto-detectate dacă sunt goale)")]
        [SerializeField] private SurvivalManager survivalManager;
        [SerializeField] private SurvivalCriticalEffects criticalEffects;
        [SerializeField] private Camera targetCamera;

        [Header("Elemente UI Game Over")]
        [SerializeField] private Canvas gameOverCanvas;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform panelTransform;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private Button restartButton;

        [Header("Poziționare în fața camerei (VR & Simulator)")]
        [Range(0.7f, 2.5f)]
        [SerializeField] private float distanceFromCamera = 1.25f;

        [SerializeField] private float verticalOffset = -0.02f;

        [Min(1f)]
        [SerializeField] private float fadeInSpeed = 4.0f;

        private bool isShowing;
        private bool isReloading;

        private void Awake()
        {
            ResolveReferences();
            EnsureEventSystemForXRAndMouse();

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(HandleRestartClicked);
                restartButton.onClick.AddListener(HandleRestartClicked);
            }

            if (titleText != null)
                titleText.text = "Ai pierdut";

            if (subtitleText != null && string.IsNullOrWhiteSpace(subtitleText.text))
                subtitleText.text = "Ai rămas fără viață în pădure";

            HideImmediate();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (survivalManager != null)
            {
                survivalManager.OnPlayerDied += HandlePlayerDied;
                survivalManager.OnStateReset += HandleStateReset;
            }
        }

        private void OnDisable()
        {
            if (survivalManager != null)
            {
                survivalManager.OnPlayerDied -= HandlePlayerDied;
                survivalManager.OnStateReset -= HandleStateReset;
            }
        }

        private void Update()
        {
            if (!isShowing)
                return;

            // Ne asigurăm că cursorul rămâne deblocat cât timp ecranul Game Over e afișat (pentru click cu mouse în simulator)
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;

            if (!Cursor.visible)
                Cursor.visible = true;

            if (canvasGroup != null && canvasGroup.alpha < 1f)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1f, fadeInSpeed * Time.unscaledDeltaTime);
            }

            if (panelTransform != null)
            {
                panelTransform.localScale = Vector3.Lerp(panelTransform.localScale, Vector3.one, 9f * Time.unscaledDeltaTime);
            }
        }

        private void ResolveReferences()
        {
            if (survivalManager == null)
                survivalManager = SurvivalManager.Instance != null ? SurvivalManager.Instance : FindAnyObjectByType<SurvivalManager>();

            if (criticalEffects == null)
                criticalEffects = FindAnyObjectByType<SurvivalCriticalEffects>();

            if (targetCamera == null)
            {
                XROrigin origin = survivalManager != null && survivalManager.PlayerXROrigin != null
                    ? survivalManager.PlayerXROrigin
                    : FindAnyObjectByType<XROrigin>();

                targetCamera = origin != null && origin.Camera != null ? origin.Camera : Camera.main;
            }

            if (gameOverCanvas == null)
                gameOverCanvas = GetComponent<Canvas>();

            if (gameOverCanvas != null && targetCamera != null)
                gameOverCanvas.worldCamera = targetCamera;
        }

        /// <summary>
        /// Asigură prezența unui EventSystem cu XRUIInputModule configurat atât pentru mouse, cât și pentru VR raycast.
        /// </summary>
        private void EnsureEventSystemForXRAndMouse()
        {
            EventSystem eventSystem = FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject esGo = new GameObject("EventSystem");
                eventSystem = esGo.AddComponent<EventSystem>();
                XRUIInputModule xrInput = esGo.AddComponent<XRUIInputModule>();
                xrInput.enableXRInput = true;
                xrInput.enableMouseInput = true;
                xrInput.enableTouchInput = true;
                xrInput.enableBuiltinActionsAsFallback = true;
            }
            else if (eventSystem.GetComponent<XRUIInputModule>() == null)
            {
                // Păstrăm compatibilitatea cu VR raycast + mouse
                BaseInputModule[] existingModules = eventSystem.GetComponents<BaseInputModule>();
                foreach (BaseInputModule mod in existingModules)
                    mod.enabled = false;

                XRUIInputModule xrInput = eventSystem.gameObject.AddComponent<XRUIInputModule>();
                xrInput.enableXRInput = true;
                xrInput.enableMouseInput = true;
                xrInput.enableTouchInput = true;
                xrInput.enableBuiltinActionsAsFallback = true;
            }
        }

        private void HandlePlayerDied()
        {
            ShowGameOver();
        }

        private void HandleStateReset()
        {
            HideImmediate();
        }

        public void ShowGameOver()
        {
            ResolveReferences();
            PositionInFrontOfCamera();

            isShowing = true;
            isReloading = false;

            if (gameOverCanvas != null)
                gameOverCanvas.enabled = true;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            if (panelTransform != null)
                panelTransform.localScale = Vector3.one * 0.92f;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void HideImmediate()
        {
            isShowing = false;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (gameOverCanvas != null)
                gameOverCanvas.enabled = false;
        }

        private void PositionInFrontOfCamera()
        {
            if (targetCamera == null)
                return;

            Transform camT = targetCamera.transform;
            Vector3 forwardFlat = Vector3.ProjectOnPlane(camT.forward, Vector3.up);
            if (forwardFlat.sqrMagnitude < 0.001f)
                forwardFlat = camT.forward;
            forwardFlat.Normalize();

            // Plasăm panoul la nivelul ochilor, direct în fața jucătorului, drept și ușor de țintit cu raycast sau mouse
            Vector3 targetPos = camT.position + forwardFlat * distanceFromCamera + Vector3.up * verticalOffset;
            Quaternion targetRot = Quaternion.LookRotation(forwardFlat, Vector3.up);

            transform.SetPositionAndRotation(targetPos, targetRot);
        }

        /// <summary>
        /// Apelat la apăsarea butonului "Reia jocul": resetează valorile & efectele și reîncarcă scena curentă.
        /// </summary>
        public void HandleRestartClicked()
        {
            if (isReloading)
                return;

            isReloading = true;
            Time.timeScale = 1f;

            // Resetăm explicit starea și efectele de cameră înainte de reîncărcare
            if (criticalEffects != null)
                criticalEffects.ResetEffects();

            if (survivalManager != null)
                survivalManager.ResetSurvivalState();

            Scene activeScene = SceneManager.GetActiveScene();

            if (activeScene.buildIndex >= 0)
            {
                SceneManager.LoadScene(activeScene.buildIndex, LoadSceneMode.Single);
            }
            else
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(activeScene.path))
                {
                    EditorSceneManager.LoadSceneInPlayMode(activeScene.path, new LoadSceneParameters(LoadSceneMode.Single));
                    return;
                }
#endif
                SceneManager.LoadScene(activeScene.name, LoadSceneMode.Single);
            }
        }
    }
}
