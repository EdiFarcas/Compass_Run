using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

namespace CompassRun.Survival
{
    /// <summary>
    /// Anchors the World Space Survival HUD cleanly in the bottom-left corner of the player's view.
    /// Automatically adapts between flat-screen Play Mode (XR Device Simulator, any aspect ratio)
    /// and an active VR headset (comfortable stereo sweet-spot offset with subtle inward angle).
    /// </summary>
    [DisallowMultipleComponent]
    public class SurvivalHUDAnchor : MonoBehaviour
    {
        [Header("Referință Cameră (auto-detectată dacă este goală)")]
        [SerializeField] private Camera targetCamera;

        [Header("Poziționare Desktop / XR Device Simulator (Viewport Stânga-Jos)")]
        [Tooltip("Distanța în metri față de Main Camera (peste nearClipPlane, sub obiectele îndepărtate).")]
        [Range(0.35f, 1.5f)]
        [SerializeField] private float hudDistance = 0.62f;

        [Tooltip("Marginea orizontală din stânga (fracție din lățimea ecranului).")]
        [Range(0.01f, 0.25f)]
        [SerializeField] private float leftMarginViewport = 0.025f;

        [Tooltip("Marginea verticală de jos (fracție din înălțimea ecranului).")]
        [Range(0.01f, 0.30f)]
        [SerializeField] private float bottomMarginViewport = 0.065f;

        [Tooltip("Înălțimea relativă a cardului HUD ca fracție din înălțimea viewport-ului.")]
        [Range(0.08f, 0.30f)]
        [SerializeField] private float cardHeightViewportFraction = 0.165f;

        [Header("Poziționare Cască VR Activă (XRSettings.isDeviceActive)")]
        [Tooltip("Offset local față de Main Camera când rulează într-o cască VR reală.")]
        [SerializeField] private Vector3 vrLocalOffset = new Vector3(-0.22f, -0.16f, 0.72f);

        [Tooltip("Rotație subtilă spre utilizator în VR pentru lizibilitate confortabilă.")]
        [SerializeField] private Vector3 vrLocalEulerAngles = new Vector3(5f, 12f, 0f);

        [Tooltip("Scara Canvas-ului în cască VR reală.")]
        [SerializeField] private float vrCanvasScale = 0.00072f;

        [Tooltip("Dacă este > 0, aplică o urmărire fină (lazy follow) în VR; 0 = blocat precis de cameră.")]
        [Range(0f, 20f)]
        [SerializeField] private float vrFollowSmoothing = 10f;

        private RectTransform canvasRect;

        private void Awake()
        {
            canvasRect = GetComponent<RectTransform>();
            ResolveCamera();
        }

        private void OnEnable()
        {
            ResolveCamera();
            UpdatePlacement(immediate: true);
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
                ResolveCamera();

            if (targetCamera == null)
                return;

            UpdatePlacement(immediate: !XRSettings.isDeviceActive || vrFollowSmoothing <= 0f);
        }

        private void ResolveCamera()
        {
            if (targetCamera != null)
                return;

            XROrigin origin = FindAnyObjectByType<XROrigin>();
            if (origin != null && origin.Camera != null)
            {
                targetCamera = origin.Camera;
                return;
            }

            targetCamera = Camera.main;
        }

        private void UpdatePlacement(bool immediate)
        {
            if (targetCamera == null)
                return;

            Transform camT = targetCamera.transform;
            Vector3 desiredWorldPos;
            Quaternion desiredWorldRot;
            float desiredScale;

            if (XRSettings.isDeviceActive)
            {
                desiredWorldPos = camT.TransformPoint(vrLocalOffset);
                desiredWorldRot = camT.rotation * Quaternion.Euler(vrLocalEulerAngles);
                desiredScale = vrCanvasScale;
            }
            else
            {
                // Calculăm dimensiunea exactă a frustum-ului la hudDistance pentru orice aspect ratio
                float frustumHeight = 2f * hudDistance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float frustumWidth = frustumHeight * targetCamera.aspect;

                float rectWidth = canvasRect != null && canvasRect.rect.width > 1f ? canvasRect.rect.width : 310f;
                float rectHeight = canvasRect != null && canvasRect.rect.height > 1f ? canvasRect.rect.height : 126f;

                float targetWorldHeight = frustumHeight * cardHeightViewportFraction;
                desiredScale = targetWorldHeight / rectHeight;
                float targetWorldWidth = rectWidth * desiredScale;

                // Poziționare curată în colțul stânga-jos (pivotul Canvas-ului este centrat la 0.5, 0.5)
                float localX = -frustumWidth * 0.5f + frustumWidth * leftMarginViewport + targetWorldWidth * 0.5f;
                float localY = -frustumHeight * 0.5f + frustumHeight * bottomMarginViewport + targetWorldHeight * 0.5f;

                desiredWorldPos = camT.TransformPoint(new Vector3(localX, localY, hudDistance));
                // Ușor orientat către cameră, aproape plat pentru claritate maximă pe monitor
                desiredWorldRot = camT.rotation * Quaternion.Euler(2f, 4f, 0f);
            }

            if (immediate)
            {
                transform.SetPositionAndRotation(desiredWorldPos, desiredWorldRot);
            }
            else
            {
                float t = 1f - Mathf.Exp(-vrFollowSmoothing * Time.unscaledDeltaTime);
                transform.position = Vector3.Lerp(transform.position, desiredWorldPos, t);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredWorldRot, t);
            }

            transform.localScale = Vector3.one * desiredScale;
        }
    }
}
