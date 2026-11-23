using UnityEngine;
using UnityEngine.Rendering;

/// <summary>World +X is east, -X west, and +Z north, matching the compass.</summary>
public sealed class DayNightCycle : MonoBehaviour
{
    [Header("Clock")]
    [Range(0f, 24f)] public float timeOfDay = 8f;
    [Min(10f)] public float cycleDurationSeconds = 600f;
    public bool advanceTime = true;

    [Header("Scene references")]
    public Light sun;
    public Light moon;
    public Renderer moonVisual;
    public Material skyboxMaterial;
    public Camera viewer;

    [Header("Lighting")]
    [Min(0f)] public float daylightIntensity = 1.3f;
    [Min(0f)] public float moonlightIntensity = .18f;
    [Range(.01f, 1f)] public float nightSkyExposure = .08f;

    [Header("Moon appearance")]
    [Min(10f)] public float moonDistance = 180f;
    [Range(.2f, 5f)] public float moonAngularDiameter = 2f;

    Material runtimeSkybox;
    Material previousSkybox;
    Light previousSun;
    AmbientMode previousAmbientMode;
    Color previousSky, previousEquator, previousGround, previousFog;
    float previousReflectionIntensity, previousAmbientIntensity;
    Vector3 moonDirection;
    bool initialized;
    LightSnapshot sunBeforeCycle, moonBeforeCycle;

    struct LightSnapshot
    {
        public Light light;
        public Quaternion rotation;
        public Color color;
        public float intensity;
        public bool enabled;

        public LightSnapshot(Light source)
        {
            light = source;
            rotation = source.transform.rotation;
            color = source.color;
            intensity = source.intensity;
            enabled = source.enabled;
        }

        public void Restore()
        {
            if (!light) return;
            light.transform.rotation = rotation;
            light.color = color;
            light.intensity = intensity;
            light.enabled = enabled;
        }
    }

    void OnEnable()
    {
        if (!Application.isPlaying || !sun || !moon || !skyboxMaterial)
            return;

        sunBeforeCycle = new LightSnapshot(sun);
        moonBeforeCycle = new LightSnapshot(moon);
        previousSkybox = RenderSettings.skybox;
        previousSun = RenderSettings.sun;
        previousAmbientMode = RenderSettings.ambientMode;
        previousSky = RenderSettings.ambientSkyColor;
        previousEquator = RenderSettings.ambientEquatorColor;
        previousGround = RenderSettings.ambientGroundColor;
        previousFog = RenderSettings.fogColor;
        previousReflectionIntensity = RenderSettings.reflectionIntensity;
        previousAmbientIntensity = RenderSettings.ambientIntensity;
        runtimeSkybox = new Material(skyboxMaterial);
        RenderSettings.skybox = runtimeSkybox;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientIntensity = 1f;
        initialized = true;
        ApplyTimeOfDay();
    }

    void Update()
    {
        if (!initialized)
            return;
        if (advanceTime)
            timeOfDay += Time.deltaTime * 24f / Mathf.Max(10f, cycleDurationSeconds);
        ApplyTimeOfDay();
    }

    /// <summary>Applies a clock value immediately. Sunrise is 06:00 and sunset is 18:00.</summary>
    public void ApplyTimeOfDay()
    {
        if (!initialized)
            return;

        timeOfDay = Mathf.Repeat(timeOfDay, 24f);
        float angle = (timeOfDay - 6f) * Mathf.Deg2Rad * 15f;
        Vector3 sunDirection = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        moonDirection = -sunDirection;
        sun.transform.rotation = Quaternion.LookRotation(-sunDirection, Vector3.forward);
        moon.transform.rotation = Quaternion.LookRotation(-moonDirection, Vector3.forward);

        float daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-.12f, .25f, sunDirection.y));
        float sunStrength = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-.03f, .35f, sunDirection.y));
        sun.intensity = daylightIntensity * sunStrength;
        // Warm light only within half an hour of sunrise or sunset.
        float distanceFromHorizonTime = Mathf.Min(Mathf.Abs(timeOfDay - 6f), Mathf.Abs(timeOfDay - 18f));
        float twilightWarmth = 1f - Mathf.SmoothStep(0f, 1f, distanceFromHorizonTime / .5f);
        sun.color = Color.Lerp(Color.white, new Color(1f, .72f, .48f), twilightWarmth);
        sun.enabled = sun.intensity > .001f;
        moon.intensity = moonlightIntensity * (1f - daylight) * Mathf.Clamp01(moonDirection.y * 3f);
        moon.color = new Color(.52f, .66f, 1f);
        moon.enabled = moon.intensity > .001f;
        RenderSettings.sun = sun.enabled ? sun : moon;

        // Procedural sky already produces blue daytime scattering and warm horizons.
        // Its Sky Tint is neutral grey, rather than the final color of the sky.
        Color sky = Color.Lerp(new Color(.025f, .025f, .04f), new Color(.5f, .5f, .5f), daylight);
        runtimeSkybox.SetColor("_SkyTint", sky);
        runtimeSkybox.SetColor("_GroundColor", Color.Lerp(new Color(.015f, .02f, .04f), new Color(.369f, .349f, .341f), daylight));
        runtimeSkybox.SetFloat("_Exposure", Mathf.Lerp(nightSkyExposure, 1f, daylight));
        bool sunVisible = sunDirection.y > -.03f;
        runtimeSkybox.SetFloat("_SunDisk", sunVisible ? 2f : 0f);
        if (sunVisible)
        {
            runtimeSkybox.EnableKeyword("_SUNDISK_HIGH_QUALITY");
            runtimeSkybox.DisableKeyword("_SUNDISK_NONE");
        }
        else
        {
            runtimeSkybox.DisableKeyword("_SUNDISK_HIGH_QUALITY");
            runtimeSkybox.EnableKeyword("_SUNDISK_NONE");
        }
        runtimeSkybox.SetFloat("_SunSize", .025f);
        RenderSettings.ambientSkyColor = Color.Lerp(new Color(.075f, .10f, .18f), new Color(.212f, .227f, .259f), daylight);
        RenderSettings.ambientEquatorColor = Color.Lerp(new Color(.045f, .06f, .10f), new Color(.114f, .125f, .133f), daylight);
        RenderSettings.ambientGroundColor = Color.Lerp(new Color(.025f, .03f, .045f), new Color(.047f, .043f, .035f), daylight);
        RenderSettings.reflectionIntensity = Mathf.Lerp(.12f, .85f, daylight);
        if (RenderSettings.fog)
            RenderSettings.fogColor = Color.Lerp(new Color(.025f, .04f, .085f), new Color(.55f, .65f, .75f), daylight);
        PositionMoon();
    }

    void LateUpdate()
    {
        if (initialized)
            PositionMoon();
    }

    void PositionMoon()
    {
        if (!moonVisual)
            return;
        if (!viewer || !viewer.isActiveAndEnabled)
            viewer = Camera.main;
        if (!viewer)
        {
            moonVisual.enabled = false;
            return;
        }

        float distance = Mathf.Min(moonDistance, viewer.farClipPlane * .8f);
        float radius = distance * Mathf.Tan(moonAngularDiameter * .5f * Mathf.Deg2Rad);
        moonVisual.transform.position = viewer.transform.position + moonDirection * distance;
        moonVisual.transform.localScale = Vector3.one * radius * 2f;
        moonVisual.enabled = moonDirection.y > -.03f;
    }

    void OnDisable()
    {
        if (!initialized)
            return;
        initialized = false;
        RenderSettings.skybox = previousSkybox;
        RenderSettings.sun = previousSun;
        RenderSettings.ambientMode = previousAmbientMode;
        RenderSettings.ambientSkyColor = previousSky;
        RenderSettings.ambientEquatorColor = previousEquator;
        RenderSettings.ambientGroundColor = previousGround;
        RenderSettings.ambientIntensity = previousAmbientIntensity;
        RenderSettings.reflectionIntensity = previousReflectionIntensity;
        RenderSettings.fogColor = previousFog;
        if (moonVisual)
            moonVisual.enabled = false;
        sunBeforeCycle.Restore();
        moonBeforeCycle.Restore();
        Destroy(runtimeSkybox);
    }
}
