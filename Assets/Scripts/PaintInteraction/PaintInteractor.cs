using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

/// <summary>
/// Interactor that allows painting in 3D space by creating line renderers when trigger is held.
/// Follows XR Interaction Toolkit patterns for input handling and lifecycle management.
/// Implements ICurveInteractionDataProvider to provide visual feedback via CurveVisualController.
/// </summary>
[AddComponentMenu("XR/Interactors/XR Paint Interactor")]
public class XRPaintInteractor : MonoBehaviour, ICurveInteractionDataProvider
{
    [Header("References")]
    [SerializeField]
    [Tooltip("Paint point component that represents where paint will spawn. Should be a child object positioned by another system.")]
    PaintPoint m_PaintPoint;
    [SerializeField]
    [Tooltip("Haptics manager component that handles vibration feedback. Should be a child object positioned by another system.")]
    HapticsManager m_HapticsManager;

    [SerializeField]
    [Tooltip("Reference to the paint game manager to check if all zones are completed. If null, painting is always allowed.")]
    PaintGameManager m_PaintGameManager;

    [Header("Zone Interaction Mode")]
    [SerializeField]
    [Tooltip("If true, allows painting when a ray from the controller hits a zone (like UI interaction). If false, requires physical entry into zones.")]
    bool m_AllowRaycastZoneDetection = false;

    [SerializeField]
    [Tooltip("Maximum distance for raycast zone detection. Only used when raycast mode is enabled.")]
    float m_RaycastMaxDistance = 10f;

    [SerializeField]
    [Tooltip("Direction of the raycast relative to the paint point. Default is forward.")]
    Vector3 m_RaycastDirection = Vector3.forward;

    [SerializeField]
    [Tooltip("Show debug gizmo for the raycast when this GameObject is selected in the editor.")]
    bool m_ShowRaycastGizmo = true;

    [SerializeField]
    [Tooltip("Length of the visual indicator line when not pointing at a zone. Gives directional feedback.")]
    float m_RestingLineLength = 0.3f;

    [SerializeField]
    [Tooltip("Smoothing amount for raycast paint position (0=instant, 1=maximum smoothing). Reduces jitter when painting with controller rotation.")]
    [Range(0f, 0.95f)]
    float m_RaycastPaintSmoothing = 0.7f;

    /// <summary>
    /// Paint point component that represents where paint will spawn.
    /// This should be positioned by external systems (e.g., controller tracking, hand tracking).
    /// </summary>
    public PaintPoint paintPoint
    {
        get => m_PaintPoint;
        set => m_PaintPoint = value;
    }

    [Header("Input")]
    [SerializeField]
    [Tooltip("Input to trigger painting. Typically the controller trigger.")]
    XRInputButtonReader m_PaintInput = new XRInputButtonReader("Paint");

    /// <summary>
    /// Input reader for the paint trigger button.
    /// </summary>
    public XRInputButtonReader paintInput
    {
        get => m_PaintInput;
        set => m_PaintInput = value;
    }

    [SerializeField]
    [Tooltip("Input to cycle through line size presets. Typically a button like primary button or grip.")]
    XRInputButtonReader m_CycleSizeInput = new XRInputButtonReader("Cycle Size");

    /// <summary>
    /// Input reader for cycling through line sizes.
    /// </summary>
    public XRInputButtonReader cycleSizeInput
    {
        get => m_CycleSizeInput;
        set => m_CycleSizeInput = value;
    }


    [SerializeField]
    [Tooltip("Input to cycle through line color presets. Typically a button like primary button or grip.")]
    XRInputButtonReader m_CycleColorInput = new XRInputButtonReader("Cycle Color");

    /// <summary>
    /// Input reader for cycling through line colors.
    /// </summary>
    public XRInputButtonReader cycleColorInput
    {
        get => m_CycleColorInput;
        set => m_CycleColorInput = value;
    }

    [Header("Paint Settings")]
    [SerializeField]
    [Tooltip("Preset line widths to cycle through. User can switch between these using the cycle size button.")]
    float[] m_LineSizePresets = new float[] { 0.002f, 0.005f, 0.010f, 0.020f, 0.050f };
    [SerializeField]
    [Tooltip("Preset line colors to cycle through. User can switch between these using the cycle color button.")]
    Color[] m_LineColorPresets = new Color[] { Color.red, Color.green, Color.blue, Color.yellow, Color.magenta };

    /// <summary>
    /// Array of preset line widths that users can cycle through.
    /// </summary>
    public float[] lineSizePresets
    {
        get => m_LineSizePresets;
        set => m_LineSizePresets = value;
    }

    /// <summary>
    /// Array of preset line colors that users can cycle through.
    /// </summary>
    public Color[] lineColorPresets
    {
        get => m_LineColorPresets;
        set => m_LineColorPresets = value;
    }

    [SerializeField]
    [Tooltip("Index of the currently selected size preset.")]
    int m_CurrentSizeIndex = 1; // Default to 0.005f (second preset)

    /// <summary>
    /// Index of the currently selected line size preset.
    /// </summary>
    public int currentSizeIndex
    {
        get => m_CurrentSizeIndex;
        set
        {
            if (m_LineSizePresets == null || m_LineSizePresets.Length == 0)
                return;
            m_CurrentSizeIndex = Mathf.Clamp(value, 0, m_LineSizePresets.Length - 1);
            m_LineWidth = m_LineSizePresets[m_CurrentSizeIndex];
            UpdatePaintPointIndicatorSize();
        }
    }
    
    [SerializeField]
    [Tooltip("Index of the currently selected color preset.")]
    int m_CurrentColorIndex = 0; // Default to first color preset

