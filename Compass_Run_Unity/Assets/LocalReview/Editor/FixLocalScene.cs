using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
public static class FixLocalScene
{
    static void Run()
    {
        if(Application.isPlaying||EditorSceneManager.GetActiveScene().name!="SimulareVR_Local"||GameObject.Find("Local review fixes"))return;
        var terrain=Object.FindFirstObjectByType<Terrain>();var gen=Object.FindFirstObjectByType<TrailGenerator>();if(!terrain||!gen)return;
        terrain.transform.localScale=Vector3.one;
        var collider=terrain.GetComponent<TerrainCollider>();if(!collider)collider=terrain.gameObject.AddComponent<TerrainCollider>();collider.enabled=true;collider.terrainData=terrain.terrainData;
        gen.seed=11102026;
        var trees=new System.Collections.Generic.List<GameObject>();
        foreach(var source in gen.treePrefabs.Where(p=>p&&p.name.StartsWith("fir_")))
        {
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(source);
            foreach(var camera in instance.GetComponentsInChildren<Camera>(true))Object.DestroyImmediate(camera.gameObject);
            foreach(var light in instance.GetComponentsInChildren<Light>(true))Object.DestroyImmediate(light);
            var renderers=instance.GetComponentsInChildren<Renderer>();var group=instance.GetComponent<LODGroup>();if(!group)group=instance.AddComponent<LODGroup>();
            group.SetLODs(new[]{new LOD(.015f,renderers)});group.RecalculateBounds();
            trees.Add(PrefabUtility.SaveAsPrefabAsset(instance,"Assets/LocalReview/"+source.name+"_Terrain.prefab"));Object.DestroyImmediate(instance);
        }
        gen.treePrefabs=trees.ToArray();gen.treeCount=200;
        var start=GameObject.Find("Start");if(start)foreach(var light in start.GetComponentsInChildren<Light>(true))light.enabled=false;
        foreach(var p in Object.FindObjectsByType<DesktopPickup>(FindObjectsSortMode.None))
        {
            foreach(var camera in p.GetComponentsInChildren<Camera>(true))camera.gameObject.SetActive(false);
            Debug.Log("LOCAL PICKUP "+p.name+" bodies="+p.GetComponentsInChildren<Rigidbody>().Length+" colliders="+p.GetComponentsInChildren<Collider>().Length);
        }
        new GameObject("Local review fixes");EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());Debug.Log("LOCAL FIX: terrain scale restored to 1, valid tree LOD prefabs, deterministic seed.");
    }
}
