using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AnnotationPromptUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_InputField titleInput;
    [SerializeField] private TMP_InputField descriptionInput;
    [SerializeField] private AnnotationManager annotationManager;

    [Header("Buttons")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private void Awake()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(Confirm);
            confirmButton.onClick.AddListener(Confirm);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(Cancel);
            cancelButton.onClick.AddListener(Cancel);
        }
    }

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
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    public bool IsOpen()
    {
        return panelRoot != null && panelRoot.activeSelf;
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