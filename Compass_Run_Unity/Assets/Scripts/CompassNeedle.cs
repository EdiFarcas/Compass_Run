using UnityEngine;

/// <summary>Aligns the red needle tip to world +Z while keeping it in the compass face.</summary>
public class CompassNeedle : MonoBehaviour
{
    [Tooltip("The red tip points along local -Y in the imported Busola model.")]
    [SerializeField] Vector3 needleLocalNorth = Vector3.down;
    [Tooltip("The imported needle lies in its local XY plane.")]
    [SerializeField] Vector3 needleLocalNormal = Vector3.forward;

    Quaternion restingLocalRotation;

    void OnEnable()
    {
        restingLocalRotation = transform.localRotation;
    }

    void LateUpdate()
    {
        // Restore the model orientation before computing the correction for the current grip pose.
        transform.localRotation = restingLocalRotation;
        Vector3 normal = transform.TransformDirection(needleLocalNormal).normalized;
        Vector3 projectedNorth = Vector3.ProjectOnPlane(Vector3.forward, normal);
        if (projectedNorth.sqrMagnitude < 0.000001f)
            return; // North is perpendicular to the face, so no in-plane heading is defined.

        Vector3 redTip = Vector3.ProjectOnPlane(transform.TransformDirection(needleLocalNorth), normal);
        if (redTip.sqrMagnitude < 0.000001f)
            return;

        float angle = Vector3.SignedAngle(redTip, projectedNorth, normal);
        transform.rotation = Quaternion.AngleAxis(angle, normal) * transform.rotation;
    }
}
