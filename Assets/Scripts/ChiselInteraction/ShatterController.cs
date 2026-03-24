using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

public class ShatterController : MonoBehaviour
{
    private bool hasShattered;
    private int fragmentHitCount;

    [SerializeField]
    private string hitTag = "Chisel";

    public int thresholdFragments;

    public GameObject head;

    [SerializeField]
    private XRGrabInteractable grabInteractable;
    [SerializeField]
    private XRGeneralGrabTransformer grabTransformer;

    void Start()
    {
        fragmentHitCount = 0;

        Collider[] fragmentColliders = GetComponentsInChildren<Collider>(true);
        int notifierCount = 0;

        foreach (Collider fragmentCollider in fragmentColliders)
        {
            if (fragmentCollider == null || fragmentCollider.transform == transform)
            {
                continue;
            }

            ShardCollisionNotifier notifier = fragmentCollider.GetComponent<ShardCollisionNotifier>();
            if (notifier == null)
            {
                notifier = fragmentCollider.gameObject.AddComponent<ShardCollisionNotifier>();
            }

            notifier.owner = this;
            notifier.hitTag = hitTag;
            notifierCount++;
        }

        Debug.Log($"Configured {notifierCount} shard collision notifiers.");
    }

    public void OnShardHit()
    {
        fragmentHitCount++;
        Debug.Log($"Fragment hits: {fragmentHitCount}");

        if (fragmentHitCount > thresholdFragments)
        {
            hasShattered = true;
            Debug.Log("ShatterController: Shattered!");
        }
    }

    void Update()
    {
        if (hasShattered)
        {
            grabInteractable.enabled = true;
            grabTransformer.enabled = true;
        }
    }

}
