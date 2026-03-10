using TMPro;
using UnityEngine;

public class AnnotationLabelUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private AnnotationLeaderLine leaderLine;

    public RectTransform RectTransform => rectTransform;
    public AnnotationLeaderLine LeaderLine => leaderLine;

    public void Setup(string title, string description)
    {
        titleText.text = title;
        descriptionText.text = description;
    }
}