    /// <summary>
    /// Index of the currently selected line color preset.
    /// </summary>
    public int currentColorIndex
    {
        get => m_CurrentColorIndex;
        set
        {
            if (m_LineColorPresets == null || m_LineColorPresets.Length == 0)
                return;
            m_CurrentColorIndex = Mathf.Clamp(value, 0, m_LineColorPresets.Length - 1);
            m_LineColor = m_LineColorPresets[m_CurrentColorIndex];
            UpdatePaintPointIndicatorColor();
        }
    }

    [SerializeField]
    [Tooltip("Minimum distance the paint point must move before adding a new point to the line.")]
    float m_MinPointDistance = 0.01f;

    /// <summary>
    /// Minimum distance required between consecutive paint points.
    /// </summary>
    public float minPointDistance
    {
        get => m_MinPointDistance;
        set => m_MinPointDistance = Mathf.Max(0.001f, value);
    }

    [SerializeField]
    [Tooltip("Enable velocity-adaptive sampling. Adds more points when moving fast to prevent jagged edges.")]
    bool m_AdaptiveSampling = true;

    /// <summary>
    /// Whether to use velocity-adaptive sampling for smoother lines during fast movements.
    /// </summary>
    public bool adaptiveSampling
    {
        get => m_AdaptiveSampling;
        set => m_AdaptiveSampling = value;
    }

    [SerializeField]
    [Tooltip("Maximum distance between points. If movement exceeds this, intermediate points are added.")]
    float m_MaxPointDistance = 0.02f;

    /// <summary>
    /// Maximum allowed distance between consecutive points. Intermediate points added if exceeded.
    /// </summary>
    public float maxPointDistance
    {
        get => m_MaxPointDistance;
        set => m_MaxPointDistance = Mathf.Max(m_MinPointDistance, value);
    }

    [SerializeField]
    [Tooltip("Enable smooth line rendering by interpolating between points. Helps prevent harsh corners and overlaps.")]
    bool m_SmoothLines = true;

    /// <summary>
    /// Whether to smooth lines by interpolating between points.
    /// </summary>
    public bool smoothLines
    {
        get => m_SmoothLines;
        set => m_SmoothLines = value;
    }

    [SerializeField]
    [Tooltip("Enable input point averaging to reduce sharp corners and crinkles. Averages the last few input positions.")]
    bool m_AverageInputPoints = true;

    /// <summary>
    /// Whether to average input points before adding them to the line.
    /// </summary>
    public bool averageInputPoints
    {
        get => m_AverageInputPoints;
        set => m_AverageInputPoints = value;
    }

    [SerializeField]
    [Tooltip("Number of recent positions to average when smoothing input. Higher values = smoother but more lag.")]
    [Range(2, 5)]
    int m_InputAveragingWindow = 3;

    /// <summary>
    /// Window size for input point averaging.
    /// </summary>
    public int inputAveragingWindow
    {
        get => m_InputAveragingWindow;
        set => m_InputAveragingWindow = Mathf.Clamp(value, 2, 5);
    }

    [SerializeField]
    [Tooltip("Number of interpolated points to add between each input point when smoothing is enabled. Higher = smoother but more expensive.")]
    [Range(1, 10)]
    int m_SmoothingSegments = 3;

    /// <summary>
    /// Number of interpolated segments between input points for smoothing.
    /// </summary>
    public int smoothingSegments
    {
        get => m_SmoothingSegments;
        set => m_SmoothingSegments = Mathf.Clamp(value, 1, 10);
    }

    [SerializeField]
    [Tooltip("Width of the painted line.")]
    float m_LineWidth = 0.005f;

    /// <summary>
    /// Width of the painted lines.
    /// </summary>
    public float lineWidth
    {
        get => m_LineWidth;
        set => m_LineWidth = Mathf.Max(0.0001f, value);
    }

    [SerializeField]
    [Tooltip("Color of the painted line.")]
    Color m_LineColor = Color.black;

    /// <summary>
    /// Color of the painted lines.
    /// </summary>
    public Color lineColor
    {
        get => m_LineColor;
        set => m_LineColor = value;
    }

    [SerializeField]
    [Tooltip("Rotation offset applied to line orientation relative to controller. Adjust to get desired behavior.")]
    Vector3 m_LineOrientationOffset = new Vector3(0f, 0f, 90f);

    /// <summary>
    /// Euler angle offset for line orientation.
    /// </summary>
    public Vector3 lineOrientationOffset
    {
        get => m_LineOrientationOffset;
        set => m_LineOrientationOffset = value;
    }

    [SerializeField]
    [Tooltip("Material to use for painted lines.")]
    Material m_LineMaterial;

    /// <summary>
    /// Material used for rendered paint lines.
    /// </summary>
    public Material lineMaterial
    {
        get => m_LineMaterial;
        set => m_LineMaterial = value;
    }

    [SerializeField]
    [Tooltip("Parent transform to organize painted lines in hierarchy.")]
    Transform m_LinesParent;

    /// <summary>
    /// Parent transform for organizing painted line GameObjects.
    /// If null, lines will be created at the scene root.
    /// </summary>
    public Transform linesParent
    {
        get => m_LinesParent;
        set => m_LinesParent = value;
    }

