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
        lineRect.sizeDelta = new Vector2(length, 2f);
        lineRect.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }
}