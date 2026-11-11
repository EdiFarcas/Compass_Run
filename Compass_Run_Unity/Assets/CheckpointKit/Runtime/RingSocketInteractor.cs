using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CompassRun.Checkpoints
{
    /// <summary>XR socket restricted to marked rings, with optional transfer from a controller grip.</summary>
    [AddComponentMenu("XR/Compass Run/Ring Socket Interactor")]
    public sealed class RingSocketInteractor : XRSocketInteractor
    {
        [SerializeField] string acceptedRingId = "CompassRunRing";
        [SerializeField] bool takeFromController = true;
        readonly List<IXRSelectInteractor> previousSelectors = new List<IXRSelectInteractor>();
        readonly List<IXRSelectInteractable> leavingRings = new List<IXRSelectInteractable>();

        bool Accepts(IXRInteractable candidate)
        {
            var marker = candidate.transform.GetComponent<CheckpointRing>();
            return marker && marker.RingId == acceptedRingId;
        }

        public override bool CanHover(IXRHoverInteractable interactable) => Accepts(interactable) && base.CanHover(interactable);
        public override bool CanSelect(IXRSelectInteractable interactable) => Accepts(interactable) && !leavingRings.Contains(interactable) && base.CanSelect(interactable);

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            if(!leavingRings.Contains(args.interactableObject))leavingRings.Add(args.interactableObject);
        }
        bool OutsideCaptureZone(IXRSelectInteractable ring)
        {
            if(ring.transform==null||!ring.transform.gameObject.activeInHierarchy)return true;
            var sphere=GetComponent<SphereCollider>();
            if(!sphere)return Vector3.Distance(ring.transform.position,transform.position)>.3f;
            var scale=sphere.transform.lossyScale;
            float radius=sphere.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Max(Mathf.Abs(scale.y),Mathf.Abs(scale.z)))+.08f;
            return Vector3.Distance(ring.transform.position,sphere.transform.TransformPoint(sphere.center))>radius;
        }
        public override void ProcessInteractor(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractor(updatePhase);
            if(updatePhase==XRInteractionUpdateOrder.UpdatePhase.Dynamic)leavingRings.RemoveAll(OutsideCaptureZone);
            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic || !takeFromController || !socketActive || hasSelection || !interactionManager)
                return;

            foreach (var hovered in interactablesHovered)
            {
                if (!(hovered is IXRSelectInteractable ring) || !Accepts(ring) || leavingRings.Contains(ring) || !ring.isSelected)
                    continue;
                // Never take a ring already installed in another checkpoint or a different socket.
                bool inSocket = false;
                previousSelectors.Clear();
                foreach (var selector in ring.interactorsSelecting)
                {
                    if (selector is XRSocketInteractor) { inSocket = true; break; }
                    previousSelectors.Add(selector);
                }
                if (inSocket || previousSelectors.Count == 0)
                    continue;

                // The standard socket intentionally rejects already-selected objects. Release the grip first,
                // then use the normal XR selection API so physics, events and attachment remain consistent.
                var marker = ring.transform.GetComponent<CheckpointRing>();
                foreach (var selector in previousSelectors)
                {
                    marker.RequireGripRelease(selector);
                    interactionManager.SelectExit(selector, ring);
                }
                if (CanSelect(ring))
                    interactionManager.SelectEnter(this, ring);
                break; // At most one transfer per update; hover lists may change during XR callbacks.
            }
        }
    }
}
