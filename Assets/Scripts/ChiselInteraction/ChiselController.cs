// using System.Diagnostics;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Feedback;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

public class ChiselController : MonoBehaviour
{
    private SphereCollider chiselHit;
    private SimpleHapticFeedback hapticFeedback;
    private readonly Collider[] tipOverlapBuffer = new Collider[8];
    private bool insideSculptureShattered;

    [SerializeField]
    private float maxImpactForce = 5f;

    [SerializeField]
    private float hapticDuration = 0.1f;

    [SerializeField]
    private float hapticFrequency = 0f;

    [SerializeField]
    private float terminalHitVelocity = 4.5f;

    [SerializeField]
    private GameObject insideSculptureObject;

    [SerializeField]
    private GameObject shatteredInsideSculpturePrefab;

    [SerializeField]
    private Transform shatteredSpawnParent;

    [SerializeField]
    private float tipCheckRadius = 0.02f;

    public Transform chiselTip;

    void Start()
    {
        chiselHit = GetComponent<SphereCollider>();
        hapticFeedback = GetComponent<SimpleHapticFeedback>();

        if (insideSculptureObject == null)
        {
            var foundInside = GameObject.FindWithTag("Sculpture");
            if (foundInside != null)
            {
                insideSculptureObject = foundInside;
            }
        }
    }


    // calculate the force vector of the hammer hit and use that to scale the haptic feedback intensity

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Hammer"))
        {
            // Handle the interaction with the hammer object here
            Debug.Log("Hit chisel");

            Vector3 hitPoint = chiselTip.position;

            var hammerBody = other.attachedRigidbody;
            var hammerVelocity = hammerBody != null ? hammerBody.linearVelocity : Vector3.zero;
            var hammerSpeed = hammerVelocity.magnitude;
            var forceVector = hammerBody != null ? hammerVelocity * hammerBody.mass : hammerVelocity;
            var intensity = Mathf.Clamp01(forceVector.magnitude / Mathf.Max(0.01f, maxImpactForce));

            TryShatterInsideSculpture(hammerSpeed);

            if (hapticFeedback != null)
            {
                var player = hapticFeedback.hapticImpulsePlayer;
                // if (player == null)
                //     player = HapticImpulsePlayer.GetOrCreateInHierarchy(gameObject);

                player.SendHapticImpulse(intensity, hapticDuration, hapticFrequency);
                Debug.Log($"Haptic feedback sent with intensity: {intensity}, duration: {hapticDuration}, frequency: {hapticFrequency}");
            }

            // TryBreakRockAtTip(hitPoint);
        }
    }

    private void TryShatterInsideSculpture(float hammerSpeed)
    {
        if (insideSculptureShattered || hammerSpeed < terminalHitVelocity)
        {
            return;
        }

        if (shatteredInsideSculpturePrefab == null)
        {
            Debug.LogWarning("Terminal hit reached, but shatteredInsideSculpturePrefab is not assigned.");
            return;
        }

        if (insideSculptureObject == null)
        {
            Debug.LogWarning("Terminal hit reached, but insideSculptureObject is not assigned.");
            return;
        }

        Transform source = insideSculptureObject.transform;

        GameObject shattered = Instantiate(shatteredInsideSculpturePrefab, source.position, source.rotation);
        shattered.transform.localScale = source.lossyScale;

        if (shatteredSpawnParent != null)
        {
            shattered.transform.SetParent(shatteredSpawnParent, true);
        }
        else if (source.parent != null)
        {
            shattered.transform.SetParent(source.parent, true);
        }

        insideSculptureObject.SetActive(false);
        insideSculptureShattered = true;

        Debug.Log($"Terminal hit velocity reached ({hammerSpeed:0.00} >= {terminalHitVelocity:0.00}). Inside sculpture shattered.");
    }

}
