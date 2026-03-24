using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class FinishedPedestal : MonoBehaviour
{
    [SerializeField]
    private GameObject fakeStatue;

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Sculpture"))
        {
            Debug.Log("Statue on final pedestal");

            // delete statue
            Destroy(collision.gameObject);

            // show fake statue
            if (fakeStatue != null)
            {
                fakeStatue.SetActive(true);
            }
            else
            {
                Debug.LogWarning("Fake statue not assigned!");
            }
        }
    }
}
