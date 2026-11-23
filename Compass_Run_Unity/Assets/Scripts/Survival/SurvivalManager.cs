using System;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace CompassRun.Survival
{
    /// <summary>
    /// Central controller for the player's Survival state: Health (HP), Hunger, Stamina,
    /// sprint speed boost integrated with both XR Origin's DynamicMoveProvider (VR) and
    /// XRDeviceSimulator (WASD HMD mode on PC), critical state notifications (1 HP),
    /// death/locomotion lock (0 HP), and Inspector debug tools.
    /// </summary>
    [DisallowMultipleComponent]
    public class SurvivalManager : MonoBehaviour
    {
        public static SurvivalManager Instance { get; private set; }

        [Header("Referințe XR / Mișcare (auto-detectate dacă sunt goale)")]
        [Tooltip("Referință către XR Origin. Dacă rămâne goală, se detectează automat în scenă.")]
        [SerializeField] private XROrigin xrOrigin;

        [Tooltip("Componenta de mișcare continuă (DynamicMoveProvider / ContinuousMoveProvider) de pe XR Origin.")]
        [SerializeField] private ContinuousMoveProvider moveProvider;

        [Tooltip("Camera principală (Main Camera) folosită pentru detecția reală a deplasării pe planul XZ.")]
        [SerializeField] private Camera playerCamera;

        [Header("Valori Maxime (Segmente)")]
        [Min(1)]
        [SerializeField] private int maxHealth = 5;

        [Min(1)]
        [SerializeField] private int maxHunger = 3;

        [Min(1)]
        [SerializeField] private int maxStamina = 3;

        [Header("Timpi Foame & Înfometare (secunde)")]
        [Tooltip("Intervalul (în secunde) după care foamea scade automat cu 1 segment. Implicit: 120s (2 minute).")]
        [Min(1f)]
        [SerializeField] private float hungerTickSeconds = 120f;

        [Tooltip("Intervalul (în secunde) după care viața scade cu 1 segment când foamea este 0. Implicit: 60s (1 minut).")]
        [Min(1f)]
        [SerializeField] private float starvationDamageSeconds = 60f;

        [Header("Stamină & Sprint (VR + XR Device Simulator)")]
        [Tooltip("Multiplicator de viteză aplicat în timpul sprint-ului atât pe DynamicMoveProvider (VR), cât și pe XRDeviceSimulator (WASD pe PC).")]
        [Range(1.1f, 3.5f)]
        [SerializeField] private float sprintSpeedMultiplier = 1.8f;

        [Tooltip("Consum stamină în timpul sprint-ului (unități/secundă). 0.1 = 3/3 consumat complet în 30s (10s per liniuță).")]
        [Min(0.01f)]
        [SerializeField] private float staminaDrainPerSecond = 0.1f;

        [Tooltip("Regenerare stamină când nu alergi (unități/secundă). 0.3 = 0/3 la 3/3 în 10s.")]
        [Min(0.01f)]
        [SerializeField] private float staminaRegenPerSecond = 0.3f;

        [Tooltip("Pauză (secunde) după oprirea sprint-ului înainte de regenerare. 0 = refacere completă exact în 10s.")]
        [Min(0f)]
        [SerializeField] private float staminaRegenDelay = 0f;

        [Tooltip("Stamina minimă necesară pentru a reîncepe sprint-ul după ce a fost epuizată la 0.")]
        [Range(0.01f, 1f)]
        [SerializeField] private float minStaminaToStartSprint = 0.05f;

        [Tooltip("Prag minim de viteză orizontală (m/s) pe planul XZ pentru a considera că jucătorul se deplasează.")]
        [Min(0.01f)]
        [SerializeField] private float movementDetectionThreshold = 0.05f;

        [Tooltip("Dacă este activ, stamina se consumă doar atunci când jucătorul chiar se deplasează pe planul XZ.")]
        [SerializeField] private bool requireMovementToDrainStamina = true;

        [Tooltip("Opțional: tratează automat deplasarea rapidă ca sprint fără apăsarea tastei.")]
        [SerializeField] private bool detectFastMovementAsSprint = false;

        [Header("Taste Sprint (New Input System - Keyboard.current)")]
        [Tooltip("Tastă principală de Sprint (LeftShift).")]
        [SerializeField] private Key sprintKey = Key.LeftShift;

        [Tooltip("Tastă secundară de Sprint (RightShift — fără conflict cu Manipulate Left din XR Device Simulator).")]
        [SerializeField] private Key alternateSprintKey = Key.RightShift;

        [Tooltip("A treia tastă de Sprint (F — lângă WASD, fără conflict cu XR Device Simulator).")]
        [SerializeField] private Key thirdSprintKey = Key.F;

        [Tooltip("Acțiune opțională InputSystem pentru Sprint în VR (implicit folosește Left Thumbstick Click).")]
        [SerializeField] private InputActionProperty customSprintAction;

        [Header("Debug & Testare")]
        [Tooltip("Activează tastele rapide pentru testarea scăderii foamei/vieții și stării critice fără a aștepta minute întregi.")]
        [SerializeField] private bool enableDebugKeys = true;

        [Tooltip("Tastă debug: scade Foamea cu 1 segment.")]
        [SerializeField] private Key debugReduceHungerKey = Key.Minus;

        [Tooltip("Tastă debug: scade Viața cu 1 segment.")]
        [SerializeField] private Key debugReduceHealthKey = Key.LeftBracket;

        [Tooltip("Tastă debug: setează Viața direct la 1 HP (testează efectele critice).")]
        [SerializeField] private Key debugSetCriticalKey = Key.RightBracket;

        [Tooltip("Tastă debug: setează Viața la 0 HP (testează ecranul Game Over).")]
        [SerializeField] private Key debugKillKey = Key.Semicolon;

        [Tooltip("Tastă debug: resetează toate valorile la maxim.")]
        [SerializeField] private Key debugResetAllKey = Key.Equals;

        // Stare curentă
        private int currentHealth;
        private int currentHunger;
        private float currentStamina;
        private bool isSprinting;
        private bool isCritical;
        private bool isDead;

        // Timere interne
        private float hungerTimer;
        private float starvationTimer;
        private float staminaRegenCooldown;
        private bool staminaExhausted;
        private float movementGraceTimer;

        // Integrare Locomotion VR + XR Device Simulator
        private float baseMoveSpeed = 2.5f;
        private bool hasCapturedBaseSpeed;

#pragma warning disable CS0618
        private XRDeviceSimulator xrDeviceSimulator;
#pragma warning restore CS0618
        private float baseSimKeyboardXSpeed = 0.2f;
        private float baseSimKeyboardZSpeed = 0.2f;
        private bool hasCapturedBaseSimSpeed;

        private Vector2 lastPlayerXZ;
        private bool hasInitializedLastXZ;
        private readonly List<LocomotionProvider> disabledLocomotionProviders = new List<LocomotionProvider>();

        // Acțiune VR fallback pentru sprint (Left Thumbstick Click, funcționează și pe tasta 4 în XR Device Simulator)
        private InputAction vrSprintFallbackAction;

        // Proprietăți publice read-only
        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;
        public int MaxHunger => maxHunger;
        public int CurrentHunger => currentHunger;
        public int MaxStamina => maxStamina;
        public float CurrentStamina => currentStamina;
        public float StaminaNormalized => maxStamina > 0 ? Mathf.Clamp01(currentStamina / maxStamina) : 0f;
        public float HungerTickSeconds => hungerTickSeconds;
        public float StarvationDamageSeconds => starvationDamageSeconds;
        public float StaminaDrainPerSecond => staminaDrainPerSecond;
        public float StaminaRegenPerSecond => staminaRegenPerSecond;
        public float StaminaFullDrainSeconds => staminaDrainPerSecond > 0.0001f ? maxStamina / staminaDrainPerSecond : 0f;
        public float StaminaFullRegenSeconds => staminaRegenPerSecond > 0.0001f ? maxStamina / staminaRegenPerSecond : 0f;
        public float HungerTimeRemaining => Mathf.Max(0f, hungerTickSeconds - hungerTimer);
        public float StarvationTimeRemaining => currentHunger <= 0 ? Mathf.Max(0f, starvationDamageSeconds - starvationTimer) : starvationDamageSeconds;
        public bool IsSprinting => isSprinting;
        public bool IsCritical => isCritical;
        public bool IsDead => isDead;
        public bool EnableDebugKeys
        {
            get => enableDebugKeys;
            set => enableDebugKeys = value;
        }
        public XROrigin PlayerXROrigin => xrOrigin;

        // Evenimente pentru decuplare curată cu HUD, Efecte Critice și Game Over UI
        public event Action<int, int> OnHealthChanged;
        public event Action<int, int> OnHungerChanged;
        public event Action<float, int> OnStaminaChanged;
        public event Action<bool> OnSprintStateChanged;
        public event Action<bool> OnCriticalStateChanged;
        public event Action OnPlayerDied;
        public event Action OnStateReset;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[SurvivalManager] Există deja o instanță activă; duplicatul a fost dezactivat.", this);
                enabled = false;
                return;
            }

            Instance = this;
            ResolveXRReferences();
            SetupVRSprintAction();
            InitializeState(notifyListeners: false);
        }

        private void OnEnable()
        {
            vrSprintFallbackAction?.Enable();
            if (customSprintAction.action != null && customSprintAction.reference == null)
                customSprintAction.action.Enable();
        }

        private void OnDisable()
        {
            RestoreAllBaseSpeeds();
            vrSprintFallbackAction?.Disable();
            if (customSprintAction.action != null && customSprintAction.reference == null)
                customSprintAction.action.Disable();
        }

        private void OnDestroy()
        {
            RestoreAllBaseSpeeds();
            vrSprintFallbackAction?.Dispose();
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            // Re-verificăm referințele în Start (ex. dacă XR Origin / XRDeviceSimulator s-au inițializat după Awake)
            ResolveXRReferences();
            hasInitializedLastXZ = false;

            // Emitem starea inițială către toți abonații (HUD, CriticalEffects, GameOverUI)
            BroadcastAllStates();

            if (enableDebugKeys)
            {
                Debug.Log(
                    $"[SurvivalManager] Debug activ — [{debugReduceHungerKey}]: -1 Foame | [{debugReduceHealthKey}]: -1 HP | " +
                    $"[{debugSetCriticalKey}]: 1 HP (Critic) | [{debugKillKey}]: 0 HP (Game Over) | [{debugResetAllKey}]: Reset | " +
                    $"Sprint: [{sprintKey}] / [{alternateSprintKey}] / [{thirdSprintKey}] / VR Left Stick Click.",
                    this);
            }
        }

        private void Update()
        {
            if (enableDebugKeys)
                HandleDebugInput();

            if (isDead)
                return;

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            UpdateHungerAndStarvation(dt);
            UpdateStaminaAndSprint(dt);
        }

        /// <summary>
        /// Găsește automat XROrigin, Main Camera, ContinuousMoveProvider (DynamicMoveProvider)
        /// și XRDeviceSimulator (dacă rulează pe PC în simulator).
        /// </summary>
        private void ResolveXRReferences()
        {
            if (xrOrigin == null)
                xrOrigin = FindAnyObjectByType<XROrigin>();

            if (playerCamera == null)
            {
                if (xrOrigin != null && xrOrigin.Camera != null)
                    playerCamera = xrOrigin.Camera;
                else
                    playerCamera = Camera.main;
            }

            if (moveProvider == null)
            {
                if (xrOrigin != null)
                    moveProvider = xrOrigin.GetComponentInChildren<ContinuousMoveProvider>(true);

                if (moveProvider == null)
                    moveProvider = FindAnyObjectByType<ContinuousMoveProvider>();
            }

            if (moveProvider != null && !hasCapturedBaseSpeed)
            {
                baseMoveSpeed = Mathf.Max(0.1f, moveProvider.moveSpeed);
                hasCapturedBaseSpeed = true;
            }

#pragma warning disable CS0618
            if (xrDeviceSimulator == null)
                xrDeviceSimulator = FindAnyObjectByType<XRDeviceSimulator>();

            if (xrDeviceSimulator != null && !hasCapturedBaseSimSpeed)
            {
                baseSimKeyboardXSpeed = Mathf.Max(0.01f, xrDeviceSimulator.keyboardXTranslateSpeed);
                baseSimKeyboardZSpeed = Mathf.Max(0.01f, xrDeviceSimulator.keyboardZTranslateSpeed);
                hasCapturedBaseSimSpeed = true;
            }
#pragma warning restore CS0618
        }

        private void SetupVRSprintAction()
        {
            // Left Thumbstick Click în VR (și tasta 4 în XR Device Simulator când controller-ul stâng e activ)
            vrSprintFallbackAction = new InputAction(name: "SurvivalVRSprint", type: InputActionType.Button);
            vrSprintFallbackAction.AddBinding("<XRController>{LeftHand}/primary2DAxisClick");
        }

        private void InitializeState(bool notifyListeners)
        {
            currentHealth = maxHealth;
            currentHunger = maxHunger;
            currentStamina = maxStamina;
            hungerTimer = 0f;
            starvationTimer = 0f;
            staminaRegenCooldown = 0f;
            movementGraceTimer = 0f;
            staminaExhausted = false;
            isSprinting = false;
            isCritical = currentHealth == 1;
            isDead = false;
            hasInitializedLastXZ = false;

            ReenableLocomotion();
            RestoreAllBaseSpeeds();

            if (notifyListeners)
            {
                OnStateReset?.Invoke();
                BroadcastAllStates();
            }
        }

        private void BroadcastAllStates()
        {
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnHungerChanged?.Invoke(currentHunger, maxHunger);
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            OnSprintStateChanged?.Invoke(isSprinting);
            OnCriticalStateChanged?.Invoke(isCritical);
        }

        /// <summary>
        /// Gestionează scăderea automată a foamei (1 segment / hungerTickSeconds)
        /// și scăderea vieții din cauza înfometării când foamea este 0 (1 segment / starvationDamageSeconds).
        /// </summary>
        private void UpdateHungerAndStarvation(float dt)
        {
            if (currentHunger > 0)
            {
                starvationTimer = 0f;
                hungerTimer += dt;

                if (hungerTimer >= hungerTickSeconds)
                {
                    hungerTimer -= hungerTickSeconds;
                    ConsumeHunger(1);
                }
            }
            else
            {
                hungerTimer = 0f;
                starvationTimer += dt;

                if (starvationTimer >= starvationDamageSeconds)
                {
                    starvationTimer -= starvationDamageSeconds;
                    TakeDamage(1);
                }
            }
        }

        /// <summary>
        /// Gestionează sprint-ul, detecția deplasării reale pe planul XZ (Main Camera + XR Origin),
        /// consumul/regenerarea staminei și boost-ul de viteză pe DynamicMoveProvider (VR) + XRDeviceSimulator (PC).
        /// </summary>
        private void UpdateStaminaAndSprint(float dt)
        {
            bool isPlayerMoving = CheckPlayerMovingXZ(dt, out float horizontalSpeed);
            bool sprintInputHeld = IsSprintInputActive();

            if (detectFastMovementAsSprint && horizontalSpeed > baseMoveSpeed * 0.85f)
                sprintInputHeld = true;

            // După epuizarea completă la 0, așteptăm un prag mic de regenerare pentru a preveni oscilarea rapidă
            if (staminaExhausted && currentStamina >= minStaminaToStartSprint)
                staminaExhausted = false;

            bool canSprint = !staminaExhausted && currentStamina > 0.0001f;
            bool shouldConsumeStamina = sprintInputHeld && canSprint && (!requireMovementToDrainStamina || isPlayerMoving);
            bool nextSprinting = sprintInputHeld && canSprint && isPlayerMoving;

            float previousStamina = currentStamina;

            if (shouldConsumeStamina)
            {
                currentStamina = Mathf.Max(0f, currentStamina - staminaDrainPerSecond * dt);
                staminaRegenCooldown = staminaRegenDelay;

                if (currentStamina <= 0.0001f)
                {
                    currentStamina = 0f;
                    staminaExhausted = true;
                    nextSprinting = false;
                }
            }
            else
            {
                if (staminaRegenCooldown > 0f)
                {
                    staminaRegenCooldown = Mathf.Max(0f, staminaRegenCooldown - dt);
                }
                else if (currentStamina < maxStamina)
                {
                    currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenPerSecond * dt);
                }
            }

            if (nextSprinting != isSprinting)
            {
                isSprinting = nextSprinting;
                OnSprintStateChanged?.Invoke(isSprinting);
            }

            // Aplicăm boost-ul de viteză pe ambele sisteme de mișcare:
            // 1. DynamicMoveProvider / ContinuousMoveProvider (VR + joystick)
            // 2. XRDeviceSimulator (WASD pe PC în modul HMD/FPS)
            ApplyMovementSpeedBoost(isSprinting, dt);

            if (!Mathf.Approximately(previousStamina, currentStamina))
            {
                OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            }
        }

        private void ApplyMovementSpeedBoost(bool sprintActive, float dt)
        {
            // 1. VR Locomotion (DynamicMoveProvider / ContinuousMoveProvider)
            if (moveProvider != null && moveProvider.enabled && hasCapturedBaseSpeed)
            {
                float targetMoveSpeed = sprintActive ? baseMoveSpeed * sprintSpeedMultiplier : baseMoveSpeed;
                moveProvider.moveSpeed = Mathf.MoveTowards(moveProvider.moveSpeed, targetMoveSpeed, baseMoveSpeed * 8f * dt);
            }

            // 2. XR Device Simulator (deplasare WASD pe PC în modul HMD / FPS)
#pragma warning disable CS0618
            if (xrDeviceSimulator != null && xrDeviceSimulator.enabled && hasCapturedBaseSimSpeed)
            {
                float targetX = sprintActive ? baseSimKeyboardXSpeed * sprintSpeedMultiplier : baseSimKeyboardXSpeed;
                float targetZ = sprintActive ? baseSimKeyboardZSpeed * sprintSpeedMultiplier : baseSimKeyboardZSpeed;

                xrDeviceSimulator.keyboardXTranslateSpeed = Mathf.MoveTowards(
                    xrDeviceSimulator.keyboardXTranslateSpeed, targetX, baseSimKeyboardXSpeed * 12f * dt);
                xrDeviceSimulator.keyboardZTranslateSpeed = Mathf.MoveTowards(
                    xrDeviceSimulator.keyboardZTranslateSpeed, targetZ, baseSimKeyboardZSpeed * 12f * dt);
            }
#pragma warning restore CS0618
        }

        private bool IsSprintInputActive()
        {
            if (customSprintAction.action != null && customSprintAction.action.IsPressed())
                return true;

            if (vrSprintFallbackAction != null && vrSprintFallbackAction.IsPressed())
                return true;

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (sprintKey != Key.None && kb[sprintKey].isPressed)
                    return true;

                if (alternateSprintKey != Key.None && kb[alternateSprintKey].isPressed)
                    return true;

                if (thirdSprintKey != Key.None && kb[thirdSprintKey].isPressed)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Detectează deplasarea reală pe planul XZ combinând XR Origin și poziția locală a Main Camera
        /// (exclude micro-tremuratul aplicat pe CameraShakeOffset la 1 HP, dar include atât DynamicMoveProvider,
        /// cât și deplasarea HMD cu WASD din XR Device Simulator).
        /// </summary>
        private bool CheckPlayerMovingXZ(float dt, out float horizontalSpeed)
        {
            horizontalSpeed = 0f;
            bool hasVRMoveInput = false;

            if (moveProvider != null && moveProvider.enabled)
            {
                Vector2 leftInput = moveProvider.leftHandMoveInput.ReadValue();
                Vector2 rightInput = moveProvider.rightHandMoveInput.ReadValue();
                if (leftInput.sqrMagnitude > 0.02f || rightInput.sqrMagnitude > 0.02f)
                    hasVRMoveInput = true;
            }

            Vector2 currentXZ = GetLogicalPlayerXZ();
            if (!hasInitializedLastXZ)
            {
                lastPlayerXZ = currentXZ;
                hasInitializedLastXZ = true;
                return hasVRMoveInput;
            }

            float displacement = Vector2.Distance(currentXZ, lastPlayerXZ);
            lastPlayerXZ = currentXZ;

            if (dt > 0.0001f)
                horizontalSpeed = displacement / dt;

            // Ignorăm teleportările mari într-un singur frame (ex. repoziționare Start / respawn)
            bool isTeleportSpike = displacement > 2.5f;
            bool movedThisFrame = (!isTeleportSpike && horizontalSpeed > movementDetectionThreshold) || hasVRMoveInput;

            if (movedThisFrame)
            {
                // Mică fereastră de continuitate (0.08s) ca să nu clipească între frame-urile de tracking
                movementGraceTimer = 0.08f;
                return true;
            }

            if (movementGraceTimer > 0f)
            {
                movementGraceTimer = Mathf.Max(0f, movementGraceTimer - dt);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returnează poziția logică pe planul XZ a jucătorului (XR Origin + offset HMD al Main Camera,
        /// fără a include offset-ul de CameraShakeOffset).
        /// </summary>
        private Vector2 GetLogicalPlayerXZ()
        {
            if (xrOrigin != null && playerCamera != null)
            {
                Transform camFloorOffset = xrOrigin.CameraFloorOffsetObject != null
                    ? xrOrigin.CameraFloorOffsetObject.transform
                    : xrOrigin.transform;

                // TrackedPoseDriver scrie poziția HMD direct în localPosition-ul Main Camera,
                // iar CameraShakeOffset este părintele camerei — astfel shake-ul nu produce mișcare falsă.
                Vector3 logicalWorldPos = camFloorOffset.TransformPoint(playerCamera.transform.localPosition);
                return new Vector2(logicalWorldPos.x, logicalWorldPos.z);
            }

            if (playerCamera != null)
            {
                Vector3 p = playerCamera.transform.position;
                return new Vector2(p.x, p.z);
            }

            if (xrOrigin != null)
            {
                Vector3 p = xrOrigin.transform.position;
                return new Vector2(p.x, p.z);
            }

            return Vector2.zero;
        }

        private void HandleDebugInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
                return;

            if (debugReduceHungerKey != Key.None && kb[debugReduceHungerKey].wasPressedThisFrame)
                DebugDecreaseHunger();

            if (debugReduceHealthKey != Key.None && kb[debugReduceHealthKey].wasPressedThisFrame)
                DebugDecreaseHealth();

            if (debugSetCriticalKey != Key.None && kb[debugSetCriticalKey].wasPressedThisFrame)
                DebugSetCriticalHealth();

            if (debugKillKey != Key.None && kb[debugKillKey].wasPressedThisFrame)
                DebugKillPlayer();

            if (debugResetAllKey != Key.None && kb[debugResetAllKey].wasPressedThisFrame)
                ResetSurvivalState();
        }

        // ======================== API PUBLIC ========================

        /// <summary>Scade viața cu cantitatea specificată și actualizează starea critică / Game Over.</summary>
        public void TakeDamage(int amount)
        {
            if (amount <= 0 || isDead)
                return;

            SetHealth(currentHealth - amount);
        }

        /// <summary>Vindecă jucătorul cu cantitatea specificată (până la maxHealth).</summary>
        public void Heal(int amount)
        {
            if (amount <= 0 || isDead)
                return;

            SetHealth(currentHealth + amount);
        }

        /// <summary>Setează direct numărul de segmente de viață (0..maxHealth).</summary>
        public void SetHealth(int newHealth)
        {
            int clamped = Mathf.Clamp(newHealth, 0, maxHealth);
            if (clamped == currentHealth)
                return;

            currentHealth = clamped;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            UpdateCriticalAndDeathStates();
        }

        /// <summary>Scade foamea cu cantitatea specificată.</summary>
        public void ConsumeHunger(int amount)
        {
            if (amount <= 0 || isDead)
                return;

            SetHunger(currentHunger - amount);
        }

        /// <summary>Reface foamea (util pentru obiecte de mâncare/provizii viitoare).</summary>
        public void RestoreHunger(int amount)
        {
            if (amount <= 0 || isDead)
                return;

            SetHunger(currentHunger + amount);
        }

        /// <summary>Setează direct nivelul de foame (0..maxHunger).</summary>
        public void SetHunger(int newHunger)
        {
            int clamped = Mathf.Clamp(newHunger, 0, maxHunger);
            if (clamped == currentHunger)
                return;

            currentHunger = clamped;
            if (currentHunger > 0)
                starvationTimer = 0f;

            OnHungerChanged?.Invoke(currentHunger, maxHunger);
        }

        /// <summary>Resetează complet toate valorile (HP, foame, stamină, timere și locomotion).</summary>
        public void ResetSurvivalState()
        {
            InitializeState(notifyListeners: true);
        }

        private void UpdateCriticalAndDeathStates()
        {
            bool nextCritical = currentHealth == 1;
            if (nextCritical != isCritical)
            {
                isCritical = nextCritical;
                OnCriticalStateChanged?.Invoke(isCritical);
            }

            if (currentHealth <= 0 && !isDead)
            {
                TriggerDeath();
            }
        }

        private void TriggerDeath()
        {
            isDead = true;

            if (isSprinting)
            {
                isSprinting = false;
                OnSprintStateChanged?.Invoke(false);
            }

            if (isCritical)
            {
                isCritical = false;
                OnCriticalStateChanged?.Invoke(false);
            }

            DisableLocomotion();

            // Deblocăm cursorul ca jucătorul să poată apăsa butonul "Reia jocul" și cu mouse-ul în XR Device Simulator
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            OnPlayerDied?.Invoke();
        }

        private void DisableLocomotion()
        {
            RestoreAllBaseSpeeds();
            disabledLocomotionProviders.Clear();

            if (xrOrigin != null)
            {
                LocomotionProvider[] providers = xrOrigin.GetComponentsInChildren<LocomotionProvider>(true);
                foreach (LocomotionProvider provider in providers)
                {
                    if (provider != null && provider.enabled)
                    {
                        provider.enabled = false;
                        disabledLocomotionProviders.Add(provider);
                    }
                }
            }
            else if (moveProvider != null && moveProvider.enabled)
            {
                moveProvider.enabled = false;
                disabledLocomotionProviders.Add(moveProvider);
            }

            // Oprim și deplasarea WASD din XR Device Simulator la Game Over (rămân active mouse-ul și raycast-ul VR)
#pragma warning disable CS0618
            if (xrDeviceSimulator != null && hasCapturedBaseSimSpeed)
            {
                xrDeviceSimulator.keyboardXTranslateSpeed = 0f;
                xrDeviceSimulator.keyboardZTranslateSpeed = 0f;
            }
#pragma warning restore CS0618
        }

        private void ReenableLocomotion()
        {
            foreach (LocomotionProvider provider in disabledLocomotionProviders)
            {
                if (provider != null)
                    provider.enabled = true;
            }

            disabledLocomotionProviders.Clear();
        }

        private void RestoreAllBaseSpeeds()
        {
            if (moveProvider != null && hasCapturedBaseSpeed)
                moveProvider.moveSpeed = baseMoveSpeed;

#pragma warning disable CS0618
            if (xrDeviceSimulator != null && hasCapturedBaseSimSpeed)
            {
                xrDeviceSimulator.keyboardXTranslateSpeed = baseSimKeyboardXSpeed;
                xrDeviceSimulator.keyboardZTranslateSpeed = baseSimKeyboardZSpeed;
            }
#pragma warning restore CS0618
        }

        // ======================== CONTEXT MENU (TESTARE DIN INSPECTOR) ========================

        [ContextMenu("Debug: -1 Foame")]
        public void DebugDecreaseHunger()
        {
            ConsumeHunger(1);
            Debug.Log($"[Survival Debug] Foame: {currentHunger}/{maxHunger}", this);
        }

        [ContextMenu("Debug: -1 Viață")]
        public void DebugDecreaseHealth()
        {
            TakeDamage(1);
            Debug.Log($"[Survival Debug] Viață: {currentHealth}/{maxHealth}", this);
        }

        [ContextMenu("Debug: Stare Critică (1 HP)")]
        public void DebugSetCriticalHealth()
        {
            if (isDead)
                ResetSurvivalState();

            SetHealth(1);
            Debug.Log("[Survival Debug] Stare critică activată (1 HP).", this);
        }

        [ContextMenu("Debug: Game Over (0 HP)")]
        public void DebugKillPlayer()
        {
            SetHealth(0);
            Debug.Log("[Survival Debug] Game Over declanșat (0 HP).", this);
        }

        [ContextMenu("Debug: Resetează Tot")]
        public void DebugResetAll()
        {
            ResetSurvivalState();
            Debug.Log("[Survival Debug] Stare survival resetată complet.", this);
        }
    }
}
