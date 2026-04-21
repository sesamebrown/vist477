using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a single paint stroke using a LineRenderer.
/// Handles adding points and maintaining the visual representation of a painted line.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class PaintLine : MonoBehaviour
{
    LineRenderer m_LineRenderer;
    List<Vector3> m_Points = new List<Vector3>();
    List<Vector3> m_InputPoints = new List<Vector3>(); // Original unsmoothed points
    bool m_IsFinalized;
    bool m_UseSmoothing;
    int m_SmoothingSegments;
    Color m_LineColor;
    bool m_GenerateCollider = true;
    GameObject m_ColliderContainer;
    XRPaintInteractor m_Creator; // The interactor (controller) that created this line
    Vector3 m_OffsetDirection; // Direction to offset points for Z-layering
    float m_OffsetAmount; // Amount to offset points along offset direction

    /// <summary>
    /// The LineRenderer component used to visualize this paint line.
    /// </summary>
    public LineRenderer lineRenderer => m_LineRenderer;

    /// <summary>
    /// Number of points in this paint line.
    /// </summary>
    public int pointCount => m_Points.Count;

    /// <summary>
    /// Whether this paint line has been finalized and is no longer accepting new points.
    /// </summary>
    public bool isFinalized => m_IsFinalized;

    /// <summary>
    /// The color of this paint line.
    /// </summary>
    public Color lineColor => m_LineColor;

    /// <summary>
    /// The XRPaintInteractor (controller) that created this paint line.
    /// </summary>
    public XRPaintInteractor creator => m_Creator;

    void Awake()
    {
        m_LineRenderer = GetComponent<LineRenderer>();
    }

    /// <summary>
    /// Initializes the paint line with specified visual properties.
    /// </summary>
    /// <param name="material">Material to use for the line.</param>
    /// <param name="color">Color of the line.</param>
    /// <param name="width">Width of the line.</param>
    public void Initialize(Material material, Color color, float width)
    {
        Initialize(material, color, width, false, 0, null, Vector3.zero, 0f);
    }

    /// <summary>
    /// Initializes the paint line with specified visual properties and creator reference.
    /// </summary>
    /// <param name="material">Material to use for the line.</param>
    /// <param name="color">Color of the line.</param>
    /// <param name="width">Width of the line.</param>
    /// <param name="creator">The XRPaintInteractor that created this line.</param>
    public void Initialize(Material material, Color color, float width, XRPaintInteractor creator)
    {
        Initialize(material, color, width, false, 0, creator, Vector3.zero, 0f);
    }

    /// <summary>
    /// Initializes the paint line with specified visual properties and smoothing options.
    /// </summary>
    /// <param name="material">Material to use for the line.</param>
    /// <param name="color">Color of the line.</param>
    /// <param name="width">Width of the line.</param>
    /// <param name="useSmoothing">Whether to interpolate between points for smoother curves.</param>
    /// <param name="smoothingSegments">Number of interpolated points between input points.</param>
    /// <param name="creator">The XRPaintInteractor that created this line.</param>
    /// <param name="offsetDirection">Direction to offset points for Z-layering.</param>
    /// <param name="offsetAmount">Amount to offset points along offset direction.</param>
    public void Initialize(Material material, Color color, float width, bool useSmoothing, int smoothingSegments, XRPaintInteractor creator = null, Vector3 offsetDirection = default, float offsetAmount = 0f)
    {
        if (m_LineRenderer == null)
            m_LineRenderer = GetComponent<LineRenderer>();

        m_UseSmoothing = useSmoothing;
        m_SmoothingSegments = smoothingSegments;
        m_LineColor = color;
        m_Creator = creator;
        m_OffsetDirection = offsetDirection;
        m_OffsetAmount = offsetAmount;

        Debug.Log($"[PaintLine] Initialized {gameObject.name} with color: {m_LineColor}, width: {width}");

        // Configure line renderer
        m_LineRenderer.material = material;
        m_LineRenderer.startColor = color;
        m_LineRenderer.endColor = color;
        m_LineRenderer.startWidth = width;
        m_LineRenderer.endWidth = width;
        m_LineRenderer.positionCount = 0;
        m_LineRenderer.useWorldSpace = true;

        // Quality settings - moderate corner vertices for smooth curves
        m_LineRenderer.numCornerVertices = useSmoothing ? 5 : 2;
        // Set cap vertices to 0 for flat ends
        m_LineRenderer.numCapVertices = 0;
        
        // Use TransformZ alignment for 2D paper-like lines
        // The line's transform rotation (set by PaintInteractor) determines the plane orientation
        // Lines will be visible when viewed from directions perpendicular to their Z axis
        m_LineRenderer.alignment = LineAlignment.TransformZ;

        m_Points.Clear();
        m_InputPoints.Clear();
        m_IsFinalized = false;
    }

    /// <summary>
    /// Adds a new point to the paint line.
    /// </summary>
    /// <param name="position">World space position of the new point.</param>
    public void AddPoint(Vector3 position)
    {
        if (m_IsFinalized)
        {
            Debug.LogWarning("[PaintLine] Cannot add point to finalized line.");
            return;
        }

        // Apply Z-offset for proper layering
        Vector3 offsetPosition = position + m_OffsetDirection * m_OffsetAmount;
        m_InputPoints.Add(offsetPosition);

        if (m_UseSmoothing)
        {
            RebuildSmoothedLine();
        }
        else
        {
            m_Points.Add(offsetPosition); // Fixed: use offsetPosition, not position
            UpdateLineRenderer();
        }

        // Check for zone overlap on first point (for immediate feedback)
        if (m_InputPoints.Count == 1)
        {
            CheckInitialZoneOverlap(position);
        }
    }

    /// <summary>
    /// Rebuilds the entire smoothed line using Catmull-Rom spline interpolation.
    /// </summary>
    void RebuildSmoothedLine()
    {
        m_Points.Clear();

        // Need at least 2 points to draw anything
        if (m_InputPoints.Count < 2)
        {
            m_Points.AddRange(m_InputPoints);
            UpdateLineRenderer();
            return;
        }

        // For each segment between input points, add interpolated points
        for (int i = 0; i < m_InputPoints.Count - 1; i++)
        {
            Vector3 p0 = i > 0 ? m_InputPoints[i - 1] : m_InputPoints[i];
            Vector3 p1 = m_InputPoints[i];
            Vector3 p2 = m_InputPoints[i + 1];
            Vector3 p3 = i < m_InputPoints.Count - 2 ? m_InputPoints[i + 2] : m_InputPoints[i + 1];

            // Add the start point
            m_Points.Add(p1);

            // Add interpolated points between p1 and p2
            for (int j = 1; j <= m_SmoothingSegments; j++)
            {
                float t = j / (float)(m_SmoothingSegments + 1);
                Vector3 interpolatedPoint = CatmullRom(p0, p1, p2, p3, t);
                m_Points.Add(interpolatedPoint);
            }
        }

        // Add the final point
        m_Points.Add(m_InputPoints[m_InputPoints.Count - 1]);

        UpdateLineRenderer();
    }

    /// <summary>
    /// Catmull-Rom spline interpolation for smooth curves.
    /// </summary>
    Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    /// <summary>
    /// Finalizes the paint line, preventing further modifications.
    /// </summary>
    public void Finalize()
    {
        m_IsFinalized = true;

        // Clean up line if it has too few points
        if (m_Points.Count < 2)
        {
            Debug.Log($"[PaintLine] Destroying line with insufficient points: {m_Points.Count}");
            Destroy(gameObject);
            return;
        }

        // Generate colliders for zone detection
        if (m_GenerateCollider)
        {
            GenerateColliders();
            
            // Verify colliders were created
            if (m_ColliderContainer != null)
            {
                int actualColliderCount = m_ColliderContainer.GetComponentsInChildren<SphereCollider>().Length;
                Debug.Log($"[PaintLine] Finalized {gameObject.name} | Collider container has {actualColliderCount} sphere colliders");
            }
            else
            {
                Debug.LogWarning($"[PaintLine] Finalized {gameObject.name} but collider container is null!");
            }
        }
        else
        {
            Debug.Log($"[PaintLine] Finalized {gameObject.name} without generating colliders (m_GenerateCollider = false)");
        }
    }

    /// <summary>
    /// Updates the LineRenderer with current point data.
    /// </summary>
    void UpdateLineRenderer()
    {
        if (m_LineRenderer == null || m_Points.Count == 0)
            return;

        m_LineRenderer.positionCount = m_Points.Count;
        m_LineRenderer.SetPositions(m_Points.ToArray());
    }

    /// <summary>
    /// Gets a copy of all points in this paint line.
    /// </summary>
    public Vector3[] GetPoints()
    {
        return m_Points.ToArray();
    }

    /// <summary>
    /// Generates sphere colliders along the line for zone detection.
    /// Creates a container GameObject and adds sphere colliders at intervals.
    /// </summary>
    void GenerateColliders()
    {
        if (m_Points.Count < 2)
        {
            Debug.LogWarning($"[PaintLine] Cannot generate colliders - too few points: {m_Points.Count}");
            return;
        }

        // Create container for colliders
        m_ColliderContainer = new GameObject("LineColliders");
        m_ColliderContainer.transform.SetParent(transform, false);
        m_ColliderContainer.transform.localPosition = Vector3.zero;
        m_ColliderContainer.transform.localRotation = Quaternion.identity;

        // Get line width for collider size
        float lineWidth = m_LineRenderer != null ? m_LineRenderer.startWidth : 0.01f;
        float colliderRadius = lineWidth * 0.75f; // Slightly smaller than line width

        // Add sphere colliders at regular intervals
        // Skip some points to reduce collider count for performance
        int step = Mathf.Max(1, m_Points.Count / 20); // Aim for ~20 colliders max

        int colliderCount = 0;
        for (int i = 0; i < m_Points.Count; i += step)
        {
            CreateSphereColliderAt(m_Points[i], colliderRadius);
            colliderCount++;
        }

        // Always add collider at the last point
        if ((m_Points.Count - 1) % step != 0)
        {
            CreateSphereColliderAt(m_Points[m_Points.Count - 1], colliderRadius);
            colliderCount++;
        }

        Debug.Log($"[PaintLine] Generated {colliderCount} colliders for {gameObject.name} (color: {m_LineColor})");

        // Manually check for overlapping PaintZones since OnTriggerEnter
        // only fires when colliders MOVE into triggers, not when spawned inside
        NotifyOverlappingPaintZones();
    }

    /// <summary>
    /// Creates a sphere collider at the specified world position.
    /// </summary>
    void CreateSphereColliderAt(Vector3 worldPosition, float radius)
    {
        GameObject colliderObj = new GameObject("LineColliderPoint");
        colliderObj.transform.SetParent(m_ColliderContainer.transform, false);
        colliderObj.transform.position = worldPosition;
        colliderObj.layer = gameObject.layer;

        SphereCollider sphereCollider = colliderObj.AddComponent<SphereCollider>();
        sphereCollider.radius = radius;
        sphereCollider.isTrigger = true;

        // Add reference to this PaintLine so the zone can identify the line
        // The zone will check the parent hierarchy for the PaintLine component
        PaintLineColliderReference reference = colliderObj.AddComponent<PaintLineColliderReference>();
        reference.paintLine = this;

        Debug.Log($"[PaintLine] Created collider at {worldPosition} | Radius: {radius} | Layer: {LayerMask.LayerToName(colliderObj.layer)} ({colliderObj.layer}) | IsTrigger: {sphereCollider.isTrigger} | Reference set: {reference.paintLine != null}");
    }

    /// <summary>
    /// Manually checks for PaintZones that this line's colliders overlap with.
    /// Called after colliders are generated since OnTriggerEnter doesn't fire for colliders spawned inside triggers.
    /// Note: May notify zones already notified in CheckInitialZoneOverlap, but duplicate checking in PaintZone prevents issues.
    /// </summary>
    void NotifyOverlappingPaintZones()
    {
        if (m_ColliderContainer == null)
            return;

        SphereCollider[] lineColliders = m_ColliderContainer.GetComponentsInChildren<SphereCollider>();
        if (lineColliders.Length == 0)
        {
            Debug.LogWarning($"[PaintLine] NotifyOverlappingPaintZones found no colliders!");
            return;
        }

        // Find all PaintZones in the scene
        PaintZone[] paintZones = FindObjectsByType<PaintZone>(FindObjectsSortMode.None);
        Debug.Log($"[PaintLine] Checking {lineColliders.Length} colliders against {paintZones.Length} paint zones");

        foreach (var zone in paintZones)
        {
            if (zone == null || !zone.enabled)
                continue;

            Collider zoneCollider = zone.GetComponent<Collider>();
            if (zoneCollider == null || !zoneCollider.isTrigger)
                continue;

            // Check if any of our line colliders overlap this zone
            bool overlaps = false;
            foreach (var lineCol in lineColliders)
            {
                if (lineCol == null)
                    continue;

                // Check if line collider bounds intersect zone bounds
                if (zoneCollider.bounds.Intersects(lineCol.bounds))
                {
                    overlaps = true;
                    Debug.Log($"[PaintLine] Detected overlap between {gameObject.name} and PaintZone {zone.gameObject.name}");
                    break;
                }
            }

            // If we overlap, manually notify the zone
            if (overlaps)
            {
                zone.OnPaintLineCreated(this, isInitialCheck: false); // Line is finalized
            }
        }
    }

    /// <summary>
    /// Checks if the initial point position is inside any PaintZone and notifies them immediately.
    /// This provides instant feedback when starting to paint with the wrong color.
    /// </summary>
    void CheckInitialZoneOverlap(Vector3 position)
    {
        // Find all PaintZones in the scene
        PaintZone[] paintZones = FindObjectsByType<PaintZone>(FindObjectsSortMode.None);
        Debug.Log($"[PaintLine] Checking initial point against {paintZones.Length} paint zones");

        foreach (var zone in paintZones)
        {
            if (zone == null || !zone.enabled)
                continue;

            Collider zoneCollider = zone.GetComponent<Collider>();
            if (zoneCollider == null || !zoneCollider.isTrigger)
                continue;

            // Check if the point is inside the zone's bounds
            if (zoneCollider.bounds.Contains(position))
            {
                Debug.Log($"[PaintLine] Initial point inside PaintZone {zone.gameObject.name}, notifying for immediate color check");
                zone.OnPaintLineCreated(this, isInitialCheck: true); // First point - immediate feedback
            }
        }
    }

    void OnDrawGizmos()
    {
        // Visualize colliders in the scene view for debugging
        if (m_ColliderContainer != null)
        {
            Gizmos.color = new Color(m_LineColor.r, m_LineColor.g, m_LineColor.b, 0.3f);
            SphereCollider[] colliders = m_ColliderContainer.GetComponentsInChildren<SphereCollider>();
            foreach (var col in colliders)
            {
                if (col != null && col.enabled)
                {
                    Gizmos.DrawWireSphere(col.transform.position, col.radius);
                }
            }
        }
    }
}