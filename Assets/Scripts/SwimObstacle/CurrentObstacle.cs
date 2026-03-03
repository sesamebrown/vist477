using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CurrentObstacle : MonoBehaviour
{
    [Header("Current")]
    [Tooltip("Direction objects will drift toward. Uses this transform's forward if left empty.")]
    [SerializeField] private Transform currentDirectionSource;

    [Tooltip("Constant drift speed this current tries to maintain along its direction (m/s).")]
    [SerializeField] private float currentDriftSpeed = 1.5f;

    [Header("Escape")]
    [Tooltip("Minimum player speed against the current needed to resist drift and swim out.")]
    [SerializeField] private float escapeSpeedAgainstCurrent = 1.25f;

    private Collider currentCollider;

    private void Awake()
    {
        currentCollider = GetComponent<Collider>();
        currentCollider.isTrigger = true;
    }

    private void OnTriggerStay(Collider other)
    {
        SwimLocomotionController swimmer = other.GetComponentInParent<SwimLocomotionController>();
        if (swimmer == null)
        {
            return;
        }

        Transform directionTransform = currentDirectionSource != null ? currentDirectionSource : transform;
        Vector3 currentDirection = directionTransform.forward.normalized;

        float opposingSpeed = Vector3.Dot(swimmer.CurrentVelocity, -currentDirection);
        if (opposingSpeed >= escapeSpeedAgainstCurrent)
        {
            return;
        }

        float currentSpeedAlongFlow = Vector3.Dot(swimmer.CurrentVelocity, currentDirection);
        float speedDelta = currentDriftSpeed - currentSpeedAlongFlow;
        swimmer.AddExternalVelocity(currentDirection * speedDelta);
    }

    private void OnDrawGizmosSelected()
    {
        Transform directionTransform = currentDirectionSource != null ? currentDirectionSource : transform;
        Vector3 direction = directionTransform.forward.normalized;

        Vector3 origin = transform.position;
        float arrowLength = Mathf.Max(0.5f, currentDriftSpeed);
        Vector3 tip = origin + direction * arrowLength;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, tip);
        Gizmos.DrawSphere(tip, 0.08f);

        Vector3 rightWing = Quaternion.AngleAxis(160f, Vector3.up) * direction;
        Vector3 leftWing = Quaternion.AngleAxis(-160f, Vector3.up) * direction;
        float wingLength = arrowLength * 0.2f;

        Gizmos.DrawLine(tip, tip + rightWing * wingLength);
        Gizmos.DrawLine(tip, tip + leftWing * wingLength);
    }
}
