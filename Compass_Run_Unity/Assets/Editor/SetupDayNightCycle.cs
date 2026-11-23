using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;

public static class SetupDayNightCycle
{

    [MenuItem("Tools/Compass Run/Setup Day Night Cycle")]
    static void Setup()
    {
        if (Application.isPlaying || EditorSceneManager.GetActiveScene().name != "SimulareVR" || Object.FindFirstObjectByType<DayNightCycle>())
            return;

        var sunObject = GameObject.Find("Directional Light");
        if (!sunObject || !sunObject.GetComponent<Light>())
            throw new System.Exception("The scene needs its Directional Light for the sun.");
        if (!AssetDatabase.IsValidFolder("Assets/Materials/Environment"))
            AssetDatabase.CreateFolder("Assets/Materials", "Environment");

        var sky = new Material(Shader.Find("Skybox/Procedural"));
        sky.name = "DayNightSky";
        sky.SetFloat("_SunDisk", 2f);
        sky.EnableKeyword("_SUNDISK_HIGH_QUALITY");
        sky.SetFloat("_SunSize", .025f);
        sky.SetFloat("_Exposure", 1f);
        sky.SetColor("_SkyTint", new Color(.5f, .5f, .5f));
        AssetDatabase.CreateAsset(sky, "Assets/Materials/Environment/DayNightSky.mat");
        var moonMaterial = new Material(Shader.Find("Compass Run/Moon Surface"));
        moonMaterial.name = "MoonSurface";
        AssetDatabase.CreateAsset(moonMaterial, "Assets/Materials/Environment/MoonSurface.mat");

        var root = new GameObject("Day Night Cycle");
        Undo.RegisterCreatedObjectUndo(root, "Add day and night cycle");
        var cycle = root.AddComponent<DayNightCycle>();
        cycle.sun = sunObject.GetComponent<Light>();
        cycle.sun.type = LightType.Directional;
        var lightObject = new GameObject("Moon Light");
        lightObject.transform.SetParent(root.transform, false);
        cycle.moon = lightObject.AddComponent<Light>();
        cycle.moon.type = LightType.Directional;
        cycle.moon.shadows = LightShadows.None;
        cycle.moon.color = new Color(.52f, .66f, 1f);
        cycle.moon.intensity = 0f;
        cycle.moon.enabled = false;
        var moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        moon.name = "Moon";
        moon.transform.SetParent(root.transform, false);
        Object.DestroyImmediate(moon.GetComponent<Collider>());
        cycle.moonVisual = moon.GetComponent<Renderer>();
        cycle.moonVisual.sharedMaterial = moonMaterial;
        cycle.moonVisual.shadowCastingMode = ShadowCastingMode.Off;
        cycle.moonVisual.receiveShadows = false;
        cycle.moonVisual.enabled = false;
        cycle.skyboxMaterial = sky;
        cycle.viewer = Camera.main;
        cycle.sun.transform.rotation = Quaternion.LookRotation(-new Vector3(.8660254f, .5f, 0), Vector3.forward);
        cycle.sun.intensity = cycle.daylightIntensity;
        RenderSettings.skybox = sky;
        RenderSettings.sun = cycle.sun;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        Debug.Log("DAY NIGHT CYCLE READY: sunrise 06:00 east +X, sunset 18:00 west -X, moon at night, 600-second cycle.");
    }
}
