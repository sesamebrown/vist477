using UnityEngine;

public class LightFlame : MonoBehaviour
{
    LightInteractor m_LightInteractor;

    void Awake()
    {
        m_LightInteractor = GetComponentInParent<LightInteractor>();
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("Flame collided with " + other.gameObject.name);
        if (other.gameObject.TryGetComponent<Star>(out var star))
        {
            if (star.TryLight())
            {
                m_LightInteractor?.PlayStarLitBuzzSequence();
            }
        }
    }
}
