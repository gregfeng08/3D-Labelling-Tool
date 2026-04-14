using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class AnnotationLabelUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private AnnotationLeaderLine leaderLine;

    public RectTransform RectTransform => rectTransform;
    public AnnotationLeaderLine LeaderLine => leaderLine;

    public AnnotationInstance Owner { get; set; }

    public void Setup(string title, string description)
    {
        titleText.text = title;
        descriptionText.text = description;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (AnnotationManager.Inst != null)
            AnnotationManager.Inst.SetHoveredAnnotation(Owner);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (AnnotationManager.Inst != null)
            AnnotationManager.Inst.ClearHoveredAnnotation(Owner);
    }
}
