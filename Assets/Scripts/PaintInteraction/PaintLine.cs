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
    bool m_IsFinalized;

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
        if (m_LineRenderer == null)
            m_LineRenderer = GetComponent<LineRenderer>();

        // Configure line renderer
        m_LineRenderer.material = material;
        m_LineRenderer.startColor = color;
        m_LineRenderer.endColor = color;
        m_LineRenderer.startWidth = width;
        m_LineRenderer.endWidth = width;
        m_LineRenderer.positionCount = 0;
        m_LineRenderer.useWorldSpace = true;

        // Quality settings
        m_LineRenderer.numCornerVertices = 4;
        m_LineRenderer.numCapVertices = 4;
        m_LineRenderer.alignment = LineAlignment.View;

        m_Points.Clear();
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

        m_Points.Add(position);
        UpdateLineRenderer();
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