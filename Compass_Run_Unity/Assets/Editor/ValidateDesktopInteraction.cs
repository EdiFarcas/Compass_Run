using UnityEngine;
using UnityEditor;
using System.Reflection;
public static class ValidateDesktopInteraction
{
    [MenuItem("Tools/Compass Run/Validate Desktop Interaction")]
    static void RunValidation(){SessionState.SetBool("DesktopValidated",false);EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;Begin();}
    static void Begin(){if(!SessionState.GetBool("DesktopValidated",false)&&GameObject.Find("Desktop setup v2")&&!EditorApplication.isPlaying){foreach(var duplicate in Object.FindObjectsByType<DesktopPickup>(FindObjectsInactive.Include,FindObjectsSortMode.None)) { if(duplicate.transform.parent && duplicate.transform.parent.GetComponentInParent<DesktopPickup>()) { var rb=duplicate.GetComponent<Rigidbody>(); Object.DestroyImmediate(duplicate); if(rb) Object.DestroyImmediate(rb); } } foreach(var item in Object.FindObjectsByType<DesktopPickup>(FindObjectsInactive.Include,FindObjectsSortMode.None)) { for(var t=item.transform;t!=null;t=t.parent)t.gameObject.SetActive(true); } UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()); SessionState.SetBool("DesktopValidationRunning",true);EditorApplication.isPlaying=true;}}
    static void State(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("DesktopValidationRunning",false))EditorApplication.delayCall+=Validate;}
    static void Validate()
    {
        try
        {
            var p=Object.FindFirstObjectByType<DesktopPlayer>();var pickups=Object.FindObjectsByType<DesktopPickup>(FindObjectsSortMode.None);
            if(!p||pickups.Length!=5)throw new System.Exception("Expected player and five pickups; found " + pickups.Length);
            foreach(var item in pickups)
            {
                var body=item.GetComponent<Rigidbody>();if(!body||item.GetComponentsInChildren<Collider>().Length==0)throw new System.Exception("Missing physics: "+item.name);
                typeof(DesktopPlayer).GetMethod("Grab",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{item});
                if(!body.isKinematic||body.useGravity)throw new System.Exception("Grab failed: "+item.name);
                typeof(DesktopPlayer).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);
                typeof(DesktopPlayer).GetMethod("Release",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);
                if(body.isKinematic||!body.useGravity)throw new System.Exception("Release failed: "+item.name);
            }
            Debug.Log("DESKTOP VALIDATION PASSED: five objects grab/release correctly; physics restored.");SessionState.SetBool("DesktopValidated",true);
        }
        catch(System.Exception e){Debug.LogError("DESKTOP VALIDATION FAILED: "+e);}
        finally{SessionState.SetBool("DesktopValidationRunning",false);EditorApplication.isPlaying=false;}
    }
}