    // State tracking
    bool m_IsPainting;
    PaintLine m_CurrentLine;
    Vector3 m_LastPaintPosition;
    bool m_PreviousCycleSizePressed;
    bool m_PreviousCycleColorPressed;
    List<Vector3> m_RecentPositions = new List<Vector3>();
    PaintZone m_ActivePaintZone; // The paint zone this controller is currently inside
    PaintZone m_RaycastDetectedZone; // The paint zone detected via raycast (if raycast mode enabled)
    Vector3 m_CachedRaycastHitPoint; // Cached hit point from raycast for use during painting
    bool m_HasCachedRaycastHit; // Whether we have a valid cached raycast hit
    Vector3 m_SmoothedRaycastPosition; // Smoothed raycast paint position to reduce jitter
    bool m_HasSmoothedPosition; // Whether we have a valid smoothed position
    bool m_IsUsingRaycastZone; // Whether current stroke is using a raycast-detected zone (not physical)
    bool m_RequireTriggerRelease; // Set when stroke is ended due to zone exit - requires trigger release before new stroke
    static int s_GlobalStrokeCounter = 0; // Global counter for all strokes to prevent z-fighting
    
    // For ICurveInteractionDataProvider
    NativeArray<Vector3> m_CurveSamplePoints;
    bool m_CurveSamplePointsInitialized;

    /// <summary>
    /// Whether the user is currently painting.
    /// </summary>
    public bool isPainting => m_IsPainting;

    /// <summary>
    /// The current active paint line being drawn, or null if not painting.
    /// </summary>
    public PaintLine currentLine => m_CurrentLine;

    // ICurveInteractionDataProvider implementation
    /// <inheritdoc />
    bool ICurveInteractionDataProvider.isActive => m_AllowRaycastZoneDetection && m_PaintPoint != null;
    
    /// <inheritdoc />
    bool ICurveInteractionDataProvider.hasValidSelect => m_IsPainting;
    
    /// <inheritdoc />
    NativeArray<Vector3> ICurveInteractionDataProvider.samplePoints => m_CurveSamplePoints;
    
    /// <inheritdoc />
    Vector3 ICurveInteractionDataProvider.lastSamplePoint
    {
        get
        {
            // If painting or have a valid zone hit, use the actual paint position
            if (m_IsPainting || (m_HasCachedRaycastHit && m_RaycastDetectedZone != null))
            {
                return GetPaintPosition();
            }
            
            // Otherwise show a short line for directional feedback
            if (m_PaintPoint != null)
            {
                Vector3 origin = m_PaintPoint.transform.position;
                Vector3 direction = m_PaintPoint.transform.TransformDirection(m_RaycastDirection).normalized;
                return origin + direction * m_RestingLineLength;
            }
            
            return Vector3.zero;
        }
    }
    
    /// <inheritdoc />
    Transform ICurveInteractionDataProvider.curveOrigin => m_PaintPoint != null ? m_PaintPoint.transform : null;

    /// <summary>
    /// Sets the active paint zone for plane constraint.
    /// </summary>
    public void SetActivePaintZone(PaintZone zone)
    {
        m_ActivePaintZone = zone;
    }

    /// <summary>
    /// Clears the active paint zone if it matches the provided zone.
    /// </summary>
    public void ClearActivePaintZone(PaintZone zone)
    {
        if (m_ActivePaintZone == zone)
        {
            m_ActivePaintZone = null;
        }
    }

    /// <summary>
    /// Projects a point onto the active paint zone's constraint plane if one is set.
    /// </summary>
    Vector3 ApplyPlaneConstraint(Vector3 point)
    {
        if (m_ActivePaintZone != null && m_ActivePaintZone.constrainToPlane)
        {
            Plane constraintPlane = m_ActivePaintZone.GetConstraintPlane();
            return constraintPlane.ClosestPointOnPlane(point);
        }
        return point;
    }

    void Awake()
    {
        // Validate paint point
        if (m_PaintPoint == null)
        {
            Debug.LogWarning($"[XRPaintInteractor] Paint point is not assigned on {gameObject.name}. " +
                "Paint interactor will not function until a PaintPoint component is assigned.", this);
        }
        else
        {
            // Set back-reference so PaintPoint can sync color
            m_PaintPoint.paintInteractor = this;
        }

        // Create default material if none assigned
        if (m_LineMaterial == null)
        {
            m_LineMaterial = CreateDefaultLineMaterial();
        }

        // Initialize line width from preset
        if (m_LineSizePresets != null && m_LineSizePresets.Length > 0)
        {
            m_CurrentSizeIndex = Mathf.Clamp(m_CurrentSizeIndex, 0, m_LineSizePresets.Length - 1);
            m_LineWidth = m_LineSizePresets[m_CurrentSizeIndex];
        }

        // Initialize line color from preset
        if (m_LineColorPresets != null && m_LineColorPresets.Length > 0)
        {
            m_CurrentColorIndex = Mathf.Clamp(m_CurrentColorIndex, 0, m_LineColorPresets.Length - 1);
            m_LineColor = m_LineColorPresets[m_CurrentColorIndex];
        }

        // Initialize curve sample points for ICurveInteractionDataProvider
        m_CurveSamplePoints = new NativeArray<Vector3>(2, Allocator.Persistent);
        m_CurveSamplePointsInitialized = true;
    }

    void OnEnable()
    {
        // Enable input readers
        m_PaintInput?.EnableDirectActionIfModeUsed();
        m_CycleSizeInput?.EnableDirectActionIfModeUsed();
        m_CycleColorInput?.EnableDirectActionIfModeUsed();
    }

    void OnDisable()
    {
        // Clean up any active paint stroke
        if (m_IsPainting)
        {
            EndPaintStroke();
        }

        // Disable input readers
        m_PaintInput?.DisableDirectActionIfModeUsed();
        m_CycleSizeInput?.DisableDirectActionIfModeUsed();
        m_CycleColorInput?.DisableDirectActionIfModeUsed();
    }

