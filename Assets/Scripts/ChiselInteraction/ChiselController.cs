// using System.Diagnostics;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Feedback;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

public class ChiselController : MonoBehaviour
{
    private SphereCollider chiselTip;
    private SimpleHapticFeedback hapticFeedback;

    [SerializeField]
    private float maxImpactForce = 5f;

    [SerializeField]
    private float hapticDuration = 0.1f;

    [SerializeField]
    private float hapticFrequency = 0f;

    void Start()
    {
        chiselTip = GetComponent<SphereCollider>();
        hapticFeedback = GetComponent<SimpleHapticFeedback>();
    }


    // calculate the force vector of the hammer hit and use that to scale the haptic feedback intensity

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Hammer"))
        {
            // Handle the interaction with the hammer object here
            Debug.Log("Hit chisel");

            var hammerBody = other.attachedRigidbody;
            var hammerVelocity = hammerBody != null ? hammerBody.linearVelocity : Vector3.zero;
            var forceVector = hammerBody != null ? hammerVelocity * hammerBody.mass : hammerVelocity;
            var intensity = Mathf.Clamp01(forceVector.magnitude / Mathf.Max(0.01f, maxImpactForce));

            if (hapticFeedback != null)
            {
                var player = hapticFeedback.hapticImpulsePlayer;
                // if (player == null)
                //     player = HapticImpulsePlayer.GetOrCreateInHierarchy(gameObject);

                player.SendHapticImpulse(intensity, hapticDuration, hapticFrequency);
                Debug.Log($"Haptic feedback sent with intensity: {intensity}, duration: {hapticDuration}, frequency: {hapticFrequency}");
            }
        }
    }
}
