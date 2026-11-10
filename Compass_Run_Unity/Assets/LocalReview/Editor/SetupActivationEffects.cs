using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Linq;

public static class SetupActivationEffects
{
    [MenuItem("Tools/Compass Run/Setup Whistle and Spray Effects")]
    static void Setup()
    {
        if(Application.isPlaying||EditorSceneManager.GetActiveScene().name!="SimulareVR_Local")return;
        var whistle=Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None).FirstOrDefault(g=>g.name=="Fluier");
        var spray=Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None).FirstOrDefault(g=>g.name=="Spray_Ursi");
        if(!whistle||!spray||spray.transform.Find("Spray_Fum"))return;
        var clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/LocalReview/Effects/Fluier.wav");if(!clip)return;
        Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Whistle and spray activation");
        var sound=new GameObject("Fluier_Sunet");Undo.RegisterCreatedObjectUndo(sound,"Whistle sound");sound.transform.SetParent(whistle.transform,false);
        var meshes=whistle.GetComponentsInChildren<Renderer>();if(meshes.Length>0)sound.transform.position=meshes[0].bounds.center;
        var audio=sound.AddComponent<AudioSource>();audio.clip=clip;audio.playOnAwake=false;audio.loop=false;audio.volume=.4f;audio.spatialBlend=1;audio.minDistance=.5f;audio.maxDistance=15;audio.rolloffMode=AudioRolloffMode.Linear;audio.dopplerLevel=0;
        Undo.RecordObject(whistle,"Whistle Activate event");UnityEventTools.AddVoidPersistentListener(whistle.activated,audio.Play);
        var smoke=new GameObject("Spray_Fum");Undo.RegisterCreatedObjectUndo(smoke,"Spray smoke");smoke.transform.SetParent(spray.transform,false);
        Vector3 point=spray.transform.position,axis=spray.transform.up;
        foreach(var filter in spray.GetComponentsInChildren<MeshFilter>())
        {
            var renderer=filter.GetComponent<Renderer>();if(!renderer||!filter.sharedMesh)continue;
            var materials=renderer.sharedMaterials;
            for(int i=0;i<materials.Length&&i<filter.sharedMesh.subMeshCount;i++)
            {
                if(!materials[i]||!(materials[i].name.Contains("Nozzle")||materials[i].name.Contains("Black")))continue;
                var cap=filter.sharedMesh.GetSubMesh(i).bounds;var center=filter.sharedMesh.bounds.center;
                var delta=cap.center-center;if(delta.sqrMagnitude<.000001f)continue;
                axis=filter.transform.TransformDirection(delta.normalized).normalized;point=filter.transform.TransformPoint(cap.center)+axis*.015f;
            }
        }
        smoke.transform.position=point;smoke.transform.rotation=Quaternion.LookRotation(axis);
        var ps=smoke.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.playOnAwake=false;main.loop=true;main.duration=1;main.startLifetime=new ParticleSystem.MinMaxCurve(.4f,.8f);main.startSpeed=new ParticleSystem.MinMaxCurve(.8f,1.6f);main.startSize=new ParticleSystem.MinMaxCurve(.035f,.07f);main.startColor=new Color(1,.35f,.06f,.65f);main.maxParticles=250;main.simulationSpace=ParticleSystemSimulationSpace.World;
        var emission=ps.emission;emission.rateOverTime=55;
        var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=12;shape.radius=.006f;
        var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.8f,.12f),new GradientAlphaKey(0,1)});color.color=gradient;
        var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.5f,1,2.5f));
        var noise=ps.noise;noise.enabled=true;noise.strength=.1f;noise.frequency=1;noise.scrollSpeed=.4f;noise.quality=ParticleSystemNoiseQuality.Low;
        var material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));material.name="Spray_Fum_Orange";material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LocalReview/Effects/FumSoft.png"));material.SetColor("_BaseColor",Color.white);material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;
        AssetDatabase.CreateAsset(material,"Assets/LocalReview/Effects/Spray_Fum_Orange.mat");
        var pr=ps.GetComponent<ParticleSystemRenderer>();pr.sharedMaterial=material;pr.renderMode=ParticleSystemRenderMode.Billboard;pr.shadowCastingMode=ShadowCastingMode.Off;
        Undo.RecordObject(spray,"Spray Activate and Deactivate events");UnityEventTools.AddVoidPersistentListener(spray.activated,ps.Play);UnityEventTools.AddVoidPersistentListener(spray.deactivated,ps.Stop);UnityEventTools.AddVoidPersistentListener(spray.selectExited,ps.Stop);
        EditorUtility.SetDirty(whistle);EditorUtility.SetDirty(spray);PrefabUtility.RecordPrefabInstancePropertyModifications(whistle);PrefabUtility.RecordPrefabInstancePropertyModifications(spray);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("ACTIVATION EFFECTS READY: Fluier Activated -> AudioSource.Play; Spray Activated -> ParticleSystem.Play, Deactivated/Select Exited -> Stop. No runtime script required.");
    }
}
