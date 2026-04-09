using UnityEngine;

public class PaintGameManager : MonoBehaviour
{
    [SerializeField]
    PaintZone[] m_PaintZones;
    int m_CurrentZoneIndex = 0;
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
            m_PaintZones[m_CurrentZoneIndex].gameObject.SetActive(true);
            m_CurrentZoneIndex++;
        }
    }
}
