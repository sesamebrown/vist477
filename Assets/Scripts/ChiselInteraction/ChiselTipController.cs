using UnityEngine;

public class ChiselTipController : MonoBehaviour
{
    [SerializeField]
    private SphereCollider hitPoint;
    private readonly Collider[] hammerOverlapBuffer = new Collider[8];

    void Start()
    {
        if (hitPoint == null)
        {
            Debug.LogError("hitPoint not defined");
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Rock") && IsHitPointTouchingHammer())
        {
            // Debug.Log("Rock is touching chisel tip while hammer touches hitPoint");

            var rockBreaker = other.GetComponent<RockBreaker>();

            if (rockBreaker != null)
            {
                Debug.Log("Breaking rock at tip");
                rockBreaker.BreakAtPoint(transform.position);
            }
        }
    }

    private bool IsHitPointTouchingHammer()
    {
        if (hitPoint == null)
        {
            return false;
        }

        float lossyScale = Mathf.Max(
            hitPoint.transform.lossyScale.x,
            hitPoint.transform.lossyScale.y,
            hitPoint.transform.lossyScale.z
        );

        float worldRadius = hitPoint.radius * lossyScale;
        int overlaps = Physics.OverlapSphereNonAlloc(
            hitPoint.transform.position,
            worldRadius,
            hammerOverlapBuffer,
            ~0,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < overlaps; i++)
        {
            var overlap = hammerOverlapBuffer[i];

            if (overlap != null && overlap != hitPoint && overlap.CompareTag("Hammer"))
            {
                return true;
            }
        }

        return false;
    }

}
