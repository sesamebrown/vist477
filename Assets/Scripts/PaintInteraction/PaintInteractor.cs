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

    [Header("Paint Settings")]
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
    }

    void OnEnable()
    {
        // Enable the input reader
        m_PaintInput?.EnableDirectActionIfModeUsed();
    }

    void OnDisable()
    {
        // Clean up any active paint stroke
        if (m_IsPainting)
        {
            EndPaintStroke();
        }

        // Disable input reader
        m_PaintInput?.DisableDirectActionIfModeUsed();
    }

    void Update()
    {
        if (m_PaintPoint == null)
            return;

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

        // Create new paint line
        GameObject lineObject = new GameObject($"PaintLine_{System.DateTime.Now:HHmmss_fff}");
        
        if (m_LinesParent != null)
            lineObject.transform.SetParent(m_LinesParent, false);

        m_CurrentLine = lineObject.AddComponent<PaintLine>();
        m_CurrentLine.Initialize(m_LineMaterial, m_LineColor, m_LineWidth);

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
            m_CurrentLine.AddPoint(currentPosition);
            m_LastPaintPosition = currentPosition;
        }
    }

    void EndPaintStroke()
    {
        m_IsPainting = false;
        
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