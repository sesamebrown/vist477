using UnityEngine;

public class ShardCollisionNotifier : MonoBehaviour
{
    public ShatterController owner;

    void OnTriggerEnter(Collider other)
    {
        if (owner != null && other.CompareTag("Chisel"))
        {
            owner.OnShardHit();
        }
    }
}