using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Defines a 3D zone that detects painted lines entering it,
/// validates their color, and tracks completion progress.
/// Attach to a GameObject with a MeshRenderer and Collider (set as Trigger).
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(MeshRenderer))]
public class PaintZone : MonoBehaviour
{
    [SerializeField]
    PaintGameManager m_GameManager;
    HapticsManager m_HapticsManager;

    [Header("Zone Configuration")]
    [SerializeField]
    [Tooltip("The correct color required to fill this zone.")]
    Color m_CorrectColor = Color.red;

    [SerializeField]
    [Tooltip("Color index to play for haptics feedback when controller enters the zone.")]
    int m_HapticsColorIndex = 0;

    [SerializeField]
    [Tooltip("Color comparison tolerance (0-1). Lower = more strict matching.")]
    [Range(0f, 0.5f)]
    float m_ColorTolerance = 0.15f;

    [SerializeField]
    [Tooltip("Percentage of zone volume that must be filled to complete (0-1).")]
    [Range(0f, 1f)]
    float m_CompletionThreshold = 0.3f;

    [SerializeField]
    [Tooltip("Resolution for coverage calculation. Higher = more accurate but slower.")]
    [Range(5, 50)]
    int m_CoverageResolution = 6;

    [Header("Plane Constraint")]
    [SerializeField]
    [Tooltip("If true, paint lines created in this zone will be constrained to a specific plane.")]
    bool m_ConstrainToPlane = false;

    [SerializeField]
    [Tooltip("The axis of the zone's transform to use as the plane normal (Forward=Z, Up=Y, Right=X).")]
    PlaneAxis m_PlaneNormalAxis = PlaneAxis.Forward;

    [SerializeField]
    [Tooltip("Offset distance along the plane normal from the zone's center.")]
    float m_PlaneOffset = 0f;

    public enum PlaneAxis { Forward, Up, Right }

    [Header("Visual Feedback")]
    [SerializeField]
    [Tooltip("Hide the zone's mesh renderer when completed.")]
    bool m_HideWhenComplete = true;

    [SerializeField]
    [Tooltip("Color to flash zone to when painting with incorrect color.")]
    Color m_IncorrectColorFlashColor = new Color(1f, 0f, 0f, 0.5f);

    [SerializeField]
    [Tooltip("Duration of incorrect color flash effect.")]
    float m_FlashDuration = 0.2f;

    [Header("Debug")]
    [SerializeField]
    [Tooltip("Enable detailed debug logging for paint zone activity.")] 
    bool m_EnableDebugLogs = false;

    [Header("Events")]
    [SerializeField]
    UnityEvent m_OnCompleted = new();

    [SerializeField]
    UnityEvent m_OnIncorrectColorDetected = new();

    [SerializeField]
    [Tooltip("Event invoked when a controller enters the zone (for haptics feedback).")]
    UnityEvent m_OnControllerEntered = new();

    // State tracking
    float m_CompletionAmount = 0.0f;
    bool m_Completed = false;
    HashSet<PaintLine> m_TrackedLines = new();
    HashSet<PaintLine> m_AllDetectedLines = new(); // Track all lines to prevent duplicate processing
    HashSet<XRPaintInteractor> m_ControllersInZone = new(); // Track controllers currently in the zone
    Dictionary<GameObject, (XRPaintInteractor interactor, HapticsManager haptics)> m_ControllerCache = new(); // Cache controller components
    Collider m_Collider;
    MeshRenderer m_MeshRenderer;
    Bounds m_ZoneBounds;
    bool m_IsFlashing;
    float m_FlashTimer;
    Color m_OriginalColor;
    Material m_ZoneMaterial;
    bool m_CoverageDirty = false;
    Coroutine m_CoverageCalculationCoroutine;
    float m_LastCoverageCalculationTime = 0f;
    int m_LineCountAtLastCalculation = 0;

    // Cached line data for performance
    struct CachedLineData
    {
        public Vector3[] points;
        public float width;
    }
    List<CachedLineData> m_CachedLineData = new();

