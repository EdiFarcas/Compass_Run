using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Linq;
public static class PrepareLocalReview
{
    static void Run()
    {
        if(Application.isPlaying||SessionState.GetBool("LocalReviewPrepared1110",false))return;
        var scene=EditorSceneManager.GetActiveScene();if(scene.name!="SimulareVR")return;
        const string path="Assets/LocalReview/SimulareVR_Local.unity";
        if(File.Exists(path))return;
        EditorSceneManager.SaveScene(scene,path,true);EditorSceneManager.OpenScene(path);
        foreach(var terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
        {
            var data=Object.Instantiate(terrain.terrainData);data.name="Terrain_Local";
            AssetDatabase.CreateAsset(data,AssetDatabase.GenerateUniqueAssetPath("Assets/LocalReview/Terrain_Local.asset"));terrain.terrainData=data;
            var collider=terrain.GetComponent<TerrainCollider>();if(collider)collider.terrainData=data;
        }
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());SessionState.SetBool("LocalReviewPrepared1110",true);
        var lines=new System.Collections.Generic.List<string>();
        foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(t.parent&&t.root.name!="Start")continue;
            lines.Add(t.name+" parent="+(t.parent?t.parent.name:"ROOT")+" active="+t.gameObject.activeInHierarchy+" pos="+t.position+" scale="+t.lossyScale+" scripts="+string.Join(",",t.GetComponents<MonoBehaviour>().Select(c=>c?c.GetType().Name:"MISSING")));
        }
        var gen=Object.FindFirstObjectByType<TrailGenerator>();
        if(gen){lines.Add("RIG="+(gen.playerRig?gen.playerRig.name:"NULL"));lines.Add("MOVE="+string.Join(",",gen.moveToStart.Select(t=>t?t.name:"NULL")));foreach(var tree in gen.treePrefabs)lines.Add("TREE="+(tree?tree.name:"NULL")+" renderers="+(tree?tree.GetComponentsInChildren<MeshRenderer>().Length:0));}
        File.WriteAllLines("/tmp/unity-local-review.txt",lines);Debug.Log("LOCAL REVIEW: isolated scene and terrain created.");
    }
}
