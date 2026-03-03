using UnityEngine;

public class LightFlame : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        Debug.Log("Flame collided with " + other.gameObject.name);
        if (other.gameObject.TryGetComponent<Star>(out var star))
        {
            // Pass the flame's position as the collision point
            star.Light(transform.position);
        }
    }
}
