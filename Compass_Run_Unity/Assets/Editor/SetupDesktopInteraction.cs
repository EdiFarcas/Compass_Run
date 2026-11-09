using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;

public static class SetupDesktopInteraction
{
    [MenuItem("Tools/Compass Run/Setup Desktop Interaction")]
    static void Setup()
    {
        if(Application.isPlaying || GameObject.Find("Desktop Player")) return;
        if(EditorSceneManager.GetActiveScene().name != "ModeleCreate") return;
        var roots = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t=>t.GetComponent<MeshFilter>() == null && new[]{"Busola","Bustean","casuta","checkpoint","Fluier","Harta","Inel","masa","Spray_Ursi","stanca"}.Contains(t.name)).ToArray();
        string[] small = {"Busola","Fluier","Harta","Inel","Spray_Ursi"};
        foreach(var t in roots)
        {
            foreach(var c in t.GetComponentsInChildren<Camera>(true)) c.gameObject.SetActive(false);
            foreach(var l in t.GetComponentsInChildren<Light>(true)) l.enabled=false;
            foreach(var a in t.GetComponentsInChildren<AudioListener>(true)) a.enabled=false;
            foreach(var filter in t.GetComponentsInChildren<MeshFilter>())
            {
                if(!filter.sharedMesh || filter.GetComponent<Collider>()) continue;
                if(small.Contains(t.name)) { var c=filter.gameObject.AddComponent<BoxCollider>(); c.center=filter.sharedMesh.bounds.center; c.size=filter.sharedMesh.bounds.size; }
                else { var c=filter.gameObject.AddComponent<MeshCollider>(); c.sharedMesh=filter.sharedMesh; }
            }
        }
        Bounds table = new Bounds(new Vector3(5, .8f, 5), new Vector3(2,1.6f,1));
        var tableRoot=roots.FirstOrDefault(t=>t.name=="masa");
        if(tableRoot) table=BoundsOf(tableRoot);
        int index=0;
        foreach(var t in roots.Where(t=>small.Contains(t.name)))
        {
            var b=BoundsOf(t);
            var destination=new Vector3(table.center.x + (index-2)*.25f, table.max.y+.06f, table.center.z);
            t.position += destination - new Vector3(b.center.x,b.min.y,b.center.z);
            var body=t.GetComponent<Rigidbody>(); if(!body) body=t.gameObject.AddComponent<Rigidbody>(); body.mass=.25f; body.useGravity=true; body.isKinematic=false; body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            var pickup=t.GetComponent<DesktopPickup>(); if(!pickup) pickup=t.gameObject.AddComponent<DesktopPickup>(); pickup.displayName=t.name;
            index++;
        }
        var ground=GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name="Sol"; ground.transform.position=new Vector3(7,-.15f,6); ground.transform.localScale=new Vector3(40,.3f,40);
        var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.color=new Color(.24f,.32f,.22f); AssetDatabase.CreateAsset(mat,AssetDatabase.GenerateUniqueAssetPath("Assets/DesktopGround.mat")); ground.GetComponent<Renderer>().sharedMaterial=mat;
        foreach(var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) cam.gameObject.SetActive(false);
        var player=new GameObject("Desktop Player"); player.transform.position=new Vector3(table.center.x, .05f, table.min.z-1.5f);
        var cc=player.AddComponent<CharacterController>(); cc.height=1.8f; cc.radius=.25f; cc.center=new Vector3(0,.9f,0); cc.stepOffset=.25f;
        var eye=new GameObject("Player Camera"); eye.tag="MainCamera"; eye.transform.SetParent(player.transform,false); eye.transform.localPosition=new Vector3(0,1.65f,0); var camera=eye.AddComponent<Camera>(); camera.nearClipPlane=.03f; camera.farClipPlane=200; eye.AddComponent<AudioListener>();
        var controls=player.AddComponent<DesktopPlayer>(); controls.view=camera;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject=player;
        Debug.Log("Desktop interaction configured; pickup objects: "+index);
    }
    static Bounds BoundsOf(Transform t) { var rs=t.GetComponentsInChildren<Renderer>(); var b=rs.Length>0?rs[0].bounds:new Bounds(t.position,Vector3.one); foreach(var r in rs)b.Encapsulate(r.bounds); return b; }
}
