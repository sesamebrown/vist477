using UnityEngine;

/// <summary>
/// Helper component that stores a reference to a PaintLine.
/// Attached to collider GameObjects so zones can identify which line triggered them.
/// </summary>
public class PaintLineColliderReference : MonoBehaviour
{
    [HideInInspector]
    public PaintLine paintLine;
}