    /// <summary>
    /// The correct color required to complete this zone.
    /// </summary>
    public Color correctColor
    {
        get => m_CorrectColor;
        set => m_CorrectColor = value;
    }

    /// <summary>
    /// Completion threshold as a percentage (0-1).
    /// </summary>
    public float completionThreshold
    {
        get => m_CompletionThreshold;
        set => m_CompletionThreshold = Mathf.Clamp01(value);
    }

    /// <summary>
    /// Current completion amount as a percentage (0-1).
    /// </summary>
    public float completionAmount => m_CompletionAmount;

    /// <summary>
    /// Whether this zone has been completed.
    /// </summary>
    public bool completed => m_Completed;

    /// <summary>
    /// Event invoked when the zone is completed.
    /// </summary>
    public UnityEvent onCompleted => m_OnCompleted;

    /// <summary>
    /// Event invoked when incorrect color painting is detected.
    /// </summary>
    public UnityEvent onIncorrectColorDetected => m_OnIncorrectColorDetected;

    /// <summary>
    /// Event invoked when a controller enters the zone (for haptics).
    /// </summary>
    public UnityEvent onControllerEntered => m_OnControllerEntered;

    /// <summary>
    /// Whether painting should be constrained to a plane in this zone.
    /// </summary>
    public bool constrainToPlane => m_ConstrainToPlane;

    /// <summary>
    /// Gets the constraint plane for this zone.
    /// Uses local transform axes (respects object rotation).
    /// </summary>
    public Plane GetConstraintPlane()
    {
        // Get the normal in world space based on local transform axis
        Vector3 normal = m_PlaneNormalAxis switch
        {
            PlaneAxis.Forward => transform.forward,  // Local Z axis in world space
            PlaneAxis.Up => transform.up,            // Local Y axis in world space
            PlaneAxis.Right => transform.right,      // Local X axis in world space
            _ => transform.forward
        };

        Vector3 pointOnPlane = transform.position + normal * m_PlaneOffset;
        return new Plane(normal, pointOnPlane);
    }

    void Awake()
    {
        m_Collider = GetComponent<Collider>();
        m_MeshRenderer = GetComponent<MeshRenderer>();

        if (m_Collider == null)
        {
            Debug.LogError($"[PaintZone] No Collider found on {gameObject.name}. PaintZone requires a Collider component.", this);
            enabled = false;
            return;
        }

        // Ensure collider is a trigger
        if (!m_Collider.isTrigger)
        {
            Debug.LogWarning($"[PaintZone] Collider on {gameObject.name} is not set as trigger. Setting isTrigger = true.", this);
            m_Collider.isTrigger = true;
        }

        // Cache the zone bounds
        m_ZoneBounds = m_Collider.bounds;

        // Store original material color
        if (m_MeshRenderer != null && m_MeshRenderer.material != null)
        {
            m_ZoneMaterial = m_MeshRenderer.material;
            m_OriginalColor = m_ZoneMaterial.color;
            
            if (m_EnableDebugLogs)
                Debug.Log($"[PaintZone] Initialized on {gameObject.name} | Layer: {LayerMask.LayerToName(gameObject.layer)} ({gameObject.layer}) | Original Color: {m_OriginalColor} | Required Color: {m_CorrectColor} | Collider is trigger: {m_Collider.isTrigger}");
        }
        else
        {
            if (m_EnableDebugLogs)
                Debug.LogWarning($"[PaintZone] MeshRenderer or material is null on {gameObject.name}!");
        }
    }

