using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.XR.CoreUtils;

public static class UseLab2Controllers
{
    [MenuItem("Tools/Compass Run/Use Lab 2 Controllers")]
    static void Apply()
    {
        if(Application.isPlaying||EditorSceneManager.GetActiveScene().name!="SimulareVR"||GameObject.Find("XR Device Simulator - Laborator 2"))return;
        const string root="Assets/Samples/XR Interaction Toolkit/3.6.1/";
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(root+"Starter Assets/Prefabs/XR Origin (XR Rig).prefab");
        var sim=AssetDatabase.LoadAssetAtPath<GameObject>(root+"XR Device Simulator/XR Device Simulator.prefab");
        if(!prefab||!sim)return;
        var oldRig=GameObject.Find("XR Origin (XR Rig)");var position=oldRig?oldRig.transform.position:new Vector3(5.5479f,.03f,10.538f);
        if(oldRig)Object.DestroyImmediate(oldRig);
        var oldSim=GameObject.Find("XR Interaction Simulator");if(oldSim)Object.DestroyImmediate(oldSim);
        var rig=(GameObject)PrefabUtility.InstantiatePrefab(prefab);rig.transform.position=position;
        var origin=rig.GetComponent<XROrigin>();origin.RequestedTrackingOriginMode=XROrigin.TrackingOriginMode.Device;origin.CameraYOffset=1.65f;
        var simulator=(GameObject)PrefabUtility.InstantiatePrefab(sim);simulator.name="XR Device Simulator - Laborator 2";
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("LAB2 XR READY: Starter Assets and XR Device Simulator copied from Tema2_VR 3.6.1.");
    }
}
