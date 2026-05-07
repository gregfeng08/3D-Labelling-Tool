using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SimpleFileBrowser;
using Dummiesman;

public class FileManager : MonoBehaviour
{
    public static GameObject currentObj = null;
    public static string currentObjSourcePath = null;

    public void LoadFile()
    {
        AnnotationManager.CurrentState = GameState.PAUSED;
        FileBrowser.SetFilters(true, new FileBrowser.Filter("OBJ Files", ".obj"));
        FileBrowser.SetDefaultFilter(".obj");
        StartCoroutine(ShowLoadDialogueCoroutine());
    }

    IEnumerator ShowLoadDialogueCoroutine()
    {
        yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Files, false, null, null, "Select Files", "Load");

        Debug.Log(FileBrowser.Success);

        if(FileBrowser.Success)
        {
            OnFilesSelected(FileBrowser.Result);
        }
        else
        {
            // User cancelled; restore previous state.
            AnnotationManager.CurrentState = currentObj != null ? GameState.RUNNING : GameState.START;
        }
    }

    void OnFilesSelected(string[] filePaths)
    {
        if(filePaths.Length>0)
        {
            Debug.Log($"Selected File:{filePaths[0]}");
            Debug.Log("Loading File...");

            GameObject loadedObj = new OBJLoader().Load(filePaths[0]);
            if(currentObj!=null)
            {
                Debug.Log($"Attempting to Destroy:{currentObj.name}");
                Destroy(currentObj);
            }

            currentObj = loadedObj;
            currentObjSourcePath = filePaths[0];

            FixNegativeScale(loadedObj);
            NormalizeModelScale(loadedObj);
            SetupMeshColliders(loadedObj);

            AnnotationManager.Inst.modelRoot = currentObj.transform;
            AnnotationManager.Inst.ClearAnnotations();

            AnnotationManager.Inst.ComputeCenters();
            AnnotationManager.Inst.ModelId = Path.GetFileNameWithoutExtension(filePaths[0]);

            OrbitCameraController cam = FindObjectOfType<OrbitCameraController>();
            if (cam != null)
                cam.ResetForModelSize(1f);

            AnnotationManager.CurrentState = GameState.RUNNING;

            Debug.Log("Object Created");
        }
    }

    public void ImportAnnotations()
    {
        if (currentObj == null)
        {
            Debug.LogWarning("FileManager: Load an OBJ before importing annotations.");
            return;
        }

        AnnotationManager.CurrentState = GameState.PAUSED;

        string initialDir = null;
        string initialName = null;
        if (!string.IsNullOrEmpty(currentObjSourcePath))
        {
            initialDir = Path.GetDirectoryName(currentObjSourcePath);
            initialName = Path.GetFileNameWithoutExtension(currentObjSourcePath) + "_annotations.json";
        }

        FileBrowser.SetFilters(true, new FileBrowser.Filter("JSON Files", ".json"));
        FileBrowser.SetDefaultFilter(".json");
        StartCoroutine(ShowImportAnnotationsDialogueCoroutine(initialDir, initialName));
    }

    IEnumerator ShowImportAnnotationsDialogueCoroutine(string initialPath, string initialFilename)
    {
        yield return FileBrowser.WaitForLoadDialog(
            FileBrowser.PickMode.Files,
            false,
            initialPath,
            initialFilename,
            "Import Annotations",
            "Load");

        if (FileBrowser.Success && FileBrowser.Result != null && FileBrowser.Result.Length > 0)
        {
            try
            {
                string json = File.ReadAllText(FileBrowser.Result[0]);
                AnnotationManager.Inst.ImportFromJson(json);
                Debug.Log($"FileManager: Imported annotations from {FileBrowser.Result[0]}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"FileManager: Annotation import failed - {e.Message}\n{e.StackTrace}");
            }
        }

        AnnotationManager.CurrentState = currentObj != null ? GameState.RUNNING : GameState.START;
    }

    public void ExportFile()
    {
        if (currentObj == null)
        {
            Debug.LogWarning("FileManager: No object loaded, nothing to export.");
            return;
        }

        AnnotationManager.CurrentState = GameState.PAUSED;

        string initialDir = null;
        string initialName = "model";
        if (!string.IsNullOrEmpty(currentObjSourcePath))
        {
            initialDir = Path.GetDirectoryName(currentObjSourcePath);
            initialName = Path.GetFileNameWithoutExtension(currentObjSourcePath);
        }

        FileBrowser.SetFilters(true, new FileBrowser.Filter("OBJ Files", ".obj"));
        FileBrowser.SetDefaultFilter(".obj");
        StartCoroutine(ShowSaveDialogueCoroutine(initialDir, initialName + "_localized.obj"));
    }

    IEnumerator ShowSaveDialogueCoroutine(string initialPath, string initialFilename)
    {
        yield return FileBrowser.WaitForSaveDialog(
            FileBrowser.PickMode.Files,
            false,
            initialPath,
            initialFilename,
            "Export Localized OBJ",
            "Save");

        if (FileBrowser.Success && FileBrowser.Result != null && FileBrowser.Result.Length > 0)
        {
            WriteExport(FileBrowser.Result[0]);
        }

        AnnotationManager.CurrentState = currentObj != null ? GameState.RUNNING : GameState.START;
    }

    static void FixNegativeScale(GameObject obj)
    {
        Vector3 s = obj.transform.localScale;
        bool flipX = s.x < 0;
        bool flipY = s.y < 0;
        bool flipZ = s.z < 0;

        if (!flipX && !flipY && !flipZ) return;

        int flipCount = (flipX ? 1 : 0) + (flipY ? 1 : 0) + (flipZ ? 1 : 0);
        bool reverseWinding = (flipCount % 2) == 1;

        foreach (MeshFilter mf in obj.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;

            Mesh mesh = Object.Instantiate(mf.sharedMesh);
            Vector3[] verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                if (flipX) verts[i].x = -verts[i].x;
                if (flipY) verts[i].y = -verts[i].y;
                if (flipZ) verts[i].z = -verts[i].z;
            }
            mesh.vertices = verts;

            if (reverseWinding)
            {
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    int[] tris = mesh.GetTriangles(sub);
                    for (int i = 0; i < tris.Length; i += 3)
                    {
                        int tmp = tris[i];
                        tris[i] = tris[i + 1];
                        tris[i + 1] = tmp;
                    }
                    mesh.SetTriangles(tris, sub);
                }
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
        }

        obj.transform.localScale = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        Debug.Log($"FileManager: Fixed negative scale — original=({s.x},{s.y},{s.z})");
    }

    static void SetupMeshColliders(GameObject obj)
    {
        foreach (MeshFilter mf in obj.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            MeshCollider mc = mf.gameObject.GetComponent<MeshCollider>();
            if (mc == null)
                mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
        }
    }

    static void NormalizeModelScale(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combined.Encapsulate(renderers[i].bounds);

        float maxDim = Mathf.Max(combined.size.x, Mathf.Max(combined.size.y, combined.size.z));
        if (maxDim < 0.0001f) return;

        float scaleFactor = 1f / maxDim;
        obj.transform.localScale *= scaleFactor;

        Debug.Log($"FileManager: Normalized model scale — worldBounds={combined.size}, factor={scaleFactor}, newScale={obj.transform.localScale}");
    }

    void WriteExport(string chosenPath)
    {
        // Normalize chosenPath to always end in .obj, and derive the annotations path
        // from the same stem so the two files always stay paired.
        string dir = Path.GetDirectoryName(chosenPath);
        string fileName = Path.GetFileName(chosenPath);
        string stem = Path.GetFileNameWithoutExtension(fileName);

        if (string.IsNullOrEmpty(stem))
        {
            Debug.LogWarning("FileManager: Invalid export path.");
            return;
        }

        // If the user kept the suggested "..._localized" suffix, strip it from the
        // annotation stem so the JSON companion is "{base}_annotations.json" rather
        // than "{base}_localized_annotations.json".
        string annotationStem = stem.EndsWith("_localized")
            ? stem.Substring(0, stem.Length - "_localized".Length)
            : stem;

        string objPath = Path.Combine(dir, stem + ".obj");
        string jsonPath = Path.Combine(dir, annotationStem + "_annotations.json");

        try
        {
            var mgr = AnnotationManager.Inst;

            HashSet<Transform> exclude = mgr != null
                ? mgr.CollectAnchorTransforms()
                : new HashSet<Transform>();

            Vector3 pivot = (mgr != null && mgr.HasCachedModelCenter)
                ? mgr.ModelCenterLocal
                : Vector3.zero;

            float uniformScale = 1f;
            if (mgr != null && OBJExporter.TryComputeRootLocalBounds(currentObj, exclude, out Bounds meshBounds))
            {
                float maxDim = Mathf.Max(meshBounds.size.x, Mathf.Max(meshBounds.size.y, meshBounds.size.z));
                if (maxDim > 0.0001f)
                    uniformScale = mgr.TargetExportDimension / maxDim;

                Debug.Log($"FileManager: mesh bounds size={meshBounds.size}, maxDim={maxDim}, target={mgr.TargetExportDimension}, uniformScale={uniformScale}");
            }
            else
            {
                Debug.LogWarning("FileManager: failed to compute mesh bounds for export scale; falling back to 1.0.");
            }

            Quaternion orientation = mgr != null ? mgr.ExportOrientation : Quaternion.identity;

            OBJExporter.Export(currentObj, objPath, pivot, orientation, uniformScale, exclude);
            if (mgr != null)
                mgr.ExportToAbsolutePath(jsonPath, pivot, orientation, uniformScale);
            Debug.Log($"FileManager: Exported {objPath} and {jsonPath} (orientationEuler={orientation.eulerAngles})");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"FileManager: Export failed - {e.Message}\n{e.StackTrace}");
        }
    }
}