    void OnDestroy()
    {
        // Dispose of native arrays
        if (m_CurveSamplePointsInitialized && m_CurveSamplePoints.IsCreated)
        {
            m_CurveSamplePoints.Dispose();
            m_CurveSamplePointsInitialized = false;
        }
    }

    void Update()
    {
        if (m_PaintPoint == null)
            return;

        // Update raycast hit detection (always update to detect when ray leaves zone)
        if (m_AllowRaycastZoneDetection)
        {
            UpdateRaycastHit();
            
            // If painting in raycast mode and the ray left the zone, end the stroke
            // (Physical zone exits are handled by OnTriggerExit in PaintZone)
            if (m_IsPainting && m_IsUsingRaycastZone && m_RaycastDetectedZone == null)
            {
                Debug.Log("[XRPaintInteractor] Ray left paint zone - ending stroke");
                EndPaintStroke(requireTriggerRelease: true);
                return; // Skip rest of update since we just ended painting
            }
        }

        // Check for size cycling input (button press)
        bool cycleSizePressed = m_CycleSizeInput.ReadIsPerformed();
        if (cycleSizePressed && !m_PreviousCycleSizePressed)
        {
            CycleToNextSize();
        }
        m_PreviousCycleSizePressed = cycleSizePressed;

        // Check for color cycling input (button press)
        bool cycleColorPressed = m_CycleColorInput.ReadIsPerformed();
        if (cycleColorPressed && !m_PreviousCycleColorPressed)
        {
            CycleToNextColor();
        }
        m_PreviousCycleColorPressed = cycleColorPressed;

        // Check paint input state
        bool paintInputActive = m_PaintInput.ReadIsPerformed();

        // Clear trigger release requirement when trigger is released
        if (!paintInputActive && m_RequireTriggerRelease)
        {
            m_RequireTriggerRelease = false;
        }

        // Start painting
        if (paintInputActive && !m_IsPainting && !m_RequireTriggerRelease)
        {
            StartPaintStroke();
        }
        // Continue painting
        else if (paintInputActive && m_IsPainting)
        {
            ContinuePaintStroke();
        }
        // End painting
        else if (!paintInputActive && m_IsPainting)
        {
            EndPaintStroke();
        }
    }

    void StartPaintStroke()
    {
        // Get the effective zone for plane constraints and validation
        PaintZone effectiveZone = GetEffectiveZone();
        
        // Debug: Log the detection method
        if (effectiveZone != null)
        {
            string detectionMethod = (m_ActivePaintZone != null) ? "Physical Trigger" : "Raycast";
            Debug.Log($"[XRPaintInteractor] Starting paint in zone {effectiveZone.gameObject.name} via {detectionMethod}");
        }
        else
        {
            Debug.Log($"[XRPaintInteractor] Cannot start paint - no zone detected. Raycast enabled: {m_AllowRaycastZoneDetection}, Has cached hit: {m_HasCachedRaycastHit}, Cached zone: {(m_RaycastDetectedZone != null ? m_RaycastDetectedZone.gameObject.name : "null")}");
        }
        
        // Check if painting is allowed based on game state
        if (!IsPaintingAllowed())
        {
            // Not allowed to paint here - trigger haptic feedback if wrong color in zone
            if (effectiveZone != null && !effectiveZone.IsColorCorrect(m_LineColor))
            {
                if (m_HapticsManager != null)
                {
                    m_HapticsManager.WrongColorHaptic();
                }
            }
            return;
        }
        
        // Determine if we're using raycast or physical zone detection
        // Check if the effective zone came from raycast detection (not physical trigger)
        bool usingRaycast = m_AllowRaycastZoneDetection && 
                           effectiveZone != null && 
                           effectiveZone == m_RaycastDetectedZone &&
                           m_ActivePaintZone == null; // No physical zone active
        
        m_IsUsingRaycastZone = usingRaycast;
        
        // If using raycast mode, set the zone as active for plane constraints
        if (usingRaycast)
        {
            m_ActivePaintZone = effectiveZone;
        }

        m_IsPainting = true;
        
        // Initialize smoothed position for raycast painting
        m_SmoothedRaycastPosition = GetPaintPosition();
        m_HasSmoothedPosition = true;
        
        m_LastPaintPosition = m_SmoothedRaycastPosition;
        m_RecentPositions.Clear();
        m_RecentPositions.Add(m_LastPaintPosition);

        // Hide indicator while painting
        m_PaintPoint.HideIndicator();

        // Create new paint line
        GameObject lineObject = new GameObject($"PaintLine_{System.DateTime.Now:HHmmss_fff}");
        
        if (m_LinesParent != null)
            lineObject.transform.SetParent(m_LinesParent, false);
        
        // Set the line's rotation to match the paint point (controller) rotation with adjustable offset
        // This determines how the 2D line plane is oriented in 3D space
        lineObject.transform.rotation = m_PaintPoint.transform.rotation * Quaternion.Euler(m_LineOrientationOffset);
        
        // Apply tiny offset to prevent z-fighting between strokes on the same plane
        // Use plane normal if in a zone, otherwise use controller forward
        Vector3 offsetDirection = Vector3.forward;
        if (m_ActivePaintZone != null && m_ActivePaintZone.constrainToPlane)
        {
            Plane plane = m_ActivePaintZone.GetConstraintPlane();
            offsetDirection = plane.normal;
        }
        float offsetAmount = s_GlobalStrokeCounter * 0.0001f; // 0.1mm per stroke
        lineObject.transform.position += offsetDirection * offsetAmount;
        s_GlobalStrokeCounter++;

        m_CurrentLine = lineObject.AddComponent<PaintLine>();
        m_CurrentLine.Initialize(m_LineMaterial, m_LineColor, m_LineWidth, m_SmoothLines, m_SmoothingSegments, this);

        // Add initial point (with plane constraint if in a zone)
        Vector3 constrainedPosition = ApplyPlaneConstraint(m_LastPaintPosition);
        m_CurrentLine.AddPoint(constrainedPosition);
    }