    void Update()
    {
        // Handle flash effect
        if (m_IsFlashing)
        {
            m_FlashTimer -= Time.deltaTime;
            if (m_FlashTimer <= 0f)
            {
                m_IsFlashing = false;
                if (m_ZoneMaterial != null)
                {
                    m_ZoneMaterial.color = m_OriginalColor;
                }
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Check for paint nib entry (for haptics and plane constraint)
        if (other.gameObject.CompareTag("PaintNib"))
        {
            // Get or cache controller components
            if (!m_ControllerCache.TryGetValue(other.gameObject, out var controllerData))
            {
                var interactor = other.gameObject.transform.parent.transform.parent.GetComponentInChildren<XRPaintInteractor>();
                var haptics = other.gameObject.transform.parent.transform.parent.GetComponentInChildren<HapticsManager>();
                controllerData = (interactor, haptics);
                m_ControllerCache[other.gameObject] = controllerData;
            }

            // Play haptic feedback
            controllerData.haptics?.PlayColorHaptic(m_HapticsColorIndex);

            // Track interactor
            if (controllerData.interactor != null)
            {
                m_ControllersInZone.Add(controllerData.interactor);

                // Set plane constraint if enabled
                if (m_ConstrainToPlane)
                {
                    controllerData.interactor.SetActivePaintZone(this);
                }
            }

            m_OnControllerEntered?.Invoke();
        }

        // Check if the colliding object has a paint line reference
        PaintLineColliderReference reference = other.GetComponent<PaintLineColliderReference>();
        if (reference != null && reference.paintLine != null)
        {
            ProcessPaintLine(reference.paintLine);
        }
    }

    void OnTriggerExit(Collider other)
    {
        // Handle paint nib exiting the zone - must match the tag checked in OnTriggerEnter
        if (other.gameObject.CompareTag("PaintNib"))
        {
            // Use cached components if available
            if (m_ControllerCache.TryGetValue(other.gameObject, out var controllerData) && controllerData.interactor != null)
            {
                // Remove from tracking set
                m_ControllersInZone.Remove(controllerData.interactor);

                // End any active paint stroke
                if (controllerData.interactor.isPainting)
                {
                    controllerData.interactor.EndPaintStroke();
                }

                // Clear plane constraint
                if (m_ConstrainToPlane)
                {
                    controllerData.interactor.ClearActivePaintZone(this);
                }
            }
        }
    }

    /// <summary>
    /// Called manually by PaintLine when it detects it was created inside this zone.
    /// This is necessary because OnTriggerEnter only fires when colliders move into triggers,
    /// not when they're spawned inside.
    /// </summary>
    /// <param name="paintLine">The paint line to process.</param>
    /// <param name="isInitialCheck">True if this is called on first point (for immediate feedback), false if called on finalization.</param>
    public void OnPaintLineCreated(PaintLine paintLine, bool isInitialCheck = false)
    {
        if (paintLine == null)
            return;

        if (m_EnableDebugLogs)
            Debug.Log($"[PaintZone] OnPaintLineCreated called manually for {paintLine.gameObject.name} | IsInitialCheck: {isInitialCheck}");

        ProcessPaintLine(paintLine, isInitialCheck);
    }

    /// <summary>
    /// Processes a paint line, checking its color and adding it to tracking if appropriate.
    /// </summary>
    /// <param name="isInitialCheck">If true, this is the first point check (skip expensive coverage calc).</param>
    void ProcessPaintLine(PaintLine paintLine, bool isInitialCheck = false)
    {
        if (paintLine == null)
            return;

        // For initial checks, prevent duplicate flashing by checking if already detected
        // For finalization, prevent duplicate coverage calculations
        if (isInitialCheck)
        {
            // Check if we've already done the initial check (prevents duplicate flashing)
            if (m_AllDetectedLines.Contains(paintLine))
                return;
            
            // Mark as detected for initial check
            m_AllDetectedLines.Add(paintLine);
        }
        else
        {
            // For finalization, check if this line is already being tracked
            if (m_TrackedLines.Contains(paintLine))
                return;
        }

        // Check if the line color matches the correct color
        bool isCorrectColor = ColorsMatch(paintLine.lineColor, m_CorrectColor, m_ColorTolerance);

        if (!isCorrectColor)
        {
            // Incorrect color detected - flash but don't track for coverage
            if (isInitialCheck)
            {
                OnPaintWithIncorrectColor(paintLine);
            }
        }
        else
        {
            // Correct color - only recalculate coverage on finalization
            if (!isInitialCheck)
            {
                // Add to tracking on finalization only
                m_TrackedLines.Add(paintLine);
                
                // Mark coverage as dirty and schedule deferred recalculation
                // Only recalculate if we've added 3+ lines or 1 second has passed
                m_CoverageDirty = true;
                int linesSinceLastCalc = m_TrackedLines.Count - m_LineCountAtLastCalculation;
                float timeSinceLastCalc = Time.time - m_LastCoverageCalculationTime;
                
                if (linesSinceLastCalc >= 3 || timeSinceLastCalc >= 1f)
                {
                    ScheduleCoverageCalculation();
                }
            }
        }
    }

    /// <summary>
    /// Schedules a deferred coverage calculation to avoid blocking the main thread.
    /// </summary>
    void ScheduleCoverageCalculation()
    {
        // Cancel any existing calculation
        if (m_CoverageCalculationCoroutine != null)
        {
            StopCoroutine(m_CoverageCalculationCoroutine);
        }

        // Start new deferred calculation
        m_CoverageCalculationCoroutine = StartCoroutine(DeferredCoverageCalculation());
    }

    /// <summary>
    /// Coroutine that defers coverage calculation to the next frame to avoid lag spikes.
    /// </summary>
    System.Collections.IEnumerator DeferredCoverageCalculation()
    {
        // Wait one frame to allow multiple lines to be added before recalculating
        yield return null;

        if (m_CoverageDirty)
        {
            yield return StartCoroutine(RecalculateCoverageAsync());
            m_CoverageDirty = false;
            m_LastCoverageCalculationTime = Time.time;
            m_LineCountAtLastCalculation = m_TrackedLines.Count;
        }

        m_CoverageCalculationCoroutine = null;
    }

    /// <summary>
    /// Recalculates the coverage amount based on all tracked paint lines.
    /// Uses a voxel-based sampling approach to estimate volume coverage.
    /// Async version spreads work over multiple frames.
    /// </summary>
    System.Collections.IEnumerator RecalculateCoverageAsync()
    {
        if (m_Completed)
            yield break;

        // Cache line data once at the start
        m_CachedLineData.Clear();
        foreach (var paintLine in m_TrackedLines)
        {
            if (paintLine == null)
                continue;

            Vector3[] points = paintLine.GetPoints();
            if (points == null || points.Length == 0)
                continue;

            float width = paintLine.lineRenderer != null ? paintLine.lineRenderer.startWidth : 0.01f;
            m_CachedLineData.Add(new CachedLineData { points = points, width = width });
        }

        // Use voxel grid sampling to estimate coverage
        int samplesPerAxis = m_CoverageResolution;
        int coveredSamples = 0;
        int validSamples = 0;

        Vector3 boundsMin = m_ZoneBounds.min;
        Vector3 boundsSize = m_ZoneBounds.size;
        float stepSize = 1f / samplesPerAxis;

        int sampleCount = 0;
        const int samplesPerFrame = 100; // Process 100 samples per frame

        // Sample points throughout the volume
        for (int x = 0; x < samplesPerAxis; x++)
        {
            for (int y = 0; y < samplesPerAxis; y++)
            {
                for (int z = 0; z < samplesPerAxis; z++)
                {
                    // Calculate sample point position
                    Vector3 samplePoint = boundsMin + new Vector3(
                        (x + 0.5f) * stepSize * boundsSize.x,
                        (y + 0.5f) * stepSize * boundsSize.y,
                        (z + 0.5f) * stepSize * boundsSize.z
                    );

                    // Check if sample point is actually inside the collider
                    if (!IsPointInsideCollider(samplePoint))
                        continue;

                    validSamples++;

                    // Check if any tracked line has a point near this sample
                    if (IsPointCoveredByPaintCached(samplePoint))
                    {
                        coveredSamples++;
                    }

                    // Yield every N samples to spread work across frames
                    sampleCount++;
                    if (sampleCount >= samplesPerFrame)
                    {
                        sampleCount = 0;
                        yield return null;
                    }
                }
            }
        }

        // Calculate completion percentage
        m_CompletionAmount = validSamples > 0 ? (float)coveredSamples / validSamples : 0f;

        // Check if threshold reached
        if (m_CompletionAmount >= m_CompletionThreshold && !m_Completed)
        {
            OnZoneCompleted();
        }
    }

    /// <summary>
    /// Recalculates the coverage amount based on all tracked paint lines.
    /// Uses a voxel-based sampling approach to estimate volume coverage.
    /// Legacy synchronous version - prefer RecalculateCoverageAsync.
    /// </summary>
    void RecalculateCoverage()
    {
        if (m_Completed)
            return;

        if (m_EnableDebugLogs)
        {
            Debug.Log($"[PaintZone] === Starting Coverage Calculation ===");
            Debug.Log($"[PaintZone] Zone bounds: Min={m_ZoneBounds.min}, Max={m_ZoneBounds.max}, Size={m_ZoneBounds.size}");
            Debug.Log($"[PaintZone] Tracked lines: {m_TrackedLines.Count}");
            int lineIndex = 0;
            foreach (var line in m_TrackedLines)
            {
                if (line != null)
                {
                    int pointCount = line.GetPoints()?.Length ?? 0;
                    float lineWidth = line.lineRenderer != null ? line.lineRenderer.startWidth : 0.01f;
                    Debug.Log($"[PaintZone]   Line {lineIndex}: {line.gameObject.name} | Points: {pointCount} | Width: {lineWidth:F5}");
                }
                lineIndex++;
            }
        }

        // Use voxel grid sampling to estimate coverage
        int samplesPerAxis = m_CoverageResolution;
        int coveredSamples = 0;
        int validSamples = 0; // Only count samples actually inside the collider

        Vector3 boundsMin = m_ZoneBounds.min;
        Vector3 boundsSize = m_ZoneBounds.size;
        float stepSize = 1f / samplesPerAxis;

        // Sample points throughout the volume
        for (int x = 0; x < samplesPerAxis; x++)
        {
            for (int y = 0; y < samplesPerAxis; y++)
            {
                for (int z = 0; z < samplesPerAxis; z++)
                {
                    // Calculate sample point position
                    Vector3 samplePoint = boundsMin + new Vector3(
                        (x + 0.5f) * stepSize * boundsSize.x,
                        (y + 0.5f) * stepSize * boundsSize.y,
                        (z + 0.5f) * stepSize * boundsSize.z
                    );

                    // Check if sample point is actually inside the collider
                    // (Important for complex mesh colliders where bounds != actual volume)
                    if (!IsPointInsideCollider(samplePoint))
                        continue;

                    validSamples++;

                    // Check if any tracked line has a point near this sample
                    if (IsPointCoveredByPaint(samplePoint))
                    {
                        coveredSamples++;
                    }
                }
            }
        }

        // Calculate completion percentage based on valid samples only
        m_CompletionAmount = validSamples > 0 ? (float)coveredSamples / validSamples : 0f;

        if (m_EnableDebugLogs)
        {
            int totalSamples = samplesPerAxis * samplesPerAxis * samplesPerAxis;
            float samplingEfficiency = totalSamples > 0 ? (float)validSamples / totalSamples : 0f;
            Debug.Log($"[PaintZone] Coverage calculated: {coveredSamples}/{validSamples} valid samples covered = {m_CompletionAmount:P1} | Threshold: {m_CompletionThreshold:P1} | Tracked lines: {m_TrackedLines.Count}");
            if (samplingEfficiency < 0.9f)
                Debug.Log($"[PaintZone] Complex mesh detected: {validSamples}/{totalSamples} samples inside collider ({samplingEfficiency:P0})");
        }

        // Check if threshold reached
        if (m_CompletionAmount >= m_CompletionThreshold && !m_Completed)
        {
            if (m_EnableDebugLogs)
                Debug.Log($"[PaintZone] Threshold reached! Completing zone.");
            OnZoneCompleted();
        }
        else if (m_EnableDebugLogs && m_CompletionAmount < m_CompletionThreshold)
        {
            Debug.Log($"[PaintZone] Not yet complete. Need {(m_CompletionThreshold - m_CompletionAmount):P1} more coverage.");
        }
    }

    /// <summary>
    /// Checks if a point in space is covered by any tracked paint line.
    /// Uses cached line data for better performance.
    /// </summary>
    bool IsPointCoveredByPaintCached(Vector3 point)
    {
        foreach (var lineData in m_CachedLineData)
        {
            float widthSquared = lineData.width * lineData.width;

            // Check distance to any line segment
            for (int i = 0; i < lineData.points.Length - 1; i++)
            {
                Vector3 segmentStart = lineData.points[i];
                Vector3 segmentEnd = lineData.points[i + 1];

                // Calculate closest point on line segment (squared distance)
                float distanceSquared = DistanceToLineSegmentSquared(point, segmentStart, segmentEnd);

                // If within the line width, consider it covered
                if (distanceSquared < widthSquared)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a point in space is covered by any tracked paint line.
    /// Legacy version - prefer IsPointCoveredByPaintCached.
    /// </summary>
    bool IsPointCoveredByPaint(Vector3 point)
    {
        foreach (var paintLine in m_TrackedLines)
        {
            if (paintLine == null)
                continue;

            Vector3[] linePoints = paintLine.GetPoints();
            if (linePoints == null || linePoints.Length == 0)
                continue;

            // Check distance to any line segment
            for (int i = 0; i < linePoints.Length - 1; i++)
            {
                Vector3 segmentStart = linePoints[i];
                Vector3 segmentEnd = linePoints[i + 1];

                // Calculate closest point on line segment
                float distance = DistanceToLineSegment(point, segmentStart, segmentEnd);

                // If within the line width, consider it covered
                // Use line width from renderer if available
                float lineWidth = paintLine.lineRenderer != null ? paintLine.lineRenderer.startWidth : 0.01f;
                if (distance < lineWidth)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a point is inside the trigger collider.
    /// This is crucial for complex mesh colliders where bounds != actual volume.
    /// </summary>
    bool IsPointInsideCollider(Vector3 point)
    {
        if (m_Collider == null)
            return false;

        // Use ClosestPoint to determine if point is inside collider
        // If the closest point on the collider equals the sample point, the point is inside
        Vector3 closestPoint = m_Collider.ClosestPoint(point);
        
        // Small epsilon for floating point comparison
        return Vector3.Distance(closestPoint, point) < 0.001f;
    }

    /// <summary>
    /// Calculates the shortest SQUARED distance from a point to a line segment.
    /// Avoids expensive sqrt operation.
    /// </summary>
    float DistanceToLineSegmentSquared(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector3 line = lineEnd - lineStart;
        float lineLengthSquared = line.sqrMagnitude;

        if (lineLengthSquared < 0.00000001f)
            return (point - lineStart).sqrMagnitude;

        // Project point onto line
        float t = Mathf.Clamp01(Vector3.Dot(point - lineStart, line) / lineLengthSquared);
        Vector3 projection = lineStart + t * line;

        return (point - projection).sqrMagnitude;
    }

    /// <summary>
    /// Calculates the shortest distance from a point to a line segment.
    /// Legacy version - prefer DistanceToLineSegmentSquared for better performance.
    /// </summary>
    float DistanceToLineSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector3 line = lineEnd - lineStart;
        float lineLength = line.magnitude;

        if (lineLength < 0.0001f)
            return Vector3.Distance(point, lineStart);

        // Project point onto line
        float t = Mathf.Clamp01(Vector3.Dot(point - lineStart, line) / (lineLength * lineLength));
        Vector3 projection = lineStart + t * line;

        return Vector3.Distance(point, projection);
    }

    /// <summary>
    /// Checks if the provided color matches the zone's required color within tolerance.
    /// Public method for external validation (e.g., paint interactor checking before creating lines).
    /// </summary>
    public bool IsColorCorrect(Color color)
    {
        return ColorsMatch(color, m_CorrectColor, m_ColorTolerance);
    }

    /// <summary>
    /// Compares two colors with a tolerance value.
    /// </summary>
    bool ColorsMatch(Color a, Color b, float tolerance)
    {
        float rDiff = Mathf.Abs(a.r - b.r);
        float gDiff = Mathf.Abs(a.g - b.g);
        float bDiff = Mathf.Abs(a.b - b.b);

        // Average difference across channels
        float averageDiff = (rDiff + gDiff + bDiff) / 3f;

        return averageDiff <= tolerance;
    }

    /// <summary>
    /// Called when painting with incorrect color is detected.
    /// </summary>
    /// <param name="paintLine">The paint line with incorrect color (used to determine which controller to send haptics to).</param>
    void OnPaintWithIncorrectColor(PaintLine paintLine = null)
    {
        if (m_EnableDebugLogs)
            Debug.Log($"[PaintZone] OnPaintWithIncorrectColor called for zone {gameObject.name}");

        // Flash the zone visual
        if (m_ZoneMaterial != null)
        {
            if (m_EnableDebugLogs)
                Debug.Log($"[PaintZone] Setting flash color from {m_ZoneMaterial.color} to {m_IncorrectColorFlashColor}");
            
            m_ZoneMaterial.color = m_IncorrectColorFlashColor;
            m_IsFlashing = true;
            m_FlashTimer = m_FlashDuration;
        }
        else
        {
            if (m_EnableDebugLogs)
                Debug.LogWarning($"[PaintZone] Cannot flash - m_ZoneMaterial is null!");
        }

        // Invoke event
        m_OnIncorrectColorDetected?.Invoke();

        // Trigger haptics - use cached data if available
        HapticsManager hapticsToUse = null;
        
        if (paintLine != null && paintLine.creator != null)
        {
            // Try to find cached haptics manager for this controller
            foreach (var cached in m_ControllerCache.Values)
            {
                if (cached.interactor == paintLine.creator)
                {
                    hapticsToUse = cached.haptics;
                    break;
                }
            }
        }

        // Trigger the haptic feedback
        hapticsToUse?.WrongColorHaptic();
    }

    /// <summary>
    /// Called when the zone reaches completion threshold.
    /// </summary>
    void OnZoneCompleted()
    {
        m_Completed = true;

        // End all active paint strokes in this zone
        foreach (var interactor in m_ControllersInZone)
        {
            if (interactor != null && interactor.isPainting)
            {
                interactor.EndPaintStroke();
                if (m_EnableDebugLogs)
                    Debug.Log($"[PaintZone] Ended paint stroke on zone completion for: {interactor.gameObject.name}");
            }
        }

        // Hide mesh if configured
        if (m_HideWhenComplete && m_MeshRenderer != null)
        {
            m_MeshRenderer.enabled = false;
        }

        // Activate next zone
        m_GameManager.AddNextPaintZone();

        // Invoke event
        m_OnCompleted?.Invoke();

        if (m_EnableDebugLogs)
            Debug.Log($"[PaintZone] Zone {gameObject.name} completed! Coverage: {m_CompletionAmount:P1}");

        // Deactivate after a frame to ensure all cleanup completes
        StartCoroutine(DeactivateAfterCompletion());
    }

    /// <summary>
    /// Deactivates the zone GameObject after a short delay to ensure all operations complete.
    /// </summary>
    System.Collections.IEnumerator DeactivateAfterCompletion()
    {
        yield return null; // Wait one frame
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Force recalculation of coverage. Useful for debugging.
    /// </summary>
    public void ForceRecalculateCoverage()
    {
        RecalculateCoverage();
    }

    /// <summary>
    /// Resets the zone to its initial state.
    /// </summary>
    public void ResetZone()
    {
        m_Completed = false;
        m_CompletionAmount = 0f;
        m_TrackedLines.Clear();
        m_AllDetectedLines.Clear();
        m_ControllersInZone.Clear();
        m_ControllerCache.Clear();
        m_CoverageDirty = false;
        m_CachedLineData.Clear();
        m_LastCoverageCalculationTime = 0f;
        m_LineCountAtLastCalculation = 0;

        if (m_CoverageCalculationCoroutine != null)
        {
            StopCoroutine(m_CoverageCalculationCoroutine);
            m_CoverageCalculationCoroutine = null;
        }

        if (m_MeshRenderer != null)
        {
            m_MeshRenderer.enabled = true;
        }

        if (m_ZoneMaterial != null)
        {
            m_ZoneMaterial.color = m_OriginalColor;
        }

        m_IsFlashing = false;
    }

    void OnDrawGizmosSelected()
    {
        // Visualize the zone bounds
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = m_Completed ? Color.green : m_CorrectColor;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
            
            // Show completion percentage as text
            #if UNITY_EDITOR
            var style = new UnityEngine.GUIStyle();
            style.normal.textColor = m_Completed ? Color.green : Color.white;
            style.fontSize = 14;
            string label = m_Completed ? "COMPLETED" : $"Coverage: {m_CompletionAmount:P1}\nThreshold: {m_CompletionThreshold:P1}\nLines: {m_TrackedLines.Count}";
            UnityEditor.Handles.Label(col.bounds.center + Vector3.up * (col.bounds.size.y * 0.5f + 0.1f), label, style);
            #endif
        }

        // Visualize the constraint plane if enabled
        if (m_ConstrainToPlane)
        {
            DrawConstraintPlaneGizmo();
        }
    }

    void DrawConstraintPlaneGizmo()
    {
        #if UNITY_EDITOR
        Collider col = GetComponent<Collider>();
        Plane plane = GetConstraintPlane();
        Vector3 center = plane.ClosestPointOnPlane(transform.position);
        
        // Draw a grid on the plane
        Gizmos.color = new Color(0f, 1f, 1f, 0.8f); // Cyan, more opaque for visibility
        
        // Get normal based on local axis (in world space)
        Vector3 normal = m_PlaneNormalAxis switch
        {
            PlaneAxis.Forward => transform.forward,  // Local Z
            PlaneAxis.Up => transform.up,            // Local Y
            PlaneAxis.Right => transform.right,      // Local X
            _ => transform.forward
        };
        
        // Get two perpendicular axes on the plane
        Vector3 right = Vector3.Cross(normal, Vector3.up);
        if (right.sqrMagnitude < 0.001f) // If normal is parallel to up, use forward instead
            right = Vector3.Cross(normal, Vector3.forward);
        right = right.normalized;
        
        Vector3 up = Vector3.Cross(normal, right).normalized;
        
        // Use bounds size for plane visualization size
        float size = col != null ? Mathf.Max(col.bounds.size.x, col.bounds.size.y, col.bounds.size.z) : 1f;
        
        // Draw plane grid
        int gridLines = 8;
        float step = size / gridLines;
        for (int i = -gridLines; i <= gridLines; i++)
        {
            // Lines along 'right' direction
            Vector3 start = center + up * (i * step) - right * size;
            Vector3 end = center + up * (i * step) + right * size;
            Gizmos.DrawLine(start, end);
            
            // Lines along 'up' direction
            start = center + right * (i * step) - up * size;
            end = center + right * (i * step) + up * size;
            Gizmos.DrawLine(start, end);
        }
        
        // Draw normal arrow
        UnityEditor.Handles.color = Color.cyan;
        UnityEditor.Handles.ArrowHandleCap(0, center, Quaternion.LookRotation(normal), size * 0.3f, EventType.Repaint);
        
        // Draw label for plane
        var planeStyle = new UnityEngine.GUIStyle();
        planeStyle.normal.textColor = Color.cyan;
        planeStyle.fontSize = 12;
        UnityEditor.Handles.Label(center + normal * (size * 0.35f), $"Constraint Plane\n{m_PlaneNormalAxis} axis (Local)", planeStyle);
        #endif
    }
}
