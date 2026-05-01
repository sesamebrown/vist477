using UnityEngine;

public class HeadBreakOnCollision : MonoBehaviour
{
    [SerializeField] bool breakOnGroundCollision = true;
    public GameObject brokenHeadPrefab;

    void OnCollisionEnter(Collision collision)
    {
        if (!breakOnGroundCollision)
        {
            return;
        }
        if (collision.gameObject.CompareTag("Ground") || collision.gameObject.CompareTag("Wall"))
        {
            Debug.Log("Head collided with ground, breaking!");

            // replace this object with a broken version
            if (brokenHeadPrefab != null)
            {
                Instantiate(brokenHeadPrefab, transform.position, transform.rotation);
                Destroy(gameObject);
            }
            else
            {
                Debug.LogWarning("Broken head prefab not assigned!");
            }
        }
    }
}
