using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

namespace CompassRun.Checkpoints
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RingSocketInteractor))]
    public sealed class CheckpointObjective : MonoBehaviour
    {
        [Tooltip("Optional. When empty, uses the checkpoint parent name (for example CP1 from TrailGenerator).")]
        [SerializeField] string checkpointId;
        [SerializeField] Renderer indicator;
        [SerializeField] Color waitingColor = new Color(.05f, .6f, 1f);
        [SerializeField] Color completedColor = new Color(.1f, 1f, .2f);
        [SerializeField] UnityEvent onValidated = new UnityEvent();
        RingSocketInteractor socket;
        MaterialPropertyBlock colors;
        public bool IsCompleted { get; private set; }
        public string CheckpointId => string.IsNullOrWhiteSpace(checkpointId) ? (transform.parent ? transform.parent.name : name) : checkpointId;
        public UnityEvent OnValidated => onValidated;

        void Awake() { socket = GetComponent<RingSocketInteractor>(); }
        void OnEnable()
        {
            if (!socket) socket = GetComponent<RingSocketInteractor>();
            socket.selectEntered.AddListener(Validate);
            RefreshIndicator();
        }
        void OnDisable() { if (socket) socket.selectEntered.RemoveListener(Validate); }
        void Validate(SelectEnterEventArgs args)
        {
            if (IsCompleted || !args.interactableObject.transform.GetComponent<CheckpointRing>()) return;
            IsCompleted = true;
            RefreshIndicator();
            Debug.Log("Checkpoint validat: " + CheckpointId, this);
            onValidated.Invoke();
        }
        /// <summary>Starts a new attempt. Removing the ring alone does not undo a visited checkpoint.</summary>
        public void ResetObjective() { IsCompleted = false; RefreshIndicator(); }
        void RefreshIndicator()
        {
            if (!indicator) return;
            if (colors == null) colors = new MaterialPropertyBlock();
            indicator.GetPropertyBlock(colors);
            var color = IsCompleted ? completedColor : waitingColor;
            colors.SetColor("_BaseColor", color);
            colors.SetColor("_Color", color);
            indicator.SetPropertyBlock(colors);
        }
    }
}
