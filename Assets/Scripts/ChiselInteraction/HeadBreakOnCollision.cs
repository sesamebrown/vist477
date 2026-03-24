using UnityEngine;

public class HeadBreakOnCollision : MonoBehaviour
{
    public GameObject brokenHeadPrefab;

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
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
