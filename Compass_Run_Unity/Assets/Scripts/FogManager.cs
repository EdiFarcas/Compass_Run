using System.Collections;
using UnityEngine;

public class FogManager : MonoBehaviour
{
    [Header("Referinte din Scena (se gasesc automat)")]
    public Transform cp1;
    public Transform cp2;
    [Tooltip("Lasa gol ca sa foloseasca automat Main Camera")]
    public Transform jucator;

    [Header("Distante de Declansare (in metri)")]
    public float distantaPlecareCP1 = 10f;
    public float distantaOprireCP2 = 6f;

    [Header("Densitate Ceata (Exponential Squared)")]
    public float densitateNormala = 0.005f;
    public float densitateCeataDensa = 0.24f;

    [Header("Temporizare si Viteza")]
    public float intarzierePornireSecunde = 2f;
    public float vitezaInstalare = 0.25f;
    public float vitezaRidicare = 0.4f;

    [Header("Stare (bifat temporar pentru test)")]
    public bool cp1Validat = true;

    private float densitateTinta;
    private float vitezaCurenta;
    private bool aAjunsLaCP1 = false;
    private bool ceataPornita = false;
    private bool hazardFinalizat = false;

    void Start()
    {
        if (jucator == null && Camera.main != null)
            jucator = Camera.main.transform;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = densitateNormala;
        densitateTinta = densitateNormala;
        vitezaCurenta = vitezaInstalare;

        CautaCheckpointuri();
        Debug.Log("[FogManager] Pornit! Jucator urmarit: " + (jucator != null ? jucator.name : "NICIUNUL"));
    }

    void Update()
    {
        RenderSettings.fogDensity = Mathf.Lerp(
            RenderSettings.fogDensity,
            densitateTinta,
            Time.deltaTime * vitezaCurenta
        );

        // Daca TrailGenerator a recreat CP1/CP2, le gaseste automat pe cele noi
        if (cp1 == null || cp2 == null)
            CautaCheckpointuri();

        if (jucator == null || hazardFinalizat) return;

        // 1. Verifica daca a ajuns la CP1 si apoi a plecat mai departe
        if (cp1 != null && !ceataPornita)
        {
            float distCP1 = DistantaOrizontala(jucator.position, cp1.position);

            if (!aAjunsLaCP1 && distCP1 <= distantaPlecareCP1)
            {
                aAjunsLaCP1 = true;
                Debug.Log("[FogManager] Ai intrat in zona CP1.");
            }
            else if (aAjunsLaCP1 && cp1Validat && distCP1 > distantaPlecareCP1)
            {
                PornesteCeataCuIntarziere();
            }
        }

        // 2. Verifica daca a ajuns la CP2 ca sa opreasca ceata definitiv
        if (cp2 != null && ceataPornita)
        {
            float distCP2 = DistantaOrizontala(jucator.position, cp2.position);

            if (distCP2 <= distantaOprireCP2)
            {
                OpresteCeataDefinitiv();
            }
        }
    }

    private void CautaCheckpointuri()
    {
        if (cp1 == null)
        {
            GameObject objCP1 = GameObject.Find("CP1");
            if (objCP1 != null)
            {
                cp1 = objCP1.transform;
                Debug.Log("[FogManager] Am gasit CP1 generat la pozitia: " + cp1.position);
            }
        }

        if (cp2 == null)
        {
            GameObject objCP2 = GameObject.Find("CP2");
            if (objCP2 != null)
            {
                cp2 = objCP2.transform;
                Debug.Log("[FogManager] Am gasit CP2 generat la pozitia: " + cp2.position);
            }
        }
    }

    private float DistantaOrizontala(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    public void MarcheazaCP1Validat()
    {
        cp1Validat = true;
        aAjunsLaCP1 = true;
        Debug.Log("[FogManager] CP1 validat cu inelul -> ceata este armata!");
    }

    public void PornesteCeataCuIntarziere()
    {
        if (!cp1Validat || hazardFinalizat || ceataPornita) return;

        ceataPornita = true;
        Debug.Log("[FogManager] Ai plecat de la CP1 -> porneste numaratoarea pentru ceata.");
        StartCoroutine(AsteaptaSiPorneste());
    }

    private IEnumerator AsteaptaSiPorneste()
    {
        yield return new WaitForSeconds(intarzierePornireSecunde);

        if (!hazardFinalizat)
        {
            Debug.Log("[FogManager] SE ASTERNE CEATA!");
            vitezaCurenta = vitezaInstalare;
            densitateTinta = densitateCeataDensa;
        }
    }

    public void OpresteCeataDefinitiv()
    {
        if (hazardFinalizat) return;

        Debug.Log("[FogManager] Ai ajuns la CP2 -> CEATA SE RIDICA DEFINITIV!");
        hazardFinalizat = true;
        StopAllCoroutines();
        vitezaCurenta = vitezaRidicare;
        densitateTinta = densitateNormala;
    }
}