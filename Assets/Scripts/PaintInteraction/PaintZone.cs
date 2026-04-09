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
    HapticsManager m_HapticsManager;

    [Header("Zone Configuration")]
    [SerializeField]
    [Tooltip("The correct color required to fill this zone.")]
    Color m_CorrectColor = Color.red;

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
    int m_CoverageResolution = 10;

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
    bool m_EnableDebugLogs = true;

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
    Collider m_Collider;
    MeshRenderer m_MeshRenderer;
    Bounds m_ZoneBounds;
    bool m_IsFlashing;
    float m_FlashTimer;
    Color m_OriginalColor;
    Material m_ZoneMaterial;

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
        if (m_EnableDebugLogs)
            Debug.Log($"[PaintZone] OnTriggerEnter triggered by {other.gameObject.name} | Layer: {LayerMask.LayerToName(other.gameObject.layer)} ({other.gameObject.layer}) | Tag: {other.gameObject.tag}");

        // Check for controller entry (for haptics)
        if ( other.gameObject.CompareTag("Controller") )
        {
            if (m_EnableDebugLogs)
                Debug.Log($"[PaintZone] Controller detected entering zone: {other.gameObject.name}");
            m_OnControllerEntered?.Invoke();
        }

        // Check if the colliding object has a paint line reference
        PaintLineColliderReference reference = other.GetComponent<PaintLineColliderReference>();
        PaintLine paintLine = reference != null ? reference.paintLine : other.GetComponent<PaintLine>();
        
        if (paintLine == null)
        {
            if (m_EnableDebugLogs)
                Debug.Log($"[PaintZone] No PaintLine found on {other.gameObject.name} | Has reference component: {reference != null}");
            return;
        }

        // Process the paint line
        ProcessPaintLine(paintLine);
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

        if (m_EnableDebugLogs)
            Debug.Log($"[PaintZone] ProcessPaintLine: {paintLine.gameObject.name}, Color: {paintLine.lineColor} | IsInitialCheck: {isInitialCheck}");

        // For initial checks, prevent duplicate flashing by checking if already detected
        // For finalization, prevent duplicate coverage calculations
        if (isInitialCheck)
        {
            // Check if we've already done the initial check (prevents duplicate flashing)
            if (m_AllDetectedLines.Contains(paintLine))
            {
                if (m_EnableDebugLogs)
                    Debug.Log($"[PaintZone] Already did initial check for this line, skipping");
                return;
            }
            
            // Mark as detected for initial check
            m_AllDetectedLines.Add(paintLine);
        }
        else
        {
            // For finalization, check if this line is already being tracked
            // If it's in m_TrackedLines, we've already finalized it
            if (m_TrackedLines.Contains(paintLine))
            {
                if (m_EnableDebugLogs)
                    Debug.Log($"[PaintZone] Line already finalized, skipping coverage recalculation");
                return;
            }
        }

        // Check if the line color matches the correct color
        bool isCorrectColor = ColorsMatch(paintLine.lineColor, m_CorrectColor, m_ColorTolerance);

        if (m_EnableDebugLogs)
            Debug.Log($"[PaintZone] Color match result: {isCorrectColor} | Line: {paintLine.lineColor} | Required: {m_CorrectColor} | Tolerance: {m_ColorTolerance}");

        if (!isCorrectColor)
        {
            // Incorrect color detected - flash but don't track for coverage
            // Only flash on initial check, not on finalization
            if (isInitialCheck)
            {
                OnPaintWithIncorrectColor(paintLine);
            }
        }
        else
        {
            // Correct color
            // Only recalculate coverage on finalization, not on initial detection (performance optimization)
            if (!isInitialCheck)
            {
                // Add to tracking on finalization only
                if (!m_TrackedLines.Contains(paintLine))
                {
                    m_TrackedLines.Add(paintLine);
                    if (m_EnableDebugLogs)
                        Debug.Log($"[PaintZone] Added line to tracking. Total tracked: {m_TrackedLines.Count}");
                }
                
                if (m_EnableDebugLogs)
                    Debug.Log($"[PaintZone] Recalculating coverage (line finalized)");
                RecalculateCoverage();
            }
            else
            {
                if (m_EnableDebugLogs)
                    Debug.Log($"[PaintZone] Correct color on initial check - will track and calculate coverage on finalization");
            }
        }
    }

    /// <summary>
    /// Recalculates the coverage amount based on all tracked paint lines.
    /// Uses a voxel-based sampling approach to estimate volume coverage.
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
    /// Calculates the shortest distance from a point to a line segment.
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
    /// Compares two colors with a tolerance value.
    /// </summary>
    bool ColorsMatch(Color a, Color b, float tolerance)
    {
        float rDiff = Mathf.Abs(a.r - b.r);
        float gDiff = Mathf.Abs(a.g - b.g);
        float bDiff = Mathf.Abs(a.b - b.b);

        // Average difference across channels
        float averageDiff = (rDiff + gDiff + bDiff) / 3f;

        if (m_EnableDebugLogs)
            Debug.Log($"[PaintZone] ColorMatch | R:{rDiff:F3} G:{gDiff:F3} B:{bDiff:F3} | Avg:{averageDiff:F3} | Tolerance:{tolerance:F3} | Match:{averageDiff <= tolerance}");

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

        // Trigger haptics on the specific controller that painted
        if (paintLine != null && paintLine.creator != null)
        {
            // Get the HapticsManager from the controller that created this paint line
            HapticsManager controllerHaptics = paintLine.creator.transform.parent.transform.parent.GetComponentInChildren<HapticsManager>();
            if (controllerHaptics != null)
            {
                controllerHaptics.WrongColorHaptic();
                if (m_EnableDebugLogs)
                    Debug.Log($"[PaintZone] Triggered wrong color haptics on controller: {paintLine.creator.gameObject.name}");
            }
            else if (m_EnableDebugLogs)
            {
                Debug.LogWarning($"[PaintZone] No HapticsManager found on controller {paintLine.creator.gameObject.name}");
            }
        }
        else
        {
            // Fallback to zone's HapticsManager if paint line doesn't have creator info
            if (m_HapticsManager != null)
            {
                m_HapticsManager.WrongColorHaptic();
                if (m_EnableDebugLogs)
                    Debug.Log($"[PaintZone] Using fallback HapticsManager (paint line had no creator reference)");
            }
            else if (m_EnableDebugLogs)
            {
                Debug.LogWarning($"[PaintZone] No HapticsManager available for haptic feedback");
            }
        }

        Debug.Log($"[PaintZone] Incorrect color detected in zone {gameObject.name}");
    }

    /// <summary>
    /// Called when the zone reaches completion threshold.
    /// </summary>
    void OnZoneCompleted()
    {
        m_Completed = true;

        // Hide mesh if configured
        if (m_HideWhenComplete && m_MeshRenderer != null)
        {
            m_MeshRenderer.enabled = false;
        }

        // Invoke event
        m_OnCompleted?.Invoke();

        Debug.Log($"[PaintZone] Zone {gameObject.name} completed! Coverage: {m_CompletionAmount:P1}");
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
    }
}
