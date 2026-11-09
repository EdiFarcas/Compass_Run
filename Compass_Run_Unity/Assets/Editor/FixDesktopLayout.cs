using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
public static class FixDesktopLayout
{
    static Bounds BoundsOf(Transform t){var rs=t.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
    static void Run()
    {
        if(Application.isPlaying || !GameObject.Find("Desktop Player") || GameObject.Find("Desktop setup v2"))return;
        var table=GameObject.Find("masa").transform; var b=BoundsOf(table);table.localScale*=.9f/b.size.y; b=BoundsOf(table);table.position+=Vector3.down*b.min.y; b=BoundsOf(table);
        string[] names={"Busola","Harta","Fluier","Inel","Spray_Ursi"};
        for(int i=0;i<names.Length;i++)
        {
            var go=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(t=>t.name==names[i]).gameObject;go.SetActive(true);
            foreach(var filter in go.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh&&!filter.GetComponent<Collider>()){var c=filter.gameObject.AddComponent<BoxCollider>();c.center=filter.sharedMesh.bounds.center;c.size=filter.sharedMesh.bounds.size;}
            var body=go.GetComponent<Rigidbody>();if(!body)body=go.AddComponent<Rigidbody>();body.mass=.25f;body.useGravity=true;body.isKinematic=false;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            var pickup=go.GetComponent<DesktopPickup>();if(!pickup)pickup=go.AddComponent<DesktopPickup>();pickup.displayName=names[i];
            var bounds=BoundsOf(go.transform);float max=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));if(max>.32f)go.transform.localScale*=.32f/max;
            bounds=BoundsOf(go.transform);go.transform.position+=new Vector3(b.center.x+(i-2)*.3f,b.max.y+.025f,b.center.z)-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        }
        var player=GameObject.Find("Desktop Player");player.transform.position=new Vector3(b.center.x,.03f,b.min.z-1f);
        new GameObject("Desktop setup v2");EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());Debug.Log("Desktop setup v2: five pickup objects, table height 0.9m.");
    }
}
