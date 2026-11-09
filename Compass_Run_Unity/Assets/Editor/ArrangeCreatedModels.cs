using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
public static class ArrangeCreatedModels
{
    [MenuItem("Tools/Compass Run/Arrange Created Models")]
    static void Run()
    {
        const string flag = "CompassRun.ModelsArranged.20261104";
        if (SessionState.GetBool(flag, false) || Application.isPlaying) return;
        SessionState.SetBool(flag, true);
        string[] names = { "Busola", "Bustean", "casuta", "checkpoint", "Fluier", "Harta", "Inel", "masa", "Spray_Ursi", "stanca" };
        var root = GameObject.Find("Modele create") ?? new GameObject("Modele create");
        Undo.RegisterCreatedObjectUndo(root, "Arrange created models");
        float x = 0, z = 0, rowDepth = 0;
        Bounds all = new Bounds(); bool first = true;
        for (int i = 0; i < names.Length; i++)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/" + names[i] + ".fbx");
            if (!asset) continue;
            var go = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Select(t=>t.gameObject).FirstOrDefault(g=>g.name == names[i] && g.scene.IsValid());
            if (!go) { go = (GameObject)PrefabUtility.InstantiatePrefab(asset); Undo.RegisterCreatedObjectUndo(go, "Add model"); }
            Undo.RecordObject(go.transform, "Position model");
            go.transform.SetParent(root.transform, true);
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) continue;
            Bounds b = renderers[0].bounds; foreach(var r in renderers) b.Encapsulate(r.bounds);
            if(i == 5) { x = 0; z += rowDepth + 2; rowDepth = 0; }
            go.transform.position += new Vector3(x - b.min.x, -b.min.y, z - b.min.z);
            b.center += new Vector3(x - b.min.x, -b.min.y, z - b.min.z);
            x += Mathf.Max(b.size.x, .5f) + 1.5f; rowDepth = Mathf.Max(rowDepth,b.size.z);
            if(first) {all=b; first=false;} else all.Encapsulate(b);
        }
        Selection.activeGameObject = root;
        if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.Frame(all, false);
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/ModeleCreate.unity");
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), path);
        Debug.Log("Modele create plasate si scena salvata: " + path);
    }
}
