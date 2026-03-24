// using System.Diagnostics;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Feedback;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

public class ChiselController : MonoBehaviour
{
    private SphereCollider chiselHit;
    private SimpleHapticFeedback hapticFeedback;
    private HapticImpulsePlayer hapticPlayer;
    private bool insideSculptureShattered;
    private bool warnedMissingSculpture;
    private bool warnedMissingHaptics;

    [SerializeField]
    private float hapticDuration = 0.1f;

    [SerializeField]
    private float hapticFrequency = 0f;

    [SerializeField]
    private float maxHitVelocityForFullHaptics = 5f;

    [SerializeField]
    private float terminalHitVelocity = 4.5f;

    [SerializeField]
    private GameObject insideSculptureObject;

    [SerializeField]
    private GameObject shatteredInsideSculpturePrefab;

    [SerializeField]
    private Transform shatteredSpawnParent;

    [SerializeField]
    private string insideSculptureTag = "Sculpture";

    public Transform chiselTip;

    void Start()
    {
        chiselHit = GetComponent<SphereCollider>();
        hapticFeedback = GetComponent<SimpleHapticFeedback>();

        if (hapticFeedback != null)
        {
            hapticPlayer = hapticFeedback.hapticImpulsePlayer;
        }

        if (hapticPlayer == null)
        {
            hapticPlayer = GetComponentInParent<HapticImpulsePlayer>();
        }

        if (hapticPlayer == null)
        {
            hapticPlayer = GetComponentInChildren<HapticImpulsePlayer>(true);
        }

        ResolveInsideSculptureReference();
    }


    // calculate the force vector of the hammer hit and use that to scale the haptic feedback intensity

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Hammer"))
        {
            return;
        }

        float hammerSpeed = GetHammerSpeed(other, 0f);
        HandleHammerHit(hammerSpeed);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.CompareTag("Hammer"))
        {
            return;
        }

        float hammerSpeed = GetHammerSpeed(collision.collider, collision.relativeVelocity.magnitude);
        HandleHammerHit(hammerSpeed);
    }

    private float GetHammerSpeed(Collider hammerCollider, float fallbackSpeed)
    {
        Rigidbody hammerBody = hammerCollider != null ? hammerCollider.attachedRigidbody : null;
        if (hammerBody != null)
        {
            return hammerBody.linearVelocity.magnitude;
        }

        return fallbackSpeed;
    }

    private void HandleHammerHit(float hammerSpeed)
    {
        Debug.Log($"Hit chisel. Hammer speed: {hammerSpeed:0.00}");

        TryShatterInsideSculpture(hammerSpeed);
        SendScaledHaptics(hammerSpeed);
    }

    private void SendScaledHaptics(float hammerSpeed)
    {
        if (hapticPlayer == null)
        {
            if (hapticFeedback != null)
            {
                hapticPlayer = hapticFeedback.hapticImpulsePlayer;
            }
        }

        if (hapticPlayer == null)
        {
            hapticPlayer = GetComponentInParent<HapticImpulsePlayer>();
        }

        if (hapticPlayer == null)
        {
            hapticPlayer = GetComponentInChildren<HapticImpulsePlayer>(true);
        }

        if (hapticPlayer == null)
        {
            if (!warnedMissingHaptics)
            {
                Debug.LogWarning("No HapticImpulsePlayer found for ChiselController. Haptics will not play.");
                warnedMissingHaptics = true;
            }

            return;
        }

        float intensity = Mathf.Clamp01(hammerSpeed / Mathf.Max(0.01f, maxHitVelocityForFullHaptics));
        hapticPlayer.SendHapticImpulse(intensity, hapticDuration, hapticFrequency);
        Debug.Log($"Haptic feedback sent with intensity: {intensity:0.00}, duration: {hapticDuration}, frequency: {hapticFrequency}");
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
            ResolveInsideSculptureReference();
        }

        if (insideSculptureObject == null)
        {
            if (!warnedMissingSculpture)
            {
                Debug.LogWarning("Terminal hit reached, but insideSculptureObject is not assigned and could not be auto-found.");
                warnedMissingSculpture = true;
            }

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

    private void ResolveInsideSculptureReference()
    {
        if (insideSculptureObject != null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(insideSculptureTag))
        {
            GameObject foundInside = GameObject.FindWithTag(insideSculptureTag);
            if (foundInside != null)
            {
                insideSculptureObject = foundInside;
            }
        }
    }

}
