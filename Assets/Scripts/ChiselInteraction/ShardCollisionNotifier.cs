using UnityEngine;

public class ShardCollisionNotifier : MonoBehaviour
{
    public ShatterController owner;
    public string hitTag = "Chisel";

    void OnTriggerEnter(Collider other)
    {
        if (owner != null && !string.IsNullOrWhiteSpace(hitTag) && other.CompareTag(hitTag))
        {
            owner.OnShardHit();
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (owner != null && !string.IsNullOrWhiteSpace(hitTag) && collision.collider.CompareTag(hitTag))
        {
            owner.OnShardHit();
        }
    }
}