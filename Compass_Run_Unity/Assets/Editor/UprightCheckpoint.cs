using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class UprightCheckpoint
{
    static void Run()
    {
        if(Application.isPlaying || EditorSceneManager.GetActiveScene().name!="SimulareVR" || SessionState.GetBool("CheckpointUpright20261104",false))return;
        var go=GameObject.Find("checkpoint");if(!go)return;
        var rs=go.GetComponentsInChildren<Renderer>();if(rs.Length==0)return;
        var before=BoundsOf(rs);var anchor=new Vector3(before.center.x,before.min.y,before.center.z);
        Undo.RecordObject(go.transform,"Turn checkpoint upright");
        go.transform.rotation=Quaternion.AngleAxis(180,Vector3.forward)*go.transform.rotation;
        var after=BoundsOf(rs);go.transform.position+=anchor-new Vector3(after.center.x,after.min.y,after.center.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());SessionState.SetBool("CheckpointUpright20261104",true);
        Selection.activeGameObject=go;after=BoundsOf(rs);
        if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(after.center,Quaternion.Euler(10,25,0),after.size.magnitude*1.2f);
        Debug.Log("CHECKPOINT UPRIGHT: rotated 180 degrees; ground contact and 1.5m height preserved.");
    }
    static Bounds BoundsOf(Renderer[] rs){var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
}
