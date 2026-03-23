using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class SplineCurrent : MonoBehaviour
{
	[Header("Triggers")]
	[Tooltip("Optional trigger colliders used for detection. If assigned, these are used instead of this object's trigger collider.")]
	[SerializeField] private Collider[] triggerColliders;

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
	private readonly List<SplineCurrentTriggerRelay> relays = new List<SplineCurrentTriggerRelay>();

	private void Awake()
	{
		triggerCollider = GetComponent<Collider>();

		bool hasCustomTriggers = triggerColliders != null && triggerColliders.Length > 0;
		if (hasCustomTriggers)
		{
			ConfigureCustomTriggers();
		}
		else
		{
			triggerCollider.isTrigger = true;
		}

		if (splineContainer == null)
		{
			splineContainer = GetComponent<SplineContainer>();
		}
	}

	private void OnDestroy()
	{
		for (int i = 0; i < relays.Count; i++)
		{
			if (relays[i] != null)
			{
				relays[i].ClearOwner(this);
			}
		}
	}

	private void ConfigureCustomTriggers()
	{
		if (triggerCollider != null)
		{
			triggerCollider.isTrigger = false;
		}

		relays.Clear();

		for (int i = 0; i < triggerColliders.Length; i++)
		{
			Collider customTrigger = triggerColliders[i];
			if (customTrigger == null)
			{
				continue;
			}

			customTrigger.isTrigger = true;
			SplineCurrentTriggerRelay relay = customTrigger.GetComponent<SplineCurrentTriggerRelay>();
			if (relay == null)
			{
				relay = customTrigger.gameObject.AddComponent<SplineCurrentTriggerRelay>();
			}

			relay.SetOwner(this);
			relays.Add(relay);
		}
	}

	private void OnTriggerStay(Collider other)
	{
		if (triggerColliders != null && triggerColliders.Length > 0)
		{
			return;
		}

		ApplyCurrent(other);
	}

	public void HandleExternalTriggerStay(Collider other)
	{
		ApplyCurrent(other);
	}

	private void ApplyCurrent(Collider other)
	{
		SwimLocomotionController swimmer = other.GetComponentInParent<SwimLocomotionController>();
		if (swimmer == null || splineContainer == null)
		{
			return;
		}

		float t = FindNearestSplineT(other.bounds.center);
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

		Vector3 desiredVelocity = flowDirection * currentDriftSpeed;
		Vector3 velocityDelta = desiredVelocity - swimmer.CurrentVelocity;
		swimmer.AddExternalVelocity(velocityDelta);
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

public class SplineCurrentTriggerRelay : MonoBehaviour
{
	private SplineCurrent owner;

	public void SetOwner(SplineCurrent splineCurrent)
	{
		owner = splineCurrent;
	}

	public void ClearOwner(SplineCurrent splineCurrent)
	{
		if (owner == splineCurrent)
		{
			owner = null;
		}
	}

	private void OnTriggerStay(Collider other)
	{
		if (owner == null || !owner.isActiveAndEnabled)
		{
			return;
		}

		owner.HandleExternalTriggerStay(other);
	}
}
