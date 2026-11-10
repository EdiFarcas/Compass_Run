using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class FinalizeLocalIsolation
{
    static void Run()
    {
        if(Application.isPlaying||EditorSceneManager.GetActiveScene().name!="SimulareVR_Local"||SessionState.GetBool("FinalLocalIsolation1110",false))return;
        AssetDatabase.ImportAsset("Assets/New Terrain.asset",ImportAssetOptions.ForceUpdate);
        const string path="Assets/LocalReview/Terrain_Isolated.asset";
        if(!AssetDatabase.LoadAssetAtPath<TerrainData>(path))AssetDatabase.CopyAsset("Assets/New Terrain.asset",path);
        var data=AssetDatabase.LoadAssetAtPath<TerrainData>(path);var terrain=Object.FindFirstObjectByType<Terrain>();terrain.terrainData=data;terrain.GetComponent<TerrainCollider>().terrainData=data;
        data.treeInstances=new TreeInstance[0];data.treePrototypes=new TreePrototype[0];
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());SessionState.SetBool("FinalLocalIsolation1110",true);Debug.Log("LOCAL ISOLATION FINALIZED: scene and TerrainData are separate assets.");
    }
}
