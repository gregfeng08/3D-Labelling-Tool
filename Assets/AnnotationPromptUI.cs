using TMPro;
using UnityEngine;

public class AnnotationPromptUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_InputField titleInput;
    [SerializeField] private TMP_InputField descriptionInput;
    [SerializeField] private AnnotationManager annotationManager;

    public void Open()
    {
        panelRoot.SetActive(true);
        titleInput.text = "";
        descriptionInput.text = "";
        titleInput.ActivateInputField();
    }

    public void OpenForEdit(string title, string description)
    {
        panelRoot.SetActive(true);
        titleInput.text = title;
        descriptionInput.text = description;
        titleInput.ActivateInputField();
    }

    public void Close()
    {
        panelRoot.SetActive(false);
    }

    public void Confirm()
    {
        if (annotationManager == null) return;

        string title = titleInput.text.Trim();
        string description = descriptionInput.text.Trim();

        if (string.IsNullOrEmpty(title))
            title = "Untitled";

        annotationManager.ConfirmPendingAnnotation(title, description);
        Close();
    }

    public void Cancel()
    {
        if (annotationManager != null)
            annotationManager.CancelPendingAnnotation();

        Close();
    }
}