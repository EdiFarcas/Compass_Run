using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Linq;
public static class SetupHandheldMap
{
 [MenuItem("Tools/Compass Run/Setup Handheld Trail Map")]
 public static void Setup(){
  if(Application.isPlaying||EditorSceneManager.GetActiveScene().name!="SimulareVR")return;
  var grab=Object.FindObjectsByType<XRGrabInteractable>().First(g=>g.name=="Harta");
  var map=grab.GetComponent<HandheldTrailMap>();if(!map)map=Undo.AddComponent<HandheldTrailMap>(grab.gameObject);
  var so=new SerializedObject(map);so.FindProperty("generator").objectReferenceValue=Object.FindAnyObjectByType<TrailGenerator>();so.ApplyModifiedProperties();
  EditorUtility.SetDirty(map);PrefabUtility.RecordPrefabInstancePropertyModifications(map);
  EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
 }
}
