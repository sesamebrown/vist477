using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TeleportOnTouch : MonoBehaviour
{
    [SerializeField] private List<GameObject> activateWhenTouchedBy;
    [SerializeField] private String sceneToLoad;
    void OnTriggerEnter(Collider other)
    {
        Debug.Log("Touched by " + other.gameObject.name);
        if (activateWhenTouchedBy.Contains(other.gameObject))
        {
            Teleport();
        }
    }
    private void Teleport()
    {
        Debug.Log("Teleporting to " + sceneToLoad);
        SceneManager.LoadScene(sceneToLoad);
    }
}