    void ContinuePaintStroke()
    {
        if (m_CurrentLine == null)
            return;

        Vector3 currentPosition = GetPaintPosition();
        float distanceMoved = Vector3.Distance(currentPosition, m_LastPaintPosition);

        // Debug: Log if we're not adding points due to insufficient movement
        if (distanceMoved < m_MinPointDistance && distanceMoved > 0)
        {
            // Uncomment for verbose debugging:
            // Debug.Log($"[XRPaintInteractor] Not adding point - distance {distanceMoved:F6} < minDistance {m_MinPointDistance:F6}");
        }

        // Only add point if moved minimum distance
        if (distanceMoved >= m_MinPointDistance)
        {
            Vector3 positionToAdd = currentPosition;

            // Apply input averaging if enabled
            if (m_AverageInputPoints)
            {
                m_RecentPositions.Add(currentPosition);
                
                // Keep only the last N positions
                if (m_RecentPositions.Count > m_InputAveragingWindow)
                {
                    m_RecentPositions.RemoveAt(0);
                }

                // Calculate average position
                Vector3 sum = Vector3.zero;
                foreach (var pos in m_RecentPositions)
                {
                    sum += pos;
                }
                positionToAdd = sum / m_RecentPositions.Count;
            }

            // Apply plane constraint to the position if in a zone
            positionToAdd = ApplyPlaneConstraint(positionToAdd);

            // Adaptive sampling: if moving fast, add intermediate points
            if (m_AdaptiveSampling && distanceMoved > m_MaxPointDistance)
            {
                // Calculate how many intermediate points we need
                int numIntermediatePoints = Mathf.CeilToInt(distanceMoved / m_MaxPointDistance);
                
                // Add interpolated points between last and current position
                for (int i = 1; i <= numIntermediatePoints; i++)
                {
                    float t = (float)i / numIntermediatePoints;
                    Vector3 intermediatePoint = Vector3.Lerp(m_LastPaintPosition, positionToAdd, t);
                    // Intermediate points also get constrained
                    intermediatePoint = ApplyPlaneConstraint(intermediatePoint);
                    m_CurrentLine.AddPoint(intermediatePoint);
                }
            }

            m_CurrentLine.AddPoint(positionToAdd);
            m_LastPaintPosition = positionToAdd;
        }
    }

    /// <summary>
    /// Gets the position where paint should be applied.
    /// For raycast mode: continuously raycasts to follow controller rotation and position with smoothing.
    /// For physical mode: uses paint point position directly.
    /// </summary>
    Vector3 GetPaintPosition()
    {
        // If using raycast mode and currently painting, continuously raycast to follow controller
        if (m_AllowRaycastZoneDetection && m_IsPainting && m_ActivePaintZone != null && m_ActivePaintZone.constrainToPlane)
        {
            // Continuously raycast from current controller position/rotation
            Vector3 rayOrigin = m_PaintPoint.transform.position;
            Vector3 rayDirection = m_PaintPoint.transform.TransformDirection(m_RaycastDirection).normalized;
            Plane constraintPlane = m_ActivePaintZone.GetConstraintPlane();
            
            Vector3 targetPosition;
            
            // Intersect ray with the constraint plane
            if (constraintPlane.Raycast(new Ray(rayOrigin, rayDirection), out float distance))
            {
                targetPosition = rayOrigin + rayDirection * distance;
            }
            else
            {
                // Fallback: project current controller position onto plane if raycast fails
                targetPosition = constraintPlane.ClosestPointOnPlane(rayOrigin);
            }
            
            // Apply smoothing to reduce jitter
            if (m_HasSmoothedPosition && m_RaycastPaintSmoothing > 0f)
            {
                m_SmoothedRaycastPosition = Vector3.Lerp(targetPosition, m_SmoothedRaycastPosition, m_RaycastPaintSmoothing);
            }
            else
            {
                m_SmoothedRaycastPosition = targetPosition;
                m_HasSmoothedPosition = true;
            }
            
            return m_SmoothedRaycastPosition;
        }
        
        // If using raycast mode and we have a cached hit (but not painting), use that position
        if (m_AllowRaycastZoneDetection && m_HasCachedRaycastHit && m_RaycastDetectedZone != null)
        {
            return m_CachedRaycastHitPoint;
        }
        
        // Otherwise use physical paint point position
        return m_PaintPoint != null ? m_PaintPoint.transform.position : Vector3.zero;
    }

    /// <summary>
    /// Ends the current paint stroke. Can be called externally (e.g., when controller exits zone).
    /// Safe to call even if not currently painting.
    /// </summary>
    /// <param name="requireTriggerRelease">If true, requires the trigger to be released before allowing a new stroke to start.</param>
    public void EndPaintStroke(bool requireTriggerRelease = false)
    {
        if (!m_IsPainting)
            return;

        m_IsPainting = false;
        
        // Set flag to require trigger release if requested (typically when exiting zones)
        if (requireTriggerRelease)
        {
            m_RequireTriggerRelease = true;
        }
        
        // Show indicator after painting
        if (m_PaintPoint != null)
            m_PaintPoint.ShowIndicator();
        
        // Finalize the line
        if (m_CurrentLine != null)
        {
            m_CurrentLine.Finalize();
            m_CurrentLine = null;
        }

        // Clear raycast-detected zone if we were using it
        if (m_AllowRaycastZoneDetection && m_IsUsingRaycastZone)
        {
            m_ActivePaintZone = null;
            // Don't clear m_RaycastDetectedZone - it's managed by UpdateRaycastHit()
        }
        
        // Reset raycast mode flag
        m_IsUsingRaycastZone = false;
        
        // Clear smoothed position state
        m_HasSmoothedPosition = false;
        
        // Clear cached raycast hit
        m_HasCachedRaycastHit = false;
    }

