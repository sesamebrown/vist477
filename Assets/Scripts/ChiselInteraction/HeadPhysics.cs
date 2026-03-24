using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class HeadPhysics : MonoBehaviour
{
    XRGrabInteractable grabInteractable;
    Rigidbody rb;
    bool isHeld = false;
    bool makeNonKinematic = false;
    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
    }
    void Update()
    {
        if (grabInteractable.isSelected)
        {
            isHeld = true;
        }
        if (isHeld && !grabInteractable.isSelected)
        {
            isHeld = false;
            makeNonKinematic = true;
        }
        if (makeNonKinematic)
        {
            rb.isKinematic = false;
            makeNonKinematic = false;
            Debug.Log("rb.isKinematic: " + rb.isKinematic);
        }
    }
}
