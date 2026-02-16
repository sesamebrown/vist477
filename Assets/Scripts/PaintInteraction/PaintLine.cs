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
        Initialize(material, color, width, false, 0);
    }

    /// <summary>
    /// Initializes the paint line with specified visual properties and smoothing options.
    /// </summary>
    /// <param name="material">Material to use for the line.</param>
    /// <param name="color">Color of the line.</param>
    /// <param name="width">Width of the line.</param>
    /// <param name="useSmoothing">Whether to interpolate between points for smoother curves.</param>
    /// <param name="smoothingSegments">Number of interpolated points between input points.</param>
    public void Initialize(Material material, Color color, float width, bool useSmoothing, int smoothingSegments)
    {
        if (m_LineRenderer == null)
            m_LineRenderer = GetComponent<LineRenderer>();

        m_UseSmoothing = useSmoothing;
        m_SmoothingSegments = smoothingSegments;

        // Configure line renderer
        m_LineRenderer.material = material;
        m_LineRenderer.startColor = color;
        m_LineRenderer.endColor = color;
        m_LineRenderer.startWidth = width;
        m_LineRenderer.endWidth = width;
        m_LineRenderer.positionCount = 0;
        m_LineRenderer.useWorldSpace = true;

        // Quality settings - keep corner vertices low for flat 2D appearance
        m_LineRenderer.numCornerVertices = useSmoothing ? 4 : 2;
        m_LineRenderer.numCapVertices = useSmoothing ? 4 : 2;
        m_LineRenderer.alignment = LineAlignment.View;

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

        m_InputPoints.Add(position);

        if (m_UseSmoothing)
        {
            RebuildSmoothedLine();
        }
        else
        {
            m_Points.Add(position);
            UpdateLineRenderer();
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
}