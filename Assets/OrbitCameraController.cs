using UnityEngine;

public class OrbitCameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Orbit")]
    [SerializeField] private float distance = 3f;
    [SerializeField] private float xSpeed = 180f;
    [SerializeField] private float ySpeed = 120f;
    [SerializeField] private float yMinLimit = -80f;
    [SerializeField] private float yMaxLimit = 80f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minDistance = 0.5f;
    [SerializeField] private float maxDistance = 10f;

    [Header("Auto Center")]
    [SerializeField] private AnnotationManager annotationManager;
    [SerializeField] private bool autoUseAnnotationManagerCenter = true;

    private float x;
    private float y;
    private Vector3 targetOffset = Vector3.zero;

    private void Start()
    {
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;

        if (target == null && annotationManager == null)
        {
            Debug.LogWarning("OrbitCameraController: No target or AnnotationManager assigned.");
        }
    }

    private void LateUpdate()
    {
        // Right mouse drag = orbit
        if (Input.GetMouseButton(1))
        {
            x += Input.GetAxis("Mouse X") * xSpeed * Time.deltaTime;
            y -= Input.GetAxis("Mouse Y") * ySpeed * Time.deltaTime;
            y = ClampAngle(y, yMinLimit, yMaxLimit);
        }

        // Scroll wheel = zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            distance -= scroll * zoomSpeed;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        Vector3 focusPoint;

        if (autoUseAnnotationManagerCenter && annotationManager != null)
            focusPoint = annotationManager.ModelCenterWorld + targetOffset;
        else if (target != null)
            focusPoint = target.position + targetOffset;
        else
            focusPoint = transform.position;

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