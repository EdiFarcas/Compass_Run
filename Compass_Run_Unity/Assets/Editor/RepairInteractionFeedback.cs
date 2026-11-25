using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using CompassRun.Checkpoints;
using System.Linq;
public static class RepairInteractionFeedback
{
 [MenuItem("Tools/Compass Run/Repair Interaction Feedback")]
 public static void Repair(){
 if(Application.isPlaying)return;
 var grabs=Object.FindObjectsByType<XRGrabInteractable>();
 var whistle=grabs.First(g=>g.name=="Fluier");var spray=grabs.First(g=>g.name=="Spray_Ursi");
 var a=whistle.GetComponentInChildren<AudioSource>(true);
 if(!a){var go=new GameObject("Fluier_Sunet");go.transform.SetParent(whistle.transform,false);a=go.AddComponent<AudioSource>();}
 a.gameObject.SetActive(true);a.enabled=true;a.clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/LocalReview/Effects/Fluier.wav");a.playOnAwake=false;a.loop=false;a.mute=false;a.volume=.85f;a.spatialBlend=.65f;a.minDistance=2;a.maxDistance=25;a.dopplerLevel=0;
 Wire(whistle.activated,a,a.Play);EditorUtility.SetDirty(a);EditorUtility.SetDirty(whistle);
 var ps=spray.GetComponentInChildren<ParticleSystem>(true);if(!ps)throw new System.Exception("Missing spray particle system");
 ps.gameObject.SetActive(true);ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
 var main=ps.main;main.playOnAwake=false;main.loop=true;main.scalingMode=ParticleSystemScalingMode.Shape;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startSize=new ParticleSystem.MinMaxCurve(.06f,.11f);main.startSpeed=new ParticleSystem.MinMaxCurve(1.5f,2.5f);main.startLifetime=new ParticleSystem.MinMaxCurve(.5f,.9f);
 var emission=ps.emission;emission.enabled=true;emission.rateOverTime=100;
 var pr=ps.GetComponent<ParticleSystemRenderer>();pr.enabled=true;pr.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/LocalReview/Effects/Spray_Fum_Orange.mat");
 Wire(spray.activated,ps,ps.Play);Wire(spray.deactivated,ps,ps.Stop);Wire(spray.selectExited,ps,ps.Stop);
 EditorUtility.SetDirty(ps);EditorUtility.SetDirty(pr);EditorUtility.SetDirty(spray);
 foreach(var g in new[]{whistle,spray})PrefabUtility.RecordPrefabInstancePropertyModifications(g);
 var prefabPath="Assets/CheckpointKit/Prefabs/CheckpointSocket.prefab";var root=PrefabUtility.LoadPrefabContents(prefabPath);
 var objective=root.GetComponentInChildren<CheckpointObjective>(true);var sound=objective.GetComponent<AudioSource>();if(!sound)sound=objective.gameObject.AddComponent<AudioSource>();
 sound.clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/CheckpointKit/Audio/CheckpointValidated.wav");sound.playOnAwake=false;sound.volume=.8f;sound.spatialBlend=.6f;sound.minDistance=2;sound.maxDistance=30;sound.dopplerLevel=0;
 Wire(objective.OnValidated,sound,sound.Play);PrefabUtility.SaveAsPrefabAsset(root,prefabPath);PrefabUtility.UnloadPrefabContents(root);
 EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();Debug.Log("INTERACTION FEEDBACK REPAIRED");
 }
 static void Clear(UnityEventBase ev,Object target){for(int i=ev.GetPersistentEventCount()-1;i>=0;i--)if(ev.GetPersistentTarget(i)==target)UnityEventTools.RemovePersistentListener(ev,i);}
 static void Wire(UnityEvent ev,Object t,UnityAction f){Clear(ev,t);UnityEventTools.AddVoidPersistentListener(ev,f);}
 static void Wire<T>(UnityEvent<T> ev,Object t,UnityAction f){Clear(ev,t);UnityEventTools.AddVoidPersistentListener(ev,f);}
}
