using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
public static class TuneLocalPreview
{
    static void Run()
    {
        if(Application.isPlaying||EditorSceneManager.GetActiveScene().name!="SimulareVR_Local"||SessionState.GetBool("LocalTuned1110",false))return;
        var gen=Object.FindFirstObjectByType<TrailGenerator>();gen.treeCount=35;gen.treePrefabs=gen.treePrefabs.Where(t=>t&&t.name.Contains("sapling")).ToArray();
        var terrain=gen.terrain;terrain.treeDistance=90;terrain.detailObjectDistance=35;
        terrain.terrainData.treeInstances=new TreeInstance[0];terrain.terrainData.treePrototypes=new TreePrototype[0];
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());SessionState.SetBool("LocalTuned1110",true);
        Debug.Log("LOCAL PERFORMANCE: 35 saplings, tree visibility 90m.");
    }
}
