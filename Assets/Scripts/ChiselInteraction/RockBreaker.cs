using System.Collections.Generic;
using UnityEngine;

public class RockBreaker : MonoBehaviour
{
    public Transform fragmentsParent;
    public float breakRadius = 0.5f;
    public float force = 200f;

    [SerializeField]
    private int maxChunksPerHit = 6;

    [SerializeField]
    private float minImpulseScale = 0.2f;

    [SerializeField]
    private bool clearConstraintsOnBreak = true;

    public void BreakAtPoint(Vector3 hitPoint)
    {
        Debug.Log($"Breaking rock at point: {hitPoint} with radius: {breakRadius} and force: {force}");

        Transform parent = fragmentsParent != null ? fragmentsParent : transform;
        Rigidbody[] fragmentBodies = parent.GetComponentsInChildren<Rigidbody>(true);

        if (fragmentBodies.Length == 0)
        {
            Debug.LogWarning("No fragment rigidbodies found under fragmentsParent/rock object.");
            return;
        }

        int chunksInRadius = 0;
        int chunksActivated = 0;
        var candidates = new List<(Rigidbody rb, float dist)>();

        foreach (Rigidbody rb in fragmentBodies)
        {
            if (rb == null)
            {
                continue;
            }

            float dist = Vector3.Distance(rb.worldCenterOfMass, hitPoint);

            if (dist < breakRadius)
            {
                chunksInRadius++;
                candidates.Add((rb, dist));
            }
        }

        candidates.Sort((a, b) => a.dist.CompareTo(b.dist));

        int limit = Mathf.Min(maxChunksPerHit, candidates.Count);
        for (int i = 0; i < limit; i++)
        {
            var candidate = candidates[i];
            float distance01 = Mathf.Clamp01(candidate.dist / Mathf.Max(0.0001f, breakRadius));
            float impulseScale = 1f - distance01;

            if (impulseScale < minImpulseScale)
            {
                continue;
            }

            Rigidbody rb = candidate.rb;
            if (clearConstraintsOnBreak)
            {
                rb.constraints = RigidbodyConstraints.None;
            }

            rb.isKinematic = false;
            rb.useGravity = true;
            rb.WakeUp();

            Vector3 dir = rb.worldCenterOfMass - hitPoint;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Random.onUnitSphere;
            }
            else
            {
                dir.Normalize();
            }

            float chunkImpulse = force * impulseScale;
            rb.AddForce(dir * chunkImpulse, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * chunkImpulse, ForceMode.Impulse);
            chunksActivated++;
        }

        if (chunksInRadius == 0)
        {
            Debug.LogWarning("No chunks were inside breakRadius. Increase breakRadius or verify hit point alignment.");
        }
        else if (chunksActivated == 0)
        {
            Debug.LogWarning("Chunks were in range, but none were activated. Check minImpulseScale, force, or rigidbody constraints.");
        }
        else
        {
            Debug.Log($"Activated {chunksActivated}/{chunksInRadius} chunks within radius.");
        }
    }
}