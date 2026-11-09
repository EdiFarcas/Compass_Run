using UnityEngine;
using UnityEditor;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Linq;
public static class ValidateVRSimulation
{
    [MenuItem("Tools/Compass Run/Validate VR Interaction (Play Mode)")]
    static void Run()
    {
        if(!Application.isPlaying) { Debug.Log("Start Play Mode before validating XR interaction."); return; }
        var manager=Object.FindFirstObjectByType<XRInteractionManager>();var grabs=Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None);
        var hands=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<IXRSelectInteractor>().Where(h=>h.GetType().Name=="NearFarInteractor").ToArray();
        if(!manager||hands.Length!=2||grabs.Length!=5){Debug.LogError("XR VALIDATION FAILED: missing rig or grab objects; hands="+hands.Length+", objects="+grabs.Length);return;}
        foreach(var hand in hands)foreach(var grab in grabs)
        {
            manager.SelectEnter((IXRSelectInteractor)hand,(IXRSelectInteractable)grab);
            if(!grab.isSelected){Debug.LogError("XR VALIDATION FAILED: "+((Component)hand).name+" -> "+grab.name);return;}
            manager.SelectExit((IXRSelectInteractor)hand,(IXRSelectInteractable)grab);
            if(grab.isSelected){Debug.LogError("XR VALIDATION FAILED: release "+grab.name);return;}
        }
        Debug.Log("XR VALIDATION PASSED: both controller interactors select and release all five objects.");
    }
}
