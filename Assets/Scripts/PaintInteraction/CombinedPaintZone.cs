using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Coordinates multiple PaintZone components to create a complex multi-mesh paint zone.
/// Each child zone must be filled independently to complete the combined zone.
/// Attach to a parent GameObject with child GameObjects that have PaintZone components.
/// </summary>
public class CombinedPaintZone : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField]
    [Tooltip("The correct color required for all child zones. Will override individual zone colors on start.")]
    Color m_RequiredColor = Color.red;

    [SerializeField]
    [Tooltip("Automatically find and register all PaintZone components in children on Awake.")]
    bool m_AutoFindChildZones = true;

    [SerializeField]
    [Tooltip("Manually assigned child paint zones (if not using auto-find).")]
    List<PaintZone> m_ChildZones = new();

    [SerializeField]
    [Tooltip("Hide all child zone meshes when the combined zone is completed.")]
    bool m_HideAllWhenComplete = true;

    [Header("Debug")]
    [SerializeField]
    [Tooltip("Enable detailed debug logging.")]
    bool m_EnableDebugLogs = true;

    [Header("Events")]
    [SerializeField]
    [Tooltip("Invoked when the entire combined zone is completed (all child zones filled).")]
    UnityEvent m_OnCombinedZoneCompleted = new();

    [SerializeField]
    [Tooltip("Invoked when any individual child zone is completed.")]
    UnityEvent m_OnChildZoneCompleted = new();

    [SerializeField]
    [Tooltip("Invoked when incorrect color is detected in any child zone.")]
    UnityEvent m_OnIncorrectColorDetected = new();

    [SerializeField]
    [Tooltip("Invoked when a controller enters any child zone.")]
    UnityEvent m_OnControllerEntered = new();

    // State tracking
    HashSet<PaintZone> m_CompletedChildZones = new();
    bool m_CombinedZoneCompleted = false;

    /// <summary>
    /// The required color for all child zones.
    /// </summary>
    public Color requiredColor
    {
        get => m_RequiredColor;
        set
        {
            m_RequiredColor = value;
            // Update all child zones
            foreach (var zone in m_ChildZones)
            {
                if (zone != null)
                    zone.correctColor = value;
            }
        }
    }

    /// <summary>
    /// Read-only list of all child zones.
    /// </summary>
    public IReadOnlyList<PaintZone> childZones => m_ChildZones;

    /// <summary>
    /// Number of child zones that have been completed.
    /// </summary>
    public int completedChildCount => m_CompletedChildZones.Count;

    /// <summary>
    /// Total number of child zones.
    /// </summary>
    public int totalChildCount => m_ChildZones.Count;

    /// <summary>
    /// Completion percentage based on child zones (0-1).
    /// </summary>
    public float completionPercentage => totalChildCount > 0 ? (float)completedChildCount / totalChildCount : 0f;

    /// <summary>
    /// Whether the entire combined zone has been completed.
    /// </summary>
    public bool isCompleted => m_CombinedZoneCompleted;

    /// <summary>
    /// Event invoked when the entire combined zone is completed.
    /// </summary>
    public UnityEvent onCombinedZoneCompleted => m_OnCombinedZoneCompleted;

    /// <summary>
    /// Event invoked when any individual child zone is completed.
    /// </summary>
    public UnityEvent onChildZoneCompleted => m_OnChildZoneCompleted;

    /// <summary>
    /// Event invoked when incorrect color is detected.
    /// </summary>
    public UnityEvent onIncorrectColorDetected => m_OnIncorrectColorDetected;

    /// <summary>
    /// Event invoked when a controller enters any child zone.
    /// </summary>
    public UnityEvent onControllerEntered => m_OnControllerEntered;

    void Awake()
    {
        InitializeChildZones();
    }

    void Start()
    {
        // Subscribe to child zone events after all zones are initialized
        foreach (var zone in m_ChildZones)
        {
            if (zone != null)
            {
                zone.onCompleted.AddListener(() => OnChildZoneCompleted(zone));
                zone.onIncorrectColorDetected.AddListener(OnChildIncorrectColor);
                zone.onControllerEntered.AddListener(OnChildControllerEntered);
            }
        }

        if (m_EnableDebugLogs)
            Debug.Log($"[CombinedPaintZone] Initialized '{gameObject.name}' with {m_ChildZones.Count} child zones | Required Color: {m_RequiredColor}");
    }

    void InitializeChildZones()
    {
        // Auto-find child zones if enabled
        if (m_AutoFindChildZones)
        {
            PaintZone[] foundZones = GetComponentsInChildren<PaintZone>();
            
            // Clear and rebuild list
            m_ChildZones.Clear();
            foreach (var zone in foundZones)
            {
                // Don't include zones that are children of other CombinedPaintZones
                if (zone.GetComponentInParent<CombinedPaintZone>() == this)
                {
                    m_ChildZones.Add(zone);
                }
            }

            if (m_EnableDebugLogs)
                Debug.Log($"[CombinedPaintZone] Auto-found {m_ChildZones.Count} child zones in '{gameObject.name}'");
        }

        // Remove null entries
        m_ChildZones.RemoveAll(zone => zone == null);

        // Set the required color for all child zones
        foreach (var zone in m_ChildZones)
        {
            if (zone != null)
            {
                zone.correctColor = m_RequiredColor;
            }
        }

        if (m_ChildZones.Count == 0)
        {
            Debug.LogWarning($"[CombinedPaintZone] No child PaintZones found in '{gameObject.name}'. Add PaintZone components to children or disable auto-find and assign manually.", this);
        }
    }

    /// <summary>
    /// Called when an individual child zone completes.
    /// </summary>
    void OnChildZoneCompleted(PaintZone zone)
    {
        if (zone == null || m_CombinedZoneCompleted)
            return;

        // Track completion
        if (!m_CompletedChildZones.Contains(zone))
        {
            m_CompletedChildZones.Add(zone);

            if (m_EnableDebugLogs)
                Debug.Log($"[CombinedPaintZone] Child zone '{zone.gameObject.name}' completed. Progress: {completedChildCount}/{totalChildCount} ({completionPercentage:P0})");

            // Invoke child completion event
            m_OnChildZoneCompleted?.Invoke();

            // Check if all zones are complete
            if (m_CompletedChildZones.Count >= m_ChildZones.Count)
            {
                OnCombinedZoneCompleted();
            }
        }
    }

    /// <summary>
    /// Called when all child zones are completed.
    /// </summary>
    void OnCombinedZoneCompleted()
    {
        if (m_CombinedZoneCompleted)
            return;

        m_CombinedZoneCompleted = true;

        if (m_EnableDebugLogs)
            Debug.Log($"[CombinedPaintZone] Combined zone '{gameObject.name}' COMPLETED! All {totalChildCount} child zones filled.");

        // Hide all child zones if configured
        if (m_HideAllWhenComplete)
        {
            foreach (var zone in m_ChildZones)
            {
                if (zone != null)
                {
                    MeshRenderer renderer = zone.GetComponent<MeshRenderer>();
                    if (renderer != null)
                        renderer.enabled = false;
                }
            }
        }

        // Invoke completion event
        m_OnCombinedZoneCompleted?.Invoke();
    }

    /// <summary>
    /// Called when incorrect color is detected in any child zone.
    /// </summary>
    void OnChildIncorrectColor()
    {
        m_OnIncorrectColorDetected?.Invoke();
    }

    /// <summary>
    /// Called when a controller enters any child zone.
    /// </summary>
    void OnChildControllerEntered()
    {
        m_OnControllerEntered?.Invoke();
    }

    /// <summary>
    /// Resets the combined zone and all child zones to their initial state.
    /// </summary>
    public void ResetCombinedZone()
    {
        m_CombinedZoneCompleted = false;
        m_CompletedChildZones.Clear();

        foreach (var zone in m_ChildZones)
        {
            if (zone != null)
            {
                zone.ResetZone();
            }
        }

        if (m_EnableDebugLogs)
            Debug.Log($"[CombinedPaintZone] Reset combined zone '{gameObject.name}'");
    }

    /// <summary>
    /// Manually add a child zone to the combined zone.
    /// </summary>
    public void AddChildZone(PaintZone zone)
    {
        if (zone == null || m_ChildZones.Contains(zone))
            return;

        m_ChildZones.Add(zone);
        zone.correctColor = m_RequiredColor;

        // Subscribe to events if already started
        if (enabled && gameObject.activeInHierarchy)
        {
            zone.onCompleted.AddListener(() => OnChildZoneCompleted(zone));
            zone.onIncorrectColorDetected.AddListener(OnChildIncorrectColor);
            zone.onControllerEntered.AddListener(OnChildControllerEntered);
        }

        if (m_EnableDebugLogs)
            Debug.Log($"[CombinedPaintZone] Added child zone '{zone.gameObject.name}' to '{gameObject.name}'");
    }

    /// <summary>
    /// Manually remove a child zone from the combined zone.
    /// </summary>
    public void RemoveChildZone(PaintZone zone)
    {
        if (zone == null)
            return;

        m_ChildZones.Remove(zone);
        m_CompletedChildZones.Remove(zone);

        if (m_EnableDebugLogs)
            Debug.Log($"[CombinedPaintZone] Removed child zone '{zone.gameObject.name}' from '{gameObject.name}'");
    }

    /// <summary>
    /// Gets the completion status of a specific child zone.
    /// </summary>
    public bool IsChildZoneCompleted(PaintZone zone)
    {
        return m_CompletedChildZones.Contains(zone);
    }

    /// <summary>
    /// Force recalculate coverage for all child zones. Useful for debugging.
    /// </summary>
    public void ForceRecalculateAllCoverage()
    {
        foreach (var zone in m_ChildZones)
        {
            if (zone != null)
            {
                zone.ForceRecalculateCoverage();
            }
        }

        if (m_EnableDebugLogs)
            Debug.Log($"[CombinedPaintZone] Forced coverage recalculation for all child zones in '{gameObject.name}'");
    }

    void OnDrawGizmosSelected()
    {
        // Draw a bounding box around the entire combined zone
        if (m_ChildZones != null && m_ChildZones.Count > 0)
        {
            Bounds combinedBounds = new Bounds(transform.position, Vector3.zero);
            bool boundsInitialized = false;

            foreach (var zone in m_ChildZones)
            {
                if (zone != null)
                {
                    Collider col = zone.GetComponent<Collider>();
                    if (col != null)
                    {
                        if (!boundsInitialized)
                        {
                            combinedBounds = col.bounds;
                            boundsInitialized = true;
                        }
                        else
                        {
                            combinedBounds.Encapsulate(col.bounds);
                        }
                    }
                }
            }

            if (boundsInitialized)
            {
                // Draw outer bounds
                Gizmos.color = m_CombinedZoneCompleted ? Color.green : new Color(m_RequiredColor.r, m_RequiredColor.g, m_RequiredColor.b, 0.5f);
                Gizmos.DrawWireCube(combinedBounds.center, combinedBounds.size);

                // Show completion status as text
                #if UNITY_EDITOR
                var style = new UnityEngine.GUIStyle();
                style.normal.textColor = m_CombinedZoneCompleted ? Color.green : Color.cyan;
                style.fontSize = 16;
                style.fontStyle = FontStyle.Bold;
                string label = m_CombinedZoneCompleted 
                    ? "COMBINED ZONE\nCOMPLETED" 
                    : $"COMBINED ZONE\n{completedChildCount}/{totalChildCount} zones\n{completionPercentage:P0}";
                UnityEditor.Handles.Label(combinedBounds.center + Vector3.up * (combinedBounds.size.y * 0.5f + 0.3f), label, style);
                #endif
            }
        }
    }
}