    /// <summary>
    /// Creates a default unlit material for line rendering if none is assigned.
    /// Uses the Sprites/Default shader which supports vertex colors for LineRenderer.
    /// </summary>
    Material CreateDefaultLineMaterial()
    {
        // Use Sprites/Default shader which works well with LineRenderer
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogWarning("[XRPaintInteractor] Sprites/Default not found, trying Unlit/Transparent");
            shader = Shader.Find("Unlit/Transparent");
        }
        
        if (shader == null)
        {
            Debug.LogError("[XRPaintInteractor] No suitable shader found for line rendering!");
            return null;
        }

        Material mat = new Material(shader);
        mat.name = "DefaultPaintMaterial";
        
        // Set the material color to white so LineRenderer vertex colors show through properly
        mat.color = Color.white;
        
        // Use opaque rendering with depth writing for correct cross-plane depth sorting
        // Lines on distant planes will correctly layer based on actual Z-buffer depth
        mat.SetInt("_ZWrite", 1);
        
        // Use Geometry queue for opaque rendering with proper depth testing
        mat.renderQueue = 2450;
        
        Debug.Log($"[XRPaintInteractor] Created paint material: shader={shader.name}, renderQueue={mat.renderQueue}, ZWrite=1 (opaque)");
        
        return mat;
    }

    /// <summary>
    /// Cycles to the next line size in the presets array.
    /// Wraps back to the first size after reaching the last.
    /// </summary>
    public void CycleToNextSize()
    {
        if (m_LineSizePresets == null || m_LineSizePresets.Length == 0)
        {
            Debug.LogWarning("[XRPaintInteractor] Cannot cycle size - no presets defined.");
            return;
        }

        m_CurrentSizeIndex = (m_CurrentSizeIndex + 1) % m_LineSizePresets.Length;
        m_LineWidth = m_LineSizePresets[m_CurrentSizeIndex];
        UpdatePaintPointIndicatorSize();

        Debug.Log($"[XRPaintInteractor] Cycled to size preset {m_CurrentSizeIndex + 1}/{m_LineSizePresets.Length}: {m_LineWidth:F4}");
    }
    /// <summary>
    /// Cycles to the next line size in the presets array.
    /// Wraps back to the first size after reaching the last.
    /// </summary>
    public void CycleToNextColor()
    {
        if (m_LineColorPresets == null || m_LineColorPresets.Length == 0)
        {
            Debug.LogWarning("[XRPaintInteractor] Cannot cycle color - no presets defined.");
            return;
        }

        m_CurrentColorIndex = (m_CurrentColorIndex + 1) % m_LineColorPresets.Length;
        m_LineColor = m_LineColorPresets[m_CurrentColorIndex];
        UpdatePaintPointIndicatorColor();

        m_HapticsManager.PlayColorHaptic(m_CurrentColorIndex);

        Debug.Log($"[XRPaintInteractor] Cycled to color preset {m_CurrentColorIndex + 1}/{m_LineColorPresets.Length}: {m_LineColor}");
    }

    /// <summary>
    /// Updates the paint point indicator size to match the current line width.
    /// Uses the line width directly as the indicator diameter.
    /// </summary>
    void UpdatePaintPointIndicatorSize()
    {
        if (m_PaintPoint != null)
        {
            m_PaintPoint.indicatorSize = m_LineWidth;
        }
    }
    /// <summary>
    /// Updates the paint point indicator color to match the current line color.
    /// </summary>
    void UpdatePaintPointIndicatorColor()
    {
        if (m_PaintPoint != null)
        {
            m_PaintPoint.indicatorColor = m_LineColor;
        }
    }

    /// <summary>
    /// Checks if painting is currently allowed based on game state and zone restrictions.
    /// </summary>
    bool IsPaintingAllowed()
    {
        // If no game manager, always allow painting (backward compatibility)
        if (m_PaintGameManager == null)
            return true;

        // If all zones are completed, allow free painting anywhere
        if (m_PaintGameManager.allZonesCompleted)
            return true;

        // Determine the active zone (either physical or raycast-detected)
        PaintZone effectiveZone = GetEffectiveZone();

        // Zones are still active - must be inside or targeting a zone to paint
        if (effectiveZone == null)
            return false;

        // Inside a zone - check if color is correct
        return effectiveZone.IsColorCorrect(m_LineColor);
    }

    /// <summary>
    /// Gets the effective paint zone for painting, checking both physical and raycast detection.
    /// </summary>
    PaintZone GetEffectiveZone()
    {
        // Physical zone always takes priority
        if (m_ActivePaintZone != null)
            return m_ActivePaintZone;

        // If raycast mode is enabled, try to find a zone via raycast
        if (m_AllowRaycastZoneDetection && TryGetZoneViaRaycast(out PaintZone raycastZone))
        {
            return raycastZone;
        }

        return null;
    }

    /// <summary>
    /// Updates the raycast hit point and detected zone.
    /// Called each frame when raycast mode is enabled to provide visual feedback.
    /// Caches the hit point for use during painting.
    /// Uses plane intersection for zones with constraint planes for more accurate detection.
    /// </summary>
    void UpdateRaycastHit()
    {
        if (m_PaintPoint == null)
        {
            m_HasCachedRaycastHit = false;
            return;
        }

        Vector3 rayOrigin = m_PaintPoint.transform.position;
        Vector3 rayDirection = m_PaintPoint.transform.TransformDirection(m_RaycastDirection).normalized;

        // Find all paint zones and check for plane intersections
        PaintZone[] allZones = FindObjectsOfType<PaintZone>();
        PaintZone closestZone = null;
        float closestDistance = m_RaycastMaxDistance;
        Vector3 closestHitPoint = Vector3.zero;

        foreach (PaintZone zone in allZones)
        {
            // For zones with constraint planes, do direct plane intersection
            if (zone.constrainToPlane)
            {
                Plane constraintPlane = zone.GetConstraintPlane();
                
                // Check if ray intersects the plane
                if (constraintPlane.Raycast(new Ray(rayOrigin, rayDirection), out float distance))
                {
                    // Check if intersection is within max distance
                    if (distance > 0 && distance <= m_RaycastMaxDistance && distance < closestDistance)
                    {
                        Vector3 planeHitPoint = rayOrigin + rayDirection * distance;
                        
                        // Check if the hit point is actually inside the zone bounds
                        // Use the zone's collider to validate the intersection
                        Collider zoneCollider = zone.GetComponent<Collider>();
                        if (zoneCollider != null)
                        {
                            // Check if the plane intersection point is inside or very close to the collider
                            Vector3 closestPointOnCollider = zoneCollider.ClosestPoint(planeHitPoint);
                            float distanceToCollider = Vector3.Distance(planeHitPoint, closestPointOnCollider);
                            
                            // Use tight tolerance for accurate zone boundary detection (1cm)
                            if (distanceToCollider < 0.01f)
                            {
                                closestZone = zone;
                                closestDistance = distance;
                                closestHitPoint = planeHitPoint;
                            }
                        }
                    }
                }
            }
            else
            {
                // For zones without constraint planes, use traditional collider raycast
                Collider zoneCollider = zone.GetComponent<Collider>();
                if (zoneCollider != null)
                {
                    Ray ray = new Ray(rayOrigin, rayDirection);
                    if (zoneCollider.Raycast(ray, out RaycastHit hit, m_RaycastMaxDistance))
                    {
                        if (hit.distance < closestDistance)
                        {
                            closestZone = zone;
                            closestDistance = hit.distance;
                            closestHitPoint = hit.point;
                        }
                    }
                }
            }
        }

        // If we found a zone
        if (closestZone != null)
        {
            // Update curve sample points for visualization to hit point
            if (m_CurveSamplePointsInitialized)
            {
                m_CurveSamplePoints[0] = rayOrigin;
                m_CurveSamplePoints[1] = closestHitPoint;
            }

            // Only log when zone changes to avoid spam
            if (m_RaycastDetectedZone != closestZone)
            {
                Debug.Log($"[XRPaintInteractor] Raycast detected zone: {closestZone.gameObject.name} (plane intersection)");
            }
            
            m_RaycastDetectedZone = closestZone;
            m_CachedRaycastHitPoint = closestHitPoint;
            m_HasCachedRaycastHit = true;
            return;
        }

        // No valid zone hit
        if (m_RaycastDetectedZone != null)
        {
            Debug.Log("[XRPaintInteractor] Raycast no longer hitting any zone");
        }

        // Update sample points to show short directional line
        if (m_CurveSamplePointsInitialized)
        {
            m_CurveSamplePoints[0] = rayOrigin;
            m_CurveSamplePoints[1] = rayOrigin + rayDirection * m_RestingLineLength;
        }
        
        // No valid hit
        m_RaycastDetectedZone = null;
        m_HasCachedRaycastHit = false;
    }

    /// <summary>
    /// Attempts to find a paint zone by raycasting from the paint point.
    /// Returns true if a zone was hit.
    /// </summary>
    bool TryGetZoneViaRaycast(out PaintZone zone)
    {
        zone = m_RaycastDetectedZone;
        return zone != null;
    }

    /// <summary>
    /// Clears all painted lines that are children of the lines parent.
    /// </summary>
    public void ClearAllLines()
    {
        if (m_LinesParent == null)
        {
            Debug.LogWarning("[XRPaintInteractor] Cannot clear lines - no lines parent assigned.");
            return;
        }

        // End current stroke if painting
        if (m_IsPainting)
        {
            EndPaintStroke();
        }

        // Destroy all child paint lines
        var paintLines = m_LinesParent.GetComponentsInChildren<PaintLine>();
        foreach (var line in paintLines)
        {
            if (Application.isPlaying)
                Destroy(line.gameObject);
            else
                DestroyImmediate(line.gameObject);
        }
    }

    /// <summary>
    /// Implementation of ICurveInteractionDataProvider for visual feedback.
    /// Attempts to determine the end point of the curve for CurveVisualController.
    /// Uses GetPaintPosition() for consistency with where paint is actually placed.
    /// Returns a short line for directional feedback when not pointing at a zone.
    /// </summary>
    public EndPointType TryGetCurveEndPoint(out Vector3 endPoint, bool snapToSelectedAttachIfAvailable = false, bool snapToSnapVolumeIfAvailable = false)
    {
        if (!m_AllowRaycastZoneDetection || m_PaintPoint == null)
        {
            endPoint = Vector3.zero;
            return EndPointType.None;
        }

        // If painting or have a valid raycast hit on a zone, show the actual paint endpoint
        if (m_IsPainting || (m_HasCachedRaycastHit && m_RaycastDetectedZone != null))
        {
            // Use GetPaintPosition() for consistency with actual paint placement
            endPoint = GetPaintPosition();
            
            // Always use ValidCastHit to keep the line visible and show where paint will go
            return EndPointType.ValidCastHit;
        }

        // Show a short directional line when not pointing at a zone
        Vector3 origin = m_PaintPoint.transform.position;
        Vector3 direction = m_PaintPoint.transform.TransformDirection(m_RaycastDirection).normalized;
        endPoint = origin + direction * m_RestingLineLength;
        
        // Use None for the short directional line (no specific target)
        return EndPointType.None;
    }

    /// <summary>
    /// Implementation of ICurveInteractionDataProvider for visual feedback.
    /// Attempts to determine the normal at the endpoint of the curve.
    /// </summary>
    public EndPointType TryGetCurveEndNormal(out Vector3 endNormal, bool snapToSelectedAttachIfAvailable = false)
    {
        if (!m_AllowRaycastZoneDetection || m_PaintPoint == null)
        {
            endNormal = Vector3.up;
            return EndPointType.None;
        }

        // If painting or have a valid raycast hit on a zone
        if (m_IsPainting || (m_HasCachedRaycastHit && m_RaycastDetectedZone != null))
        {
            // Determine which zone to use (active zone while painting, raycast zone otherwise)
            PaintZone zone = m_IsPainting ? m_ActivePaintZone : m_RaycastDetectedZone;
            
            // Use the plane normal if zone has plane constraint
            if (zone != null && zone.constrainToPlane)
            {
                Plane constraintPlane = zone.GetConstraintPlane();
                endNormal = constraintPlane.normal;
            }
            else
            {
                // Default to up vector
                endNormal = Vector3.up;
            }
            
            // Always use ValidCastHit to match TryGetCurveEndPoint
            return EndPointType.ValidCastHit;
        }

        // For short directional line, use forward direction as normal
        endNormal = m_PaintPoint.transform.TransformDirection(m_RaycastDirection).normalized;
        return EndPointType.None;
    }

    void OnDrawGizmosSelected()
    {
        // Visualize the raycast for zone detection
        if (!m_ShowRaycastGizmo || !m_AllowRaycastZoneDetection || m_PaintPoint == null)
            return;

        Vector3 rayOrigin = m_PaintPoint.transform.position;
        Vector3 rayDirection = m_PaintPoint.transform.TransformDirection(m_RaycastDirection).normalized;
        Vector3 rayEnd = rayOrigin + rayDirection * m_RaycastMaxDistance;

        // Find zones using the same logic as UpdateRaycastHit
        PaintZone[] allZones = FindObjectsOfType<PaintZone>();
        PaintZone closestZone = null;
        float closestDistance = m_RaycastMaxDistance;
        Vector3 closestHitPoint = Vector3.zero;

        foreach (PaintZone zone in allZones)
        {
            if (zone.constrainToPlane)
            {
                Plane constraintPlane = zone.GetConstraintPlane();
                if (constraintPlane.Raycast(new Ray(rayOrigin, rayDirection), out float distance))
                {
                    if (distance > 0 && distance <= m_RaycastMaxDistance && distance < closestDistance)
                    {
                        Vector3 planeHitPoint = rayOrigin + rayDirection * distance;
                        Collider zoneCollider = zone.GetComponent<Collider>();
                        if (zoneCollider != null)
                        {
                            Vector3 closestPointOnCollider = zoneCollider.ClosestPoint(planeHitPoint);
                            float distanceToCollider = Vector3.Distance(planeHitPoint, closestPointOnCollider);
                            if (distanceToCollider < 0.1f)
                            {
                                closestZone = zone;
                                closestDistance = distance;
                                closestHitPoint = planeHitPoint;
                            }
                        }
                    }
                }
            }
            else
            {
                Collider zoneCollider = zone.GetComponent<Collider>();
                if (zoneCollider != null)
                {
                    Ray ray = new Ray(rayOrigin, rayDirection);
                    if (zoneCollider.Raycast(ray, out RaycastHit hit, m_RaycastMaxDistance))
                    {
                        if (hit.distance < closestDistance)
                        {
                            closestZone = zone;
                            closestDistance = hit.distance;
                            closestHitPoint = hit.point;
                        }
                    }
                }
            }
        }

        // Draw the ray
        if (closestZone != null)
        {
            // Green - hitting a valid zone
            Gizmos.color = Color.green;
            Gizmos.DrawLine(rayOrigin, closestHitPoint);
            Gizmos.DrawWireSphere(closestHitPoint, 0.02f);
            
            // Draw plane indicator if zone has constraint plane
            if (closestZone.constrainToPlane)
            {
                Plane constraintPlane = closestZone.GetConstraintPlane();
                Gizmos.color = Color.cyan;
                // Draw a small cross on the plane to show orientation
                Vector3 right = Vector3.Cross(constraintPlane.normal, Vector3.up).normalized * 0.05f;
                if (right.sqrMagnitude < 0.01f)
                    right = Vector3.Cross(constraintPlane.normal, Vector3.forward).normalized * 0.05f;
                Vector3 up = Vector3.Cross(right, constraintPlane.normal).normalized * 0.05f;
                Gizmos.DrawLine(closestHitPoint - right, closestHitPoint + right);
                Gizmos.DrawLine(closestHitPoint - up, closestHitPoint + up);
            }
        }
        else
        {
            // Red - not hitting any zone
            Gizmos.color = Color.red;
            Gizmos.DrawLine(rayOrigin, rayEnd);
        }

        // Draw origin point
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(rayOrigin, 0.01f);
    }
}