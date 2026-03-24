using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

public class ShatterController : MonoBehaviour
{
    private bool hasShattered;
    private int fallenShardCount;
    private readonly List<TrackedShard> trackedShards = new List<TrackedShard>();

    [SerializeField]
    private float movementThreshold = 0.001f;

    public int thresholdFragments;

    public GameObject head;

    [SerializeField]
    private XRGrabInteractable grabInteractable;
    [SerializeField]
    private XRGeneralGrabTransformer grabTransformer;

    private class TrackedShard
    {
        public Rigidbody body;
        public Vector3 startWorldPosition;
        public bool counted;
    }

    void Start()
    {
        fallenShardCount = 0;
        trackedShards.Clear();

        Rigidbody[] shardBodies = GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody shardBody in shardBodies)
        {
            if (shardBody == null || shardBody.transform == transform)
            {
                continue;
            }

            trackedShards.Add(new TrackedShard
            {
                body = shardBody,
                startWorldPosition = shardBody.worldCenterOfMass,
                counted = false,
            });
        }

        Debug.Log($"Tracking {trackedShards.Count} shards for movement detection.");
    }

    public void OnShardHit()
    {
        // Kept for compatibility with existing ShardCollisionNotifier references.
    }

    void Update()
    {
        UpdateFallenShardCount();

        if (hasShattered)
        {
            if (grabInteractable != null)
            {
                grabInteractable.enabled = true;
            }

            if (grabTransformer != null)
            {
                grabTransformer.enabled = true;
            }
        }
    }

    private void UpdateFallenShardCount()
    {
        if (hasShattered)
        {
            return;
        }

        for (int i = 0; i < trackedShards.Count; i++)
        {
            TrackedShard shard = trackedShards[i];
            if (shard == null || shard.counted || shard.body == null)
            {
                continue;
            }

            float movement = (shard.body.worldCenterOfMass - shard.startWorldPosition).sqrMagnitude;
            float threshold = movementThreshold * movementThreshold;
            if (movement < threshold)
            {
                continue;
            }

            shard.counted = true;
            fallenShardCount++;
            Debug.Log($"Fallen shards: {fallenShardCount}");

            if (fallenShardCount >= thresholdFragments)
            {
                hasShattered = true;
                Debug.Log("ShatterController: Shattered by fallen shard threshold.");
                return;
            }
        }
    }

}
