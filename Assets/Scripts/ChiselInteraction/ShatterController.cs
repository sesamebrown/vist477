using UnityEngine;

public class ShatterController : MonoBehaviour
{
    private bool hasShattered;

    void Start()
    {
        Collider[] shardColliders = GetComponentsInChildren<Collider>();

        foreach (Transform child in transform)
        {
            if (!child.CompareTag("Sculpture"))
            {
                GameObject childObject = child.gameObject;
                Rigidbody rb = childObject.GetComponent<Rigidbody>();
                Collider shardCollider = childObject.GetComponent<Collider>();
                MeshCollider meshCollider = childObject.GetComponent<MeshCollider>();

                rb.useGravity = false;
                rb.isKinematic = true;

                if (meshCollider != null)
                {
                    meshCollider.convex = true;
                }

                if (shardCollider != null)
                {
                    shardCollider.isTrigger = true;
                }

                var notifier = childObject.AddComponent<ShardCollisionNotifier>();
                notifier.owner = this;
            }
        }

        for (int i = 0; i < shardColliders.Length; i++)
        {
            for (int j = i + 1; j < shardColliders.Length; j++)
            {
                if (shardColliders[i].transform.parent == transform && shardColliders[j].transform.parent == transform)
                {
                    Physics.IgnoreCollision(shardColliders[i], shardColliders[j], true);
                }
            }
        }
    }

    public void OnShardHit()
    {
        if (hasShattered)
        {
            return;
        }

        hasShattered = true;

        foreach (Transform child in transform)
        {
            if (!child.CompareTag("Sculpture"))
            {
                GameObject childObject = child.gameObject;
                Rigidbody rb = childObject.GetComponent<Rigidbody>();
                Collider shardCollider = childObject.GetComponent<Collider>();

                rb.useGravity = true;
                rb.isKinematic = false;

                if (shardCollider != null)
                {
                    shardCollider.isTrigger = false;
                }
            }
        }
    }
}
