using UnityEngine;
using UnityEngine.UI;

public class AnnotationLeaderLine : MonoBehaviour
{
    [SerializeField] private RectTransform lineRect;
    [SerializeField] private Image lineImage;

    public void SetEndpoints(Vector2 a, Vector2 b)
    {
        Vector2 dir = b - a;
        float length = dir.magnitude;

        lineRect.position = (a + b) * 0.5f;

        // sizeDelta is in local units, so if any parent RectTransform applies a
        // non-identity localScale (as the label prefab root does for adaptive
        // sizing), we must divide by lossyScale to get an on-screen line whose
        // visible length exactly matches `length` pixels.
        Vector3 lossy = lineRect.lossyScale;
        float sx = Mathf.Abs(lossy.x) > 0.0001f ? Mathf.Abs(lossy.x) : 1f;
        float sy = Mathf.Abs(lossy.y) > 0.0001f ? Mathf.Abs(lossy.y) : 1f;

        lineRect.sizeDelta = new Vector2(length / sx, 2f / sy);
        lineRect.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }
}