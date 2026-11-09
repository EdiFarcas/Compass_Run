using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using System.Collections.Generic;

public static class FixCheckpoint
{
    [MenuItem("Tools/Compass Run/Fix Checkpoint")]
    static void Run()
    {
        if(Application.isPlaying || EditorSceneManager.GetActiveScene().name!="SimulareVR" || SessionState.GetBool("CheckpointFixed20261104",false))return;
        var go=GameObject.Find("checkpoint");if(!go)return;
        var renderers=go.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return;
        Bounds bounds=BoundsOf(renderers);float oldHeight=bounds.size.y;
        Undo.RecordObject(go.transform,"Resize checkpoint");
        var bottom=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        go.transform.localScale*=1.5f/bounds.size.y;
        bounds=BoundsOf(renderers);go.transform.position+=bottom-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        if(!AssetDatabase.IsValidFolder("Assets/Materials"))AssetDatabase.CreateFolder("Assets","Materials");
        var copies=new Dictionary<Material,Material>();
        foreach(var renderer in renderers)
        {
            var materials=renderer.sharedMaterials;
            for(int i=0;i<materials.Length;i++)
            {
                var source=materials[i];if(!source)continue;
                if(!copies.TryGetValue(source,out var material))
                {
                    material=new Material(source);material.name="Checkpoint_"+source.name+"_DoubleSided";
                    if(!material.HasProperty("_Cull")){var color=source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):source.color;material.shader=Shader.Find("Universal Render Pipeline/Lit");material.SetColor("_BaseColor",color);}
                    material.SetFloat("_Cull",(float)CullMode.Off);material.doubleSidedGI=true;
                    AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath("Assets/Materials/"+material.name+".mat"));copies[source]=material;
                }
                materials[i]=material;
            }
            Undo.RecordObject(renderer,"Checkpoint double sided materials");renderer.sharedMaterials=materials;renderer.shadowCastingMode=ShadowCastingMode.TwoSided;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        bounds=BoundsOf(renderers);Selection.activeGameObject=go;
        if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(bounds.center,Quaternion.Euler(10,25,0),bounds.size.magnitude*1.2f);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        SessionState.SetBool("CheckpointFixed20261104",true);
        Debug.Log("CHECKPOINT FIXED: height "+oldHeight+" -> "+bounds.size.y+" metres; "+copies.Count+" double sided materials; saved SimulareVR.");
    }
    static Bounds BoundsOf(Renderer[] rs){var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
}
