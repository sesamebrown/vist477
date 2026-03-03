using UnityEngine;
using UnityEngine.Splines;

[RequireComponent(typeof(Collider))]
public class SplineCurrent : MonoBehaviour
{
	[Header("Spline")]
	[Tooltip("Spline path that defines current flow direction.")]
	[SerializeField] private SplineContainer splineContainer;

	[Tooltip("Sampling resolution used to approximate nearest point on spline.")]
	[SerializeField] private int nearestPointSamples = 40;

	[Header("Current")]
	[Tooltip("Constant drift speed along the spline tangent (m/s).")]
	[SerializeField] private float currentDriftSpeed = 1.5f;

	[Header("Escape")]
	[Tooltip("Minimum player speed against the spline flow needed to resist drift and swim out.")]
	[SerializeField] private float escapeSpeedAgainstCurrent = 1.25f;

	private Collider triggerCollider;

	private void Awake()
	{
		triggerCollider = GetComponent<Collider>();
		triggerCollider.isTrigger = true;

		if (splineContainer == null)
		{
			splineContainer = GetComponent<SplineContainer>();
		}
	}

	private void OnTriggerStay(Collider other)
	{
		SwimLocomotionController swimmer = other.GetComponentInParent<SwimLocomotionController>();
		if (swimmer == null || splineContainer == null)
		{
			return;
		}

		float t = FindNearestSplineT(swimmer.transform.position);
		Vector3 flowDirection = (Vector3)splineContainer.EvaluateTangent(t);
		flowDirection = flowDirection.normalized;
		if (flowDirection.sqrMagnitude < 0.0001f)
		{
			return;
		}

		float opposingSpeed = Vector3.Dot(swimmer.CurrentVelocity, -flowDirection);
		if (opposingSpeed >= escapeSpeedAgainstCurrent)
		{
			return;
		}

		float currentSpeedAlongFlow = Vector3.Dot(swimmer.CurrentVelocity, flowDirection);
		float speedDelta = currentDriftSpeed - currentSpeedAlongFlow;
		swimmer.AddExternalVelocity(flowDirection * speedDelta);
	}

	private float FindNearestSplineT(Vector3 worldPosition)
	{
		int samples = Mathf.Max(2, nearestPointSamples);

		float bestT = 0f;
		float bestSqrDistance = float.MaxValue;

		for (int i = 0; i < samples; i++)
		{
			float t = i / (float)(samples - 1);
			Vector3 point = splineContainer.EvaluatePosition(t);
			float sqrDistance = (worldPosition - point).sqrMagnitude;

			if (sqrDistance < bestSqrDistance)
			{
				bestSqrDistance = sqrDistance;
				bestT = t;
			}
		}

		return bestT;
	}

	private void OnDrawGizmosSelected()
	{
		if (splineContainer == null)
		{
			return;
		}

		int samples = Mathf.Max(2, nearestPointSamples);
		Gizmos.color = Color.cyan;

		Vector3 previousPoint = splineContainer.EvaluatePosition(0f);
		for (int i = 1; i < samples; i++)
		{
			float t = i / (float)(samples - 1);
			Vector3 point = splineContainer.EvaluatePosition(t);
			Gizmos.DrawLine(previousPoint, point);
			previousPoint = point;
		}

		float midT = 0.5f;
		Vector3 midPoint = splineContainer.EvaluatePosition(midT);
		Vector3 midDirection = (Vector3)splineContainer.EvaluateTangent(midT);
		midDirection = midDirection.normalized;
		float arrowLength = Mathf.Max(0.5f, currentDriftSpeed);
		Vector3 arrowTip = midPoint + midDirection * arrowLength;

		Gizmos.DrawLine(midPoint, arrowTip);
		Gizmos.DrawSphere(arrowTip, 0.08f);
	}
}
