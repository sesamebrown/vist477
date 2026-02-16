using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

/// <summary>
/// Interactor that allows painting in 3D space by creating line renderers when trigger is held.
/// Follows XR Interaction Toolkit patterns for input handling and lifecycle management.
/// </summary>
[AddComponentMenu("XR/Interactors/XR Paint Interactor")]
public class XRPaintInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    [Tooltip("Paint point component that represents where paint will spawn. Should be a child object positioned by another system.")]
    PaintPoint m_PaintPoint;

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

    [Header("Paint Settings")]
    [SerializeField]
    [Tooltip("Preset line widths to cycle through. User can switch between these using the cycle size button.")]
    float[] m_LineSizePresets = new float[] { 0.002f, 0.005f, 0.010f, 0.020f, 0.050f };

    /// <summary>
    /// Array of preset line widths that users can cycle through.
    /// </summary>
    public float[] lineSizePresets
    {
        get => m_LineSizePresets;
        set => m_LineSizePresets = value;
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
    List<Vector3> m_RecentPositions = new List<Vector3>();

    /// <summary>
    /// Whether the user is currently painting.
    /// </summary>
    public bool isPainting => m_IsPainting;

    /// <summary>
    /// The current active paint line being drawn, or null if not painting.
    /// </summary>
    public PaintLine currentLine => m_CurrentLine;

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
    }

    void OnEnable()
    {
        // Enable input readers
        m_PaintInput?.EnableDirectActionIfModeUsed();
        m_CycleSizeInput?.EnableDirectActionIfModeUsed();
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
    }

    void Update()
    {
        if (m_PaintPoint == null)
            return;

        // Check for size cycling input (button press)
        bool cycleSizePressed = m_CycleSizeInput.ReadIsPerformed();
        if (cycleSizePressed && !m_PreviousCycleSizePressed)
        {
            CycleToNextSize();
        }
        m_PreviousCycleSizePressed = cycleSizePressed;

        // Check paint input state
        bool paintInputActive = m_PaintInput.ReadIsPerformed();

        // Start painting
        if (paintInputActive && !m_IsPainting)
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
        m_IsPainting = true;
        m_LastPaintPosition = m_PaintPoint.transform.position;
        m_RecentPositions.Clear();
        m_RecentPositions.Add(m_LastPaintPosition);

        // Hide indicator while painting
        m_PaintPoint.HideIndicator();

        // Create new paint line
        GameObject lineObject = new GameObject($"PaintLine_{System.DateTime.Now:HHmmss_fff}");
        
        if (m_LinesParent != null)
            lineObject.transform.SetParent(m_LinesParent, false);

        m_CurrentLine = lineObject.AddComponent<PaintLine>();
        m_CurrentLine.Initialize(m_LineMaterial, m_LineColor, m_LineWidth, m_SmoothLines, m_SmoothingSegments);

        // Add initial point
        m_CurrentLine.AddPoint(m_LastPaintPosition);
    }

    void ContinuePaintStroke()
    {
        if (m_CurrentLine == null)
            return;

        Vector3 currentPosition = m_PaintPoint.transform.position;
        float distanceMoved = Vector3.Distance(currentPosition, m_LastPaintPosition);

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

            // Adaptive sampling: if moving fast, add intermediate points
            if (m_AdaptiveSampling && distanceMoved > m_MaxPointDistance)
            {
                // Calculate how many intermediate points we need
                int numIntermediatePoints = Mathf.CeilToInt(distanceMoved / m_MaxPointDistance);
                
                // Add interpolated points between last and current position
                for (int i = 1; i <= numIntermediatePoints; i++)
                {
                    float t = i / (float)(numIntermediatePoints + 1);
                    Vector3 intermediatePoint = Vector3.Lerp(m_LastPaintPosition, positionToAdd, t);
                    m_CurrentLine.AddPoint(intermediatePoint);
                }
            }

            m_CurrentLine.AddPoint(positionToAdd);
            m_LastPaintPosition = positionToAdd;
        }
    }

    void EndPaintStroke()
    {
        m_IsPainting = false;
        
        // Show indicator after painting
        if (m_PaintPoint != null)
            m_PaintPoint.ShowIndicator();
        
        // Finalize the line
        if (m_CurrentLine != null)
        {
            m_CurrentLine.Finalize();
            m_CurrentLine = null;
        }
    }

    /// <summary>
    /// Creates a default unlit material for line rendering if none is assigned.
    /// Uses the Sprites/Default shader which supports vertex colors for LineRenderer.
    /// </summary>
    Material CreateDefaultLineMaterial()
    {
        // Use Sprites/Default shader which supports vertex colors
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogWarning("[XRPaintInteractor] Sprites/Default shader not found, trying Particles/Standard Unlit");
            shader = Shader.Find("Particles/Standard Unlit");
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
}