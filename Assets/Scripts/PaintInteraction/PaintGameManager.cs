using UnityEngine;

public class PaintGameManager : MonoBehaviour
{
    [SerializeField]
    PaintZone[] m_PaintZones;
    
    [SerializeField]
    [Tooltip("Paint interactors to manage. When color switching is disabled, these will be auto-set to the correct zone color.")]
    XRPaintInteractor[] m_PaintInteractors;
    
    [SerializeField]
    [Tooltip("If true, players can cycle through colors manually. If false, color is automatically set to match the active zone.")]
    bool m_AllowColorSwitching = true;
    
    int m_CurrentZoneIndex = 0;

    /// <summary>
    /// Whether all paint zones have been completed.
    /// When true, users can paint freely anywhere.
    /// When false, painting is restricted to zones only.
    /// </summary>
    public bool allZonesCompleted => m_CurrentZoneIndex > m_PaintZones.Length;
    
    /// <summary>
    /// Whether players can manually switch colors.
    /// </summary>
    public bool allowColorSwitching => m_AllowColorSwitching;
    private void Start()
    {
        // Deactivate all paint zones at the start
        foreach (var zone in m_PaintZones)
        {
            zone.gameObject.SetActive(false);
        }
        AddNextPaintZone(); // Activate the first zone
    }
    public void AddNextPaintZone()
    {
        if (m_CurrentZoneIndex < m_PaintZones.Length)
        {
            PaintZone zone = m_PaintZones[m_CurrentZoneIndex];
            zone.gameObject.SetActive(true);
            
            // If color switching is disabled, automatically set all interactors to the correct color
            if (!m_AllowColorSwitching && m_PaintInteractors != null)
            {
                Color correctColor = zone.correctColor;
                foreach (var interactor in m_PaintInteractors)
                {
                    if (interactor != null)
                    {
                        // Find the matching color in the interactor's presets and set it
                        int colorIndex = FindColorPresetIndex(interactor, correctColor);
                        if (colorIndex >= 0)
                        {
                            interactor.currentColorIndex = colorIndex;
                        }
                        else
                        {
                            // If no preset matches, directly set the color
                            interactor.lineColor = correctColor;
                        }
                        Debug.Log($"[PaintGameManager] Auto-set interactor color to {correctColor} for zone {m_CurrentZoneIndex}");
                    }
                }
            }
            
            m_CurrentZoneIndex++;
        }
    }
    
    /// <summary>
    /// Finds the index of a color in the interactor's preset array that matches the target color.
    /// </summary>
    int FindColorPresetIndex(XRPaintInteractor interactor, Color targetColor)
    {
        Color[] presets = interactor.lineColorPresets;
        if (presets == null) return -1;
        
        for (int i = 0; i < presets.Length; i++)
        {
            // Check if colors match (with small tolerance for floating point)
            if (Mathf.Abs(presets[i].r - targetColor.r) < 0.01f &&
                Mathf.Abs(presets[i].g - targetColor.g) < 0.01f &&
                Mathf.Abs(presets[i].b - targetColor.b) < 0.01f)
            {
                return i;
            }
        }
        return -1;
    }
}
