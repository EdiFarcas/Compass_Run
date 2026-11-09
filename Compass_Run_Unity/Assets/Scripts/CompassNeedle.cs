using UnityEngine;

public class CompassNeedle : MonoBehaviour
{
    void Update()
    {
        // Nordul global în Unity este mereu axa Z (Vector3.forward)
        Vector3 northDirection = Vector3.forward;

        // Proiectăm direcția Nordului pe planul orizontal al busolei. 
        // Asta previne ca acul să iasă prin sticlă când jucătorul înclină busola în VR.
        Vector3 projectedNorth = Vector3.ProjectOnPlane(northDirection, transform.parent.up);

        // Rotim acul doar dacă busola nu este complet întoarsă cu fața în jos
        if (projectedNorth.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(projectedNorth, transform.parent.up);
        }
    }
}