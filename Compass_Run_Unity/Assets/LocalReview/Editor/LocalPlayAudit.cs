using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Linq;
using System.IO;
public static class LocalPlayAudit
{
    static int frames;
    [MenuItem("Tools/Compass Run/Audit Local Play")]
    static void EnableAudit(){if(Application.isPlaying){frames=0;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}}
    static void State(PlayModeStateChange s)
    {
        if(s==PlayModeStateChange.EnteredPlayMode&&EditorSceneManager.GetActiveScene().name=="SimulareVR_Local"){frames=0;EditorApplication.isPaused=false;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
        if(s==PlayModeStateChange.ExitingPlayMode)EditorApplication.update-=Tick;
    }
    static void Tick()
    {
        if(!Application.isPlaying||Time.timeSinceLevelLoad<4f)return;EditorApplication.update-=Tick;
        var lines=new System.Collections.Generic.List<string>();
        var terrain=Object.FindFirstObjectByType<Terrain>();var gen=Object.FindFirstObjectByType<TrailGenerator>();
        lines.Add("terrainScale="+terrain.transform.lossyScale+" trees="+terrain.terrainData.treeInstanceCount+" keyPoints="+gen.keyPoints.Count+" trails="+gen.trails.Count);
        lines.Add("rig="+gen.playerRig.position+" ground="+(terrain.SampleHeight(gen.playerRig.position)+terrain.transform.position.y));
        var grabs=Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None);var hands=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<IXRSelectInteractor>().Where(h=>h.GetType().Name=="NearFarInteractor").ToArray();
        lines.Add("grabs="+grabs.Length+" hands="+hands.Length);
        foreach(var grab in grabs)lines.Add("pickup="+grab.name+" position="+grab.transform.position+" gravity="+grab.GetComponent<Rigidbody>().useGravity);
        lines.Add("missingScripts="+Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
        File.WriteAllLines("/tmp/unity-local-play-audit.txt",lines);Debug.Log("LOCAL PLAY AUDIT COMPLETE: "+string.Join("; ",lines.Take(4)));
    }
}
