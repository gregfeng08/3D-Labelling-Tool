using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class OrbitCameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Orbit")]
    [SerializeField] public float distance = 3f;
    [SerializeField] private float xSpeed = 180f;
    [SerializeField] private float ySpeed = 120f;
    [SerializeField] private float yMinLimit = -80f;
    [SerializeField] private float yMaxLimit = 80f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float distanceMax = 2f;
    [SerializeField] private Slider slider;

    [Header("Auto Center")]
    [SerializeField] private bool autoUseAnnotationManagerCenter = true;

    private float x;
    private float y;
    private Vector3 targetOffset = Vector3.zero;

    private void Start()
    {
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;

        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = distance*distanceMax;
            slider.value = distance;
            slider.onValueChanged.AddListener(OnDistanceChanged);
        }
    }

    private void OnDistanceChanged(float value)
    {
        distance = slider.value;
    }

    private void LateUpdate()
    {
        if (AnnotationManager.CurrentState == GameState.START || AnnotationManager.CurrentState == GameState.PAUSED) return;
        
        if (Input.GetMouseButton(1))
        {
            x += Input.GetAxis("Mouse X") * xSpeed * Time.deltaTime;
            y -= Input.GetAxis("Mouse Y") * ySpeed * Time.deltaTime;
            y = ClampAngle(y, yMinLimit, yMaxLimit);
        }

        Vector3 focusPoint;

        if (autoUseAnnotationManagerCenter && AnnotationManager.Inst != null)
            focusPoint = AnnotationManager.Inst.ModelCenterWorld + targetOffset;
        else if (target != null)
            focusPoint = target.position + targetOffset;
        else
            focusPoint = new Vector3(0, 0, 0);

        Quaternion rotation = Quaternion.Euler(y, x, 0);
        Vector3 position = focusPoint - (rotation * Vector3.forward * distance);

        transform.rotation = rotation;
        transform.position = position;
    }

    private static float ClampAngle(float angle, float min, float max)
    {
        while (angle < -360f) angle += 360f;
        while (angle > 360f) angle -= 360f;
        return Mathf.Clamp(angle, min, max);
    }
}