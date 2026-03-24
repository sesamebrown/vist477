using UnityEngine;
using UnityEngine.XR;
using UnityEngine.SceneManagement;

public class ResetButton : MonoBehaviour
{
    [Header("Input Settings")]
    [Tooltip("Which hand controller to listen to")]
    [SerializeField] private XRNode controllerNode = XRNode.LeftHand;
    
    [Tooltip("Which button to use for reset (Primary = A/X, Secondary = B/Y)")]
    [SerializeField] private bool usePrimaryButton = true;
    
    private InputDevice targetDevice;
    private bool wasButtonPressed = false;

    void Update()
    {
        // Get the input device if we don't have it
        if (!targetDevice.isValid)
        {
            targetDevice = InputDevices.GetDeviceAtXRNode(controllerNode);
        }

        if (!targetDevice.isValid)
            return;

        // Check for button press
        bool isButtonPressed = false;
        
        if (usePrimaryButton)
        {
            targetDevice.TryGetFeatureValue(CommonUsages.primaryButton, out isButtonPressed);
        }
        else
        {
            targetDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out isButtonPressed);
        }

        // Detect button down (pressed this frame but not last frame)
        if (isButtonPressed && !wasButtonPressed)
        {
            OnResetButtonPressed();
        }

        wasButtonPressed = isButtonPressed;
    }

    private void OnResetButtonPressed()
    {
        Debug.Log($"Reset button pressed on {controllerNode}! Reloading scene...");
        
        // Reload the current scene
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }
}
