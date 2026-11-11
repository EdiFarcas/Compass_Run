using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CompassRun.Checkpoints
{
    /// <summary>Ring identity and grip-release guard after a socket takes it from a controller.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class CheckpointRing : MonoBehaviour, IXRSelectFilter
    {
        [SerializeField] string ringId = "CompassRunRing";
        readonly List<IXRSelectInteractor> releaseRequired = new List<IXRSelectInteractor>();
        XRGrabInteractable grab;
        public string RingId => ringId;
        public bool canProcess => isActiveAndEnabled;
        void OnEnable() { grab=GetComponent<XRGrabInteractable>();grab.selectFilters.Add(this); }
        void OnDisable() { if(grab)grab.selectFilters.Remove(this);releaseRequired.Clear(); }
        internal void RequireGripRelease(IXRSelectInteractor controller)
        {
            if(controller.isSelectActive&&!releaseRequired.Contains(controller))releaseRequired.Add(controller);
        }
        void Update()
        {
            if(releaseRequired.Count>0)releaseRequired.RemoveAll(controller=>controller.transform==null||!controller.isSelectActive);
        }
        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            // Clear as soon as the old grip is released, even while another interactor is evaluating this ring.
            releaseRequired.RemoveAll(controller=>controller.transform==null||!controller.isSelectActive);
            return !releaseRequired.Contains(interactor);
        }
    }
}
