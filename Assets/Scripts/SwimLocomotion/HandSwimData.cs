using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Tracks the movement data for a single controller/hand used in swimming locomotion.
/// </summary>
[System.Serializable]
public class HandSwimData
{
    [Tooltip("The XR node to track (LeftHand or RightHand)")]
    public XRNode xrNode;

    [Tooltip("Optional: Specific Input Device to track. If null, will use XRNode.")]
    public InputDevice targetDevice;

    // Current state
    private Vector3 currentPosition;
    private Quaternion currentRotation;
    private bool isTracked;

    // Previous frame state
    private Vector3 previousPosition;
    private Quaternion previousRotation;

    // Calculated values
    private Vector3 velocity;
    private float speed;

    // Input state
    private bool gripPressed;

    /// <summary>
    /// Current world position of the controller.
    /// </summary>
    public Vector3 CurrentPosition => currentPosition;

    /// <summary>
    /// Current rotation of the controller.
    /// </summary>
    public Quaternion CurrentRotation => currentRotation;

    /// <summary>
    /// Calculated velocity vector (units per second).
    /// </summary>
    public Vector3 Velocity => velocity;

    /// <summary>
    /// Speed of movement (magnitude of velocity).
    /// </summary>
    public float Speed => speed;

    /// <summary>
    /// Whether the controller is currently tracked by the XR system.
    /// </summary>
    public bool IsTracked => isTracked;

    /// <summary>
    /// Whether the grip button (side handle) is currently pressed.
    /// </summary>
    public bool GripPressed => gripPressed;

    /// <summary>
    /// Initializes the hand data with the specified XR node.
    /// </summary>
    public HandSwimData(XRNode node)
    {
        xrNode = node;
        currentPosition = Vector3.zero;
        previousPosition = Vector3.zero;
        currentRotation = Quaternion.identity;
        previousRotation = Quaternion.identity;
        velocity = Vector3.zero;
        speed = 0f;
        isTracked = false;
        gripPressed = false;
    }

    /// <summary>
    /// Updates tracking data for this hand. Should be called every frame.
    /// </summary>
    /// <param name="deltaTime">Time since last update</param>
    public void UpdateTracking(float deltaTime)
    {
        // Try to get the input device if we don't have it cached
        if (!targetDevice.isValid)
        {
            targetDevice = InputDevices.GetDeviceAtXRNode(xrNode);
        }

        if (!targetDevice.isValid)
        {
            isTracked = false;
            return;
        }

        // Update tracking state
        if (targetDevice.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked))
        {
            isTracked = tracked;
        }

        if (!isTracked)
        {
            velocity = Vector3.zero;
            speed = 0f;
            return;
        }

        // Store previous frame data
        previousPosition = currentPosition;
        previousRotation = currentRotation;

        // Get current position
        if (targetDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
        {
            currentPosition = position;
        }

        // Get current rotation
        if (targetDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
        {
            currentRotation = rotation;
        }

        // Calculate velocity (change in position over time)
        if (deltaTime > 0f)
        {
            velocity = (currentPosition - previousPosition) / deltaTime;
            speed = velocity.magnitude;
        }
        else
        {
            velocity = Vector3.zero;
            speed = 0f;
        }

        // Get grip button state
        if (targetDevice.TryGetFeatureValue(CommonUsages.gripButton, out bool grip))
        {
            gripPressed = grip;
        }
    }

    /// <summary>
    /// Checks if this hand should contribute to swimming propulsion based on current state.
    /// </summary>
    /// <param name="requireGrip">Whether the grip button must be pressed</param>
    /// <param name="minimumSpeed">Minimum speed threshold</param>
    /// <returns>True if this hand should generate propulsion</returns>
    public bool ShouldApplyForce(bool requireGrip, float minimumSpeed)
    {
        if (!isTracked)
            return false;

        if (speed < minimumSpeed)
            return false;

        if (requireGrip && !gripPressed)
            return false;

        return true;
    }

    /// <summary>
    /// Gets the forward direction of the hand (useful for rotation-based filtering).
    /// </summary>
    public Vector3 GetHandForward()
    {
        return currentRotation * Vector3.forward;
    }

    /// <summary>
    /// Gets the palm direction (useful for determining hand cupping).
    /// </summary>
    public Vector3 GetPalmDirection()
    {
        // In XR, typically the palm faces down (negative Y)
        return currentRotation * Vector3.down;
    }
}
