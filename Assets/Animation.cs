using UnityEngine;

public class Animation : MonoBehaviour
{

    // Update is called once per frame
    void Update()
    {
        transform.Translate(new Vector3(15, 30, 45) * Time.deltaTime);
    }
}
