using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public enum GameState
{
    RUNNING,
    PAUSED,
    START
}

public class AnnotationManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] public Transform modelRoot;
    [SerializeField] public Collider targetCollider;
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform annotationUIRoot;
    [SerializeField] private GameObject annotationWorldAnchorPrefab;
    [SerializeField] private AnnotationLabelUI annotationLabelPrefab;

    [Header("Config")]
    [SerializeField] private string modelId = "my_model";
    public string ModelId { get => modelId; set => modelId = value; }
    [SerializeField] private float sidePadding = 220f;
    [SerializeField] private float verticalSpacing = 100f;
    [SerializeField] private float labelOutwardOffset = 120f;
    [SerializeField] private LayerMask occlusionMask = ~0;
    [SerializeField] private float occlusionSurfaceTolerance = 0.03f;

    [Header("Label Scaling")]
    [Tooltip("Lower bound on how far labels are allowed to shrink.")]
    [SerializeField] private float minLabelScale = 0.35f;
    [Tooltip("Annotation count at which labels start shrinking.")]
    [SerializeField] private int labelShrinkStartCount = 6;
    [Tooltip("Pixel gap between the label edge and the end of its leader line.")]
    [SerializeField] private float leaderLineGap = 8f;

    [Header("World Anchor Scaling")]
    [Tooltip("World anchor ball size as a fraction of the loaded model's bounding-box diagonal.")]
    [SerializeField] private float anchorSizeFraction = 0.015f;
    [Tooltip("Fallback anchor scale if model world size cannot be computed.")]
    [SerializeField] private float anchorFallbackScale = 0.1f;

    [Header("Auto Center")]
    [SerializeField] private bool autoComputeModelCenterOnAwake = true;

    [Header("Export")]
    [Tooltip("The longest side of the exported (centered) mesh is rescaled to exactly this size, in meters — larger objects shrink, smaller ones grow. Set this to match the bounding box used in the main project so every imported scan lands at the same standard size.")]
    [SerializeField] private float targetExportDimension = 1f;
    public float TargetExportDimension => targetExportDimension;

    [Header("Prompt UI")]
    [SerializeField] private AnnotationPromptUI annotationPromptUI;

    [Header("Click Settings")]
    [SerializeField] private float clickDragThreshold = 10f;

    [Header("State Descriptor")]
    [SerializeField] private TMP_Text stateText;

    private bool leftMousePressed = false;
    private Vector2 mouseDownPosition;

    private Vector3 pendingWorldPoint;
    private bool hasPendingPoint = false;

    private AnnotationInstance editingAnnotation;
    private AnnotationInstance hoveredAnnotation;

    private readonly List<AnnotationInstance> annotations = new();

    private Vector3 cachedModelCenterLocal = Vector3.zero;
    private bool hasCachedModelCenter = false;

    private float cachedModelWorldSize = 0f;
    private bool hasCachedModelWorldSize = false;

    private Quaternion exportOrientation = Quaternion.identity;
    public Quaternion ExportOrientation => exportOrientation;

    public Vector3 ModelCenterWorld =>
        hasCachedModelCenter ? modelRoot.TransformPoint(cachedModelCenterLocal) : modelRoot.position;

    public Vector3 ModelCenterLocal =>
        hasCachedModelCenter ? cachedModelCenterLocal : Vector3.zero;

    public bool HasCachedModelCenter => hasCachedModelCenter;

    public static AnnotationManager Inst { get; private set; }

    private static GameState currentState;
    public static GameState CurrentState
    {
        get => currentState;
        set
        {
            if (currentState == value) return;
            currentState = value;
            if (Inst != null && Inst.stateText != null)
                Inst.stateText.text = $"Game State: {CurrentState}";
        }
    }

    void Awake()
    {
        if(Inst!=null&&Inst!=this)
        {
            Destroy(gameObject);
            return;
        }
        Inst = this;
        CurrentState = GameState.START;
    }

    private void Update()
    {
        if (CurrentState == GameState.START||CurrentState==GameState.PAUSED) return;

        if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
        {
            TryPlaceAnnotation();
        }

        bool promptOpen = hasPendingPoint || editingAnnotation != null;
        if (hoveredAnnotation != null && !promptOpen)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                BeginEditAnnotation(hoveredAnnotation);
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                RemoveAnnotation(hoveredAnnotation);
            }
        }

        UpdateAnnotationUIPositions();
    }

    public void SetHoveredAnnotation(AnnotationInstance instance)
    {
        if (instance == null) return;
        hoveredAnnotation = instance;
    }

    public void ClearHoveredAnnotation(AnnotationInstance instance)
    {
        if (hoveredAnnotation == instance)
            hoveredAnnotation = null;
    }

    public void ComputeCenters()
    {
        exportOrientation = Quaternion.identity;

        if (autoComputeModelCenterOnAwake)
        {
            ComputeAndCacheModelCenterFromMeshes();
        }
        ComputeAndCacheModelWorldSize();
    }

    public void SetFrontFromCamera()
    {
        if (modelRoot == null || mainCamera == null)
        {
            Debug.LogWarning("AnnotationManager.SetFrontFromCamera: modelRoot or mainCamera missing.");
            return;
        }

        Vector3 center = ModelCenterWorld;
        Vector3 worldDir = mainCamera.transform.position - center;

        // Transform the camera direction into the exporter's root-local frame
        // (includes the OBJ loader's -1 X scale). Yaw-only: project onto the
        // root-local XZ plane so the model stays upright.
        Vector3 localDir = modelRoot.worldToLocalMatrix.MultiplyVector(worldDir);
        localDir.y = 0f;
        if (localDir.sqrMagnitude < 0.0001f)
        {
            Debug.LogWarning("AnnotationManager.SetFrontFromCamera: camera is directly above/below the model; cannot derive a yaw.");
            return;
        }
        localDir.Normalize();

        exportOrientation = Quaternion.FromToRotation(localDir, Vector3.forward);
        Debug.Log($"AnnotationManager: Set Front — export orientation euler = {exportOrientation.eulerAngles}");
    }

    public void ResetExportOrientation()
    {
        exportOrientation = Quaternion.identity;
        Debug.Log("AnnotationManager: Export orientation reset.");
    }

    private void ComputeAndCacheModelWorldSize()
    {
        Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            hasCachedModelWorldSize = false;
            cachedModelWorldSize = 0f;
            return;
        }

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            combined.Encapsulate(renderers[i].bounds);
        }

        cachedModelWorldSize = combined.size.magnitude;
        hasCachedModelWorldSize = cachedModelWorldSize > 0.0001f;
    }

    public bool ComputeAndCacheModelCenterFromMeshes()
    {
        MeshFilter[] meshFilters = modelRoot.GetComponentsInChildren<MeshFilter>();
        SkinnedMeshRenderer[] skinnedMeshes = modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>();

        Vector3 accumulatedWorld = Vector3.zero;
        int totalVertexCount = 0;

        foreach (MeshFilter mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;

            Vector3[] vertices = mf.sharedMesh.vertices;
            Transform t = mf.transform;

            for (int i = 0; i < vertices.Length; i++)
            {
                accumulatedWorld += t.TransformPoint(vertices[i]);
            }

            totalVertexCount += vertices.Length;
        }

        foreach (SkinnedMeshRenderer smr in skinnedMeshes)
        {
            if (smr.sharedMesh == null) continue;

            Vector3[] vertices = smr.sharedMesh.vertices;
            Transform t = smr.transform;

            for (int i = 0; i < vertices.Length; i++)
            {
                accumulatedWorld += t.TransformPoint(vertices[i]);
            }

            totalVertexCount += vertices.Length;
        }

        if (totalVertexCount == 0)
        {
            hasCachedModelCenter = false;
            cachedModelCenterLocal = Vector3.zero;
            Debug.LogWarning("AnnotationManager: No mesh vertices found while computing model center.");
            return false;
        }

        Vector3 averageWorld = accumulatedWorld / totalVertexCount;
        cachedModelCenterLocal = modelRoot.InverseTransformPoint(averageWorld);
        hasCachedModelCenter = true;

        Debug.Log($"AnnotationManager: Computed model center from {totalVertexCount} vertices. World center = {averageWorld}");
        return true;
    }

    private bool TryGetModelScreenBounds(out float minX, out float maxX, out float minY, out float maxY)
    {
        minX = float.MaxValue;
        maxX = float.MinValue;
        minY = float.MaxValue;
        maxY = float.MinValue;

        Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return false;

        bool foundAnyVisiblePoint = false;

        foreach (Renderer r in renderers)
        {
            Bounds b = r.bounds;

            Vector3[] corners = new Vector3[8]
            {
                new Vector3(b.min.x, b.min.y, b.min.z),
                new Vector3(b.min.x, b.min.y, b.max.z),
                new Vector3(b.min.x, b.max.y, b.min.z),
                new Vector3(b.min.x, b.max.y, b.max.z),
                new Vector3(b.max.x, b.min.y, b.min.z),
                new Vector3(b.max.x, b.min.y, b.max.z),
                new Vector3(b.max.x, b.max.y, b.min.z),
                new Vector3(b.max.x, b.max.y, b.max.z)
            };

            foreach (Vector3 corner in corners)
            {
                Vector3 sp = mainCamera.WorldToScreenPoint(corner);

                if (sp.z <= 0f) continue;

                foundAnyVisiblePoint = true;

                minX = Mathf.Min(minX, sp.x);
                maxX = Mathf.Max(maxX, sp.x);
                minY = Mathf.Min(minY, sp.y);
                maxY = Mathf.Max(maxY, sp.y);
            }
        }

        // Include computed model center as a fallback anchor point too.
        if (hasCachedModelCenter)
        {
            Vector3 centerScreen = mainCamera.WorldToScreenPoint(ModelCenterWorld);
            if (centerScreen.z > 0f)
            {
                foundAnyVisiblePoint = true;
                minX = Mathf.Min(minX, centerScreen.x);
                maxX = Mathf.Max(maxX, centerScreen.x);
                minY = Mathf.Min(minY, centerScreen.y);
                maxY = Mathf.Max(maxY, centerScreen.y);
            }
        }

        return foundAnyVisiblePoint;
    }

    private void TryPlaceAnnotation()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (targetCollider.Raycast(ray, out RaycastHit hit, 1000f))
        {
            pendingWorldPoint = hit.point;
            hasPendingPoint = true;

            if (annotationPromptUI != null)
            {
                annotationPromptUI.Open();
            }
            else
            {
                Debug.LogWarning("AnnotationManager: AnnotationPromptUI is not assigned.");
            }
        }
    }

    public void ConfirmPendingAnnotation(string title, string description)
    {
        if (editingAnnotation != null)
        {
            editingAnnotation.data.title = title;
            editingAnnotation.data.description = description;
            editingAnnotation.ui.Setup(title, description);
            editingAnnotation = null;
            return;
        }

        if (!hasPendingPoint) return;

        CreateAnnotation(pendingWorldPoint, title, description);
        hasPendingPoint = false;
    }

    public void CancelPendingAnnotation()
    {
        hasPendingPoint = false;
        editingAnnotation = null;
    }

    public void CreateAnnotation(Vector3 worldPosition, string title, string description)
    {
        Vector3 localPos = modelRoot.InverseTransformPoint(worldPosition);

        AnnotationData data = new AnnotationData
        {
            title = title,
            description = description,
            localPosition = new SerializableVector3(localPos)
        };

        GameObject anchor = annotationWorldAnchorPrefab != null
            ? Instantiate(annotationWorldAnchorPrefab, worldPosition, Quaternion.identity, modelRoot)
            : new GameObject("AnnotationAnchor");

        if (annotationWorldAnchorPrefab == null)
        {
            anchor.transform.SetParent(modelRoot);
            anchor.transform.position = worldPosition;
        }

        ApplyAnchorScale(anchor.transform);

        AnnotationLabelUI ui = Instantiate(annotationLabelPrefab, annotationUIRoot);
        ui.Setup(title, description);
        ui.gameObject.SetActive(true);

        AnnotationInstance instance = new AnnotationInstance
        {
            data = data,
            worldAnchor = anchor,
            ui = ui
        };

        ui.Owner = instance;

        annotations.Add(instance);
    }

    public void BeginEditAnnotation(AnnotationInstance instance)
    {
        if (instance == null) return;

        editingAnnotation = instance;
        hasPendingPoint = false;

        if (annotationPromptUI != null)
        {
            annotationPromptUI.OpenForEdit(instance.data.title, instance.data.description);
        }
        else
        {
            Debug.LogWarning("AnnotationManager: AnnotationPromptUI is not assigned.");
        }
    }

    public void RemoveAnnotation(AnnotationInstance instance)
    {
        if (instance == null) return;

        if (hoveredAnnotation == instance) hoveredAnnotation = null;
        if (editingAnnotation == instance) editingAnnotation = null;

        if (instance.worldAnchor != null) Destroy(instance.worldAnchor);
        if (instance.ui != null) Destroy(instance.ui.gameObject);

        annotations.Remove(instance);
    }

    private void ApplyAnchorScale(Transform anchorTransform)
    {
        float target = hasCachedModelWorldSize
            ? cachedModelWorldSize * anchorSizeFraction
            : anchorFallbackScale;

        // The anchor is parented under modelRoot, which carries a -1 X scale from
        // the OBJ loader. That's a pure reflection so for a sphere the sign of X
        // doesn't matter visually, but we set a positive uniform local scale for
        // clarity. World radius ends up equal to `target`.
        anchorTransform.localScale = new Vector3(target, target, target);
    }

    private float ComputeLabelScale(float modelScreenHeight, bool gotBounds)
    {
        float scale = 1f;

        if (annotations.Count > labelShrinkStartCount)
        {
            scale *= Mathf.Clamp((float)labelShrinkStartCount / annotations.Count, minLabelScale, 1f);
        }

        if (gotBounds)
        {
            // Reference: label should be full-sized when the model fills roughly
            // 70% of screen height; shrinks proportionally for smaller footprints.
            float modelRatio = modelScreenHeight / (Screen.height * 0.7f);
            scale *= Mathf.Clamp(modelRatio, minLabelScale, 1.2f);
        }

        return Mathf.Clamp(scale, minLabelScale, 1.2f);
    }

    private void UpdateAnnotationUIPositions()
    {
        if (annotations.Count == 0) return;

        List<AnnotationInstance> leftSide = new();
        List<AnnotationInstance> rightSide = new();

        foreach (var ann in annotations)
        {
            Vector3 worldPos = modelRoot.TransformPoint(ann.data.localPosition.ToVector3());
            ann.worldAnchor.transform.position = worldPos;

            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

            bool onScreen = IsScreenVisible(screenPos);
            bool occluded = onScreen && IsOccludedFromCamera(worldPos);

            if (!onScreen || occluded)
            {
                ann.ui.gameObject.SetActive(false);
                if (ann.worldAnchor != null)
                    ann.worldAnchor.SetActive(!occluded && onScreen);
                if (hoveredAnnotation == ann) hoveredAnnotation = null;
                continue;
            }

            ann.ui.gameObject.SetActive(true);
            if (ann.worldAnchor != null)
                ann.worldAnchor.SetActive(true);

            if (screenPos.x < Screen.width * 0.5f)
                leftSide.Add(ann);
            else
                rightSide.Add(ann);
        }

        LayoutSide(leftSide, true);
        LayoutSide(rightSide, false);
    }

    private void LayoutSide(List<AnnotationInstance> sideAnnotations, bool left)
    {
        if (sideAnnotations.Count == 0) return;

        sideAnnotations.Sort((a, b) =>
        {
            float ay = mainCamera.WorldToScreenPoint(modelRoot.TransformPoint(a.data.localPosition.ToVector3())).y;
            float by = mainCamera.WorldToScreenPoint(modelRoot.TransformPoint(b.data.localPosition.ToVector3())).y;
            return by.CompareTo(ay);
        });

        float modelMinX, modelMaxX, modelMinY, modelMaxY;
        bool gotBounds = TryGetModelScreenBounds(out modelMinX, out modelMaxX, out modelMinY, out modelMaxY);

        float modelScreenHeight = gotBounds ? (modelMaxY - modelMinY) : 0f;
        float labelScale = ComputeLabelScale(modelScreenHeight, gotBounds);
        float scaledSpacing = verticalSpacing * labelScale;
        float scaledOutwardOffset = labelOutwardOffset * Mathf.Lerp(0.7f, 1f, labelScale);

        float x;
        if (gotBounds)
        {
            x = left ? modelMinX - scaledOutwardOffset : modelMaxX + scaledOutwardOffset;
        }
        else
        {
            x = left ? sidePadding : Screen.width - sidePadding;
        }

        x = Mathf.Clamp(x, 100f, Screen.width - 100f);

        float startY = gotBounds ? modelMaxY : Screen.height * 0.7f;

        for (int i = 0; i < sideAnnotations.Count; i++)
        {
            var ann = sideAnnotations[i];
            RectTransform rt = ann.ui.RectTransform;

            rt.localScale = new Vector3(labelScale, labelScale, 1f);

            Vector3 worldPos = modelRoot.TransformPoint(ann.data.localPosition.ToVector3());
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

            float y = startY - i * scaledSpacing;
            y = Mathf.Clamp(y, 60f, Screen.height - 60f);

            rt.position = new Vector3(x, y, 0f);

            if (ann.ui.LeaderLine != null)
            {
                Vector2 anchorPoint = new Vector2(screenPos.x, screenPos.y);

                // rt.rect.width is in local units; multiply by lossyScale to get the
                // label's actual on-screen half-width, then add a small gap so the
                // leader line terminates just outside the visible label edge.
                float halfWidthScreen = rt.rect.width * 0.5f * rt.lossyScale.x;
                float gap = leaderLineGap * labelScale;

                float labelEdgeX = left
                    ? rt.position.x + halfWidthScreen + gap
                    : rt.position.x - halfWidthScreen - gap;

                Vector2 labelPoint = new Vector2(labelEdgeX, rt.position.y);

                ann.ui.LeaderLine.SetEndpoints(anchorPoint, labelPoint);
            }
        }
    }

    private bool IsOccludedFromCamera(Vector3 anchorWorldPos)
    {
        if (targetCollider == null) return false;

        Vector3 cameraPos = mainCamera.transform.position;
        Vector3 dir = anchorWorldPos - cameraPos;
        float dist = dir.magnitude;

        if (dist <= 0.0001f) return false;

        // Raycast only against the model collider. Using Physics.Raycast here
        // would hit the anchor ball's own SphereCollider first, causing a
        // feedback loop: ball visible -> ray hits ball surface -> reports
        // occluded -> ball disabled -> ray now reaches the model -> reports
        // visible -> ball re-enabled -> repeat (flicker).
        Ray ray = new Ray(cameraPos, dir.normalized);
        if (targetCollider.Raycast(ray, out RaycastHit hit, dist))
        {
            float hitToAnchor = Vector3.Distance(hit.point, anchorWorldPos);

            // If the ray hits the same surface point (or extremely close), treat it as visible.
            // If it hits significantly earlier, the mesh is blocking it.
            return hitToAnchor > occlusionSurfaceTolerance;
        }

        return false;
    }

    public string ExportToJson()
    {
        return ExportToJson(Vector3.zero, Quaternion.identity, 1f);
    }

    public string ExportToJson(Vector3 localPivotOffset, float uniformScale)
    {
        return ExportToJson(localPivotOffset, Quaternion.identity, uniformScale);
    }

    public string ExportToJson(Vector3 localPivotOffset, Quaternion localOrientation, float uniformScale)
    {
        ModelAnnotationExport export = new ModelAnnotationExport
        {
            modelId = modelId
        };

        foreach (var ann in annotations)
        {
            Vector3 original = ann.data.localPosition.ToVector3();
            Vector3 transformed = localOrientation * (original - localPivotOffset) * uniformScale;

            export.annotations.Add(new AnnotationData
            {
                title = ann.data.title,
                description = ann.data.description,
                localPosition = new SerializableVector3(transformed)
            });
        }

        return JsonUtility.ToJson(export, true);
    }

    public void ExportToFile(string fileName = "annotations.json")
    {
        string json = ExportToJson();
        string path = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllText(path, json);
        Debug.Log($"Exported annotations to: {path}");
    }

    public void ExportToAbsolutePath(string absolutePath)
    {
        ExportToAbsolutePath(absolutePath, Vector3.zero, Quaternion.identity, 1f);
    }

    public void ExportToAbsolutePath(string absolutePath, Vector3 localPivotOffset, float uniformScale)
    {
        ExportToAbsolutePath(absolutePath, localPivotOffset, Quaternion.identity, uniformScale);
    }

    public void ExportToAbsolutePath(string absolutePath, Vector3 localPivotOffset, Quaternion localOrientation, float uniformScale)
    {
        string json = ExportToJson(localPivotOffset, localOrientation, uniformScale);
        File.WriteAllText(absolutePath, json);
        Debug.Log($"Exported annotations to: {absolutePath} (pivot={localPivotOffset}, orientationEuler={localOrientation.eulerAngles}, scale={uniformScale})");
    }

    public HashSet<Transform> CollectAnchorTransforms()
    {
        HashSet<Transform> set = new HashSet<Transform>();
        foreach (var ann in annotations)
        {
            if (ann.worldAnchor != null)
                set.Add(ann.worldAnchor.transform);
        }
        return set;
    }

    public int AnnotationCount => annotations.Count;

    public void ImportFromJson(string json)
    {
        ClearAnnotations();

        ModelAnnotationExport import = JsonUtility.FromJson<ModelAnnotationExport>(json);
        if (import == null || import.annotations == null) return;

        foreach (var data in import.annotations)
        {
            Vector3 worldPos = modelRoot.TransformPoint(data.localPosition.ToVector3());
            CreateAnnotation(worldPos, data.title, data.description);
        }
    }

    public void ClearAnnotations()
    {
        foreach (var ann in annotations)
        {
            if (ann.worldAnchor != null) Destroy(ann.worldAnchor);
            if (ann.ui != null) Destroy(ann.ui.gameObject);
        }

        annotations.Clear();
    }

    private bool IsScreenVisible(Vector3 screenPos)
    {
        return screenPos.z > 0f &&
               screenPos.x >= 0f && screenPos.x <= Screen.width &&
               screenPos.y >= 0f && screenPos.y <= Screen.height;
    }
}