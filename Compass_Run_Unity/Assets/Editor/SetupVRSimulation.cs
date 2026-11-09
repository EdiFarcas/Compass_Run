using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class SetupVRSimulation
{
    [MenuItem("Tools/Compass Run/Setup VR Simulation")]
    static void Run()
    {
        if(Application.isPlaying || GameObject.Find("XR Interaction Simulator") || EditorSceneManager.GetActiveScene().name!="ModeleCreate")return;
        const string sample="Assets/Samples/XR Interaction Toolkit/3.3.2/";
        var rigAsset=AssetDatabase.LoadAssetAtPath<GameObject>(sample+"Starter Assets/Prefabs/XR Origin (XR Rig).prefab");
        var simAsset=AssetDatabase.LoadAssetAtPath<GameObject>(sample+"XR Interaction Simulator/XR Interaction Simulator.prefab");
        if(!rigAsset||!simAsset)return;
        var desktop=GameObject.Find("Desktop Player"); Vector3 start=desktop?desktop.transform.position:Vector3.zero;
        if(desktop)desktop.SetActive(false);
        foreach(var p in Object.FindObjectsByType<DesktopPickup>(FindObjectsSortMode.None))
        {
            if(!p.GetComponent<XRGrabInteractable>()) {var grab=p.gameObject.AddComponent<XRGrabInteractable>();grab.useDynamicAttach=true;grab.throwOnDetach=true;}
        }
        if(!Object.FindFirstObjectByType<XRInteractionManager>())new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
        var rig=(GameObject)PrefabUtility.InstantiatePrefab(rigAsset);rig.transform.position=start;
        var origin=rig.GetComponent<XROrigin>();origin.RequestedTrackingOriginMode=XROrigin.TrackingOriginMode.Device;origin.CameraYOffset=1.65f;
        var simulator=(GameObject)PrefabUtility.InstantiatePrefab(simAsset);simulator.name="XR Interaction Simulator";
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),"Assets/Scenes/SimulareVR.unity");
        Debug.Log("VR SIMULATION READY: XR Origin, two controllers, interaction simulator, five grab interactables.");
    }
}
