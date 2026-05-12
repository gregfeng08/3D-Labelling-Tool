using UnityEngine;
using TMPro;

public class BoundingBoxGizmo : MonoBehaviour
{
    [SerializeField] private AnnotationManager annotationManager;

    [Header("Rotation Inputs")]
    [SerializeField] private TMP_InputField rotXInput;
    [SerializeField] private TMP_InputField rotYInput;
    [SerializeField] private TMP_InputField rotZInput;

    [Header("Appearance")]
    [SerializeField] private Color wireColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private Color frontFaceColor = new Color(0f, 0.5f, 1f, 0.25f);
    [SerializeField] private Color frontEdgeColor = new Color(0f, 0.7f, 1f, 0.9f);

    private Material glMaterial;
    private Quaternion boxRotation = Quaternion.identity;
    private Bounds rootLocalBounds;
    private bool hasBounds;
    private Matrix4x4 baseWorldToLocal;

    private static readonly int[,] Edges = {
        {0,1}, {1,3}, {3,2}, {2,0},
        {4,5}, {5,7}, {7,6}, {6,4},
        {0,4}, {1,5}, {2,6}, {3,7}
    };

    private static readonly int[] FrontFace = { 4, 5, 7, 6 };

    private static readonly int[,] FrontEdges = {
        {4,5}, {5,7}, {7,6}, {6,4}
    };

    private void Awake()
    {
        if (rotXInput != null)
        {
            rotXInput.contentType = TMP_InputField.ContentType.DecimalNumber;
            rotXInput.onValueChanged.AddListener((_) => OnRotationInputChanged());
        }
        if (rotYInput != null)
        {
            rotYInput.contentType = TMP_InputField.ContentType.DecimalNumber;
            rotYInput.onValueChanged.AddListener((_) => OnRotationInputChanged());
        }
        if (rotZInput != null)
        {
            rotZInput.contentType = TMP_InputField.ContentType.DecimalNumber;
            rotZInput.onValueChanged.AddListener((_) => OnRotationInputChanged());
        }
    }

    private void OnRotationInputChanged()
    {
        float x = ParseField(rotXInput, 0f);
        float y = ParseField(rotYInput, 0f);
        float z = ParseField(rotZInput, 0f);

        boxRotation = Quaternion.Euler(x, y, z);
        SyncOrientation();
    }

    private static float ParseField(TMP_InputField field, float fallback)
    {
        if (field == null) return fallback;
        if (float.TryParse(field.text, out float val)) return val;
        return fallback;
    }

    private void UpdateInputFields()
    {
        Vector3 euler = boxRotation.eulerAngles;
        if (rotXInput != null && !rotXInput.isFocused)
            rotXInput.text = euler.x.ToString("0.##");
        if (rotYInput != null && !rotYInput.isFocused)
            rotYInput.text = euler.y.ToString("0.##");
        if (rotZInput != null && !rotZInput.isFocused)
            rotZInput.text = euler.z.ToString("0.##");
    }

    public void ComputeBounds()
    {
        hasBounds = false;
        if (annotationManager == null || annotationManager.modelRoot == null) return;

        Transform root = annotationManager.modelRoot;

        // Store the base (unrotated) worldToLocal for SnapToCamera
        baseWorldToLocal = root.worldToLocalMatrix;

        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>();
        if (meshFilters.Length == 0) return;

        Matrix4x4 rootWorldToLocal = root.worldToLocalMatrix;
        Vector3 min = Vector3.one * float.PositiveInfinity;
        Vector3 max = Vector3.one * float.NegativeInfinity;
        bool any = false;

        foreach (MeshFilter mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;
            Vector3[] verts = mf.sharedMesh.vertices;
            Matrix4x4 meshToRoot = rootWorldToLocal * mf.transform.localToWorldMatrix;

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 p = meshToRoot.MultiplyPoint3x4(verts[i]);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
                any = true;
            }
        }

        if (!any) return;

        rootLocalBounds = new Bounds((min + max) * 0.5f, max - min);
        hasBounds = true;
    }

    public void ResetRotation()
    {
        boxRotation = Quaternion.identity;
        UpdateInputFields();
        SyncOrientation();
    }

    private void SyncOrientation()
    {
        if (annotationManager == null) return;

        annotationManager.SetExportOrientation(boxRotation);

        if (annotationManager.modelRoot != null)
            annotationManager.modelRoot.localRotation = boxRotation;
    }

    private void EnsureMaterial()
    {
        if (glMaterial != null) return;
        glMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
        glMaterial.hideFlags = HideFlags.HideAndDontSave;
        glMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        glMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        glMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        glMaterial.SetInt("_ZWrite", 0);
        glMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
    }

    private Vector3[] GetWorldCorners()
    {
        Matrix4x4 baseLocalToWorld = baseWorldToLocal.inverse;

        // Recompute axis-aligned bounds that contain the rotated original box
        Vector3 center = rootLocalBounds.center;
        Vector3 he = rootLocalBounds.extents;

        Vector3 min = Vector3.one * float.PositiveInfinity;
        Vector3 max = Vector3.one * float.NegativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            Vector3 orig = new Vector3(
                ((i & 1) == 0) ? -he.x : he.x,
                ((i & 2) == 0) ? -he.y : he.y,
                ((i & 4) == 0) ? -he.z : he.z
            );
            Vector3 rotated = boxRotation * (center + orig);
            min = Vector3.Min(min, rotated);
            max = Vector3.Max(max, rotated);
        }

        Vector3 newCenter = (min + max) * 0.5f;
        Vector3 newHe = (max - min) * 0.5f;

        Vector3[] corners = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            Vector3 offset = new Vector3(
                ((i & 1) == 0) ? -newHe.x : newHe.x,
                ((i & 2) == 0) ? -newHe.y : newHe.y,
                ((i & 4) == 0) ? -newHe.z : newHe.z
            );
            corners[i] = baseLocalToWorld.MultiplyPoint3x4(newCenter + offset);
        }
        return corners;
    }

    private void OnRenderObject()
    {
        if (!hasBounds) return;
        if (annotationManager == null || annotationManager.modelRoot == null) return;

        EnsureMaterial();
        glMaterial.SetPass(0);

        Vector3[] c = GetWorldCorners();

        GL.Begin(GL.LINES);
        GL.Color(wireColor);
        for (int e = 0; e < 12; e++)
        {
            int i0 = Edges[e, 0], i1 = Edges[e, 1];
            if (IsFrontEdge(i0, i1)) continue;
            GL.Vertex(c[i0]);
            GL.Vertex(c[i1]);
        }
        GL.End();

        GL.Begin(GL.LINES);
        GL.Color(frontEdgeColor);
        for (int e = 0; e < 4; e++)
        {
            GL.Vertex(c[FrontEdges[e, 0]]);
            GL.Vertex(c[FrontEdges[e, 1]]);
        }
        GL.End();

        GL.Begin(GL.QUADS);
        GL.Color(frontFaceColor);
        GL.Vertex(c[FrontFace[0]]);
        GL.Vertex(c[FrontFace[1]]);
        GL.Vertex(c[FrontFace[2]]);
        GL.Vertex(c[FrontFace[3]]);
        GL.End();
    }

    private static bool IsFrontEdge(int a, int b)
    {
        for (int i = 0; i < 4; i++)
        {
            if ((FrontEdges[i, 0] == a && FrontEdges[i, 1] == b) ||
                (FrontEdges[i, 0] == b && FrontEdges[i, 1] == a))
                return true;
        }
        return false;
    }
}
