using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Linq;
using System.IO;
public static class TestActivationEffects
{
    static bool pending;
    [MenuItem("Tools/Compass Run/Test Activation Effects (Play Mode)")]
    static void RunManual(){if(!Application.isPlaying){Debug.Log("Start Play Mode first.");return;}pending=true;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void OnPlay(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode&&EditorSceneManager.GetActiveScene().name=="SimulareVR_Local"&&!SessionState.GetBool("ActivationEffectsTested",false)){pending=true;EditorApplication.update+=Tick;}
        if(state==PlayModeStateChange.ExitingPlayMode){EditorApplication.update-=Tick;pending=false;}
    }
    static void Tick()
    {
        if(!pending||!Application.isPlaying||Time.timeSinceLevelLoad<2)return;pending=false;EditorApplication.update-=Tick;
        try
        {
            var grabs=Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None);
            var whistle=grabs.First(g=>g.name=="Fluier");var spray=grabs.First(g=>g.name=="Spray_Ursi");
            var audio=whistle.GetComponentInChildren<AudioSource>();var ps=spray.GetComponentInChildren<ParticleSystem>();
            if(audio.isPlaying||ps.isEmitting)throw new System.Exception("Effect started without activation.");
            ((IXRActivateInteractable)whistle).OnActivated(new ActivateEventArgs{interactableObject=whistle});
            if(!audio.isPlaying)throw new System.Exception("Whistle Activate did not play audio.");audio.Stop();
            ((IXRActivateInteractable)spray).OnActivated(new ActivateEventArgs{interactableObject=spray});
            if(!ps.isEmitting)throw new System.Exception("Spray Activate did not emit.");ps.Simulate(.2f,true,false);ps.Play();
            if(ps.particleCount==0)throw new System.Exception("No spray particles.");int count=ps.particleCount;
            ((IXRActivateInteractable)spray).OnDeactivated(new DeactivateEventArgs{interactableObject=spray});
            if(ps.isEmitting)throw new System.Exception("Spray did not stop on Deactivate.");
            ps.Play();spray.selectExited.Invoke(new SelectExitEventArgs{interactableObject=spray});
            if(ps.isEmitting)throw new System.Exception("Spray did not stop on Select Exit.");
            ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            string report="PASS: effects idle at startup; whistle Activate plays AudioSource; spray Activate emits "+count+" particles; Deactivate and Select Exited stop emission.\nWhistle clip: "+audio.clip.length+" seconds; nozzle local position="+ps.transform.localPosition+", local rotation="+ps.transform.localEulerAngles;
            File.WriteAllText("/tmp/activation-effects-test.txt",report);Debug.Log("ACTIVATION TEST PASSED: "+report);SessionState.SetBool("ActivationEffectsTested",true);
        }
        catch(System.Exception e){File.WriteAllText("/tmp/activation-effects-test.txt","FAIL: "+e);Debug.LogError("ACTIVATION TEST FAILED: "+e);}
    }
}
