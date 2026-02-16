using UnityEngine;


/// <summary>
/// Component that marks a transform as a paint point and optionally visualizes where paint will appear.
/// Attach this to the paint point transform to show a visual indicator that auto-syncs with the paint interactor's color.
/// </summary>
public class PaintPoint : MonoBehaviour
{   
    [Header("References")]
    [SerializeField]
    [Tooltip("The paint interactor that uses this paint point. Used to sync indicator color.")]
    XRPaintInteractor m_PaintInteractor;

    /// <summary>
    /// The paint interactor that uses this paint point.
    /// </summary>
    public XRPaintInteractor paintInteractor
    {
        get => m_PaintInteractor;
        set => m_PaintInteractor = value;
    }
    [Header("Visualization")]
    [SerializeField]
    [Tooltip("Whether to show a visual indicator for the paint point.")]
    bool m_ShowIndicator = true;

    [SerializeField]
    [Tooltip("Color of the paint point indicator. If Paint Interactor is assigned, this will be overridden with the line color at startup.")]
    Color m_IndicatorColor = Color.cyan;

    [SerializeField]
    [Tooltip("Opacity of the indicator (0-1). Lower values make it more transparent so you can see drawn lines through it.")]
    [Range(0f, 1f)]
    float m_IndicatorAlpha = 0.5f;

    [SerializeField]
    [Tooltip("Size of the paint point indicator. This should be set programmatically to match the line width.")]
    float m_IndicatorSize = 0.01f;

    GameObject m_IndicatorSphere;
    MeshRenderer m_IndicatorRenderer;
    Vector3 m_BaseScale;

    /// <summary>
    /// Whether the paint point indicator is visible.
    /// </summary>
    public bool showIndicator
    {
        get => m_ShowIndicator;
        set
        {
            m_ShowIndicator = value;
            UpdateIndicatorVisibility();
        }
    }

    /// <summary>
    /// Color of the paint point indicator.
    /// </summary>
    public Color indicatorColor
    {
        get => m_IndicatorColor;
        set
        {
            m_IndicatorColor = value;
            UpdateIndicatorColor();
        }
    }

    /// <summary>
    /// Size of the paint point indicator.
    /// </summary>
    public float indicatorSize
    {
        get => m_IndicatorSize;
        set
        {
            m_IndicatorSize = value;
            UpdateIndicatorSize();
        }
    }

    void Awake()
    {
        CreateIndicator();
    }

    void Start()
    {
        // Auto-sync with paint interactor if assigned
        if (m_PaintInteractor != null)
        {
            SyncColorWithPaintInteractor();
            // Update indicator size to match line width
            indicatorSize = m_PaintInteractor.lineWidth;
        }
    }

    void OnEnable()
    {
        UpdateIndicatorVisibility();
    }

    void OnDisable()
    {
        if (m_IndicatorSphere != null)
            m_IndicatorSphere.SetActive(false);
    }

    void CreateIndicator()
    {
        m_IndicatorSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        m_IndicatorSphere.name = "PaintPointIndicator";
        m_IndicatorSphere.transform.SetParent(transform, false);
        m_IndicatorSphere.transform.localPosition = Vector3.zero;
        m_BaseScale = Vector3.one * m_IndicatorSize;
        m_IndicatorSphere.transform.localScale = m_BaseScale;

        // Remove collider
        if (m_IndicatorSphere.TryGetComponent<Collider>(out var collider))
            Destroy(collider);

        m_IndicatorRenderer = m_IndicatorSphere.GetComponent<MeshRenderer>();
        
        // Use UI/Default shader which reliably supports transparency
        Shader shader = Shader.Find("UI/Default");
        if (shader == null)
        {
            Debug.LogWarning("[PaintPoint] UI/Default not found, trying Legacy Shaders/Transparent/Diffuse");
            shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
        }
        
        if (shader == null)
        {
            Debug.LogError("[PaintPoint] No suitable shader found for indicator!");
            return;
        }
        
        var mat = new Material(shader);
        mat.color = m_IndicatorColor;
        
        // Debug.Log($"[PaintPoint] Created indicator with shader: {shader.name}, initial color: {m_IndicatorColor}");
        
        m_IndicatorRenderer.material = mat;
        
        // Debug.Log($"[PaintPoint] Indicator created with shader: {shader?.name ?? "NULL"}, color: {m_IndicatorColor}, scale: {m_IndicatorSize}");

        UpdateIndicatorVisibility();
    }

    void UpdateIndicatorVisibility()
    {
        if (m_IndicatorSphere != null)
            m_IndicatorSphere.SetActive(m_ShowIndicator);
    }

    void UpdateIndicatorColor()
    {
        if (m_IndicatorRenderer != null && m_IndicatorRenderer.material != null)
            m_IndicatorRenderer.material.color = m_IndicatorColor;
    }

    void UpdateIndicatorSize()
    {
        if (m_IndicatorSphere != null)
        {
            m_BaseScale = Vector3.one * m_IndicatorSize;
            m_IndicatorSphere.transform.localScale = m_BaseScale;
        }
    }

    /// <summary>
    /// Syncs the indicator color with the paint interactor's line color,
    /// applying the configured alpha transparency.
    /// </summary>
    public void SyncColorWithPaintInteractor()
    {
        if (m_PaintInteractor == null)
        {
            Debug.LogWarning("[PaintPoint] Cannot sync color - no paint interactor assigned.", this);
            return;
        }

        Color lineColor = m_PaintInteractor.lineColor;
        lineColor.a = m_IndicatorAlpha;
        indicatorColor = lineColor;
        
        // Debug.Log($"[PaintPoint] Synced color to: {lineColor} from paint interactor's line color: {m_PaintInteractor.lineColor}");
    }
    /// <summary>
    /// Hides the paint point indicator.
    /// </summary>
    public void HideIndicator()
    {
        if (m_IndicatorSphere != null)
            m_IndicatorSphere.SetActive(false);
    }

    /// <summary>
    /// Shows the paint point indicator.
    /// </summary>
    public void ShowIndicator()
    {
        if (m_IndicatorSphere != null && m_ShowIndicator)
            m_IndicatorSphere.SetActive(true);
    }}