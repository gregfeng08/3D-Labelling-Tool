using System.Collections;
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

            MeshFilter mf = loadedObj.GetComponentInChildren<MeshFilter>();
            MeshCollider mc = loadedObj.GetComponent<MeshCollider>();

            if (mc == null)
                mc = loadedObj.AddComponent<MeshCollider>();

            if (mf != null)
            {
                mc.sharedMesh = mf.sharedMesh;
            }

            currentObj = loadedObj;
            currentObjSourcePath = filePaths[0];

            AnnotationManager.Inst.modelRoot = currentObj.transform;
            AnnotationManager.Inst.targetCollider = mc;
            AnnotationManager.Inst.ClearAnnotations();
            AnnotationManager.Inst.ComputeCenters();
            AnnotationManager.Inst.ModelId = Path.GetFileNameWithoutExtension(filePaths[0]);

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
            OBJExporter.Export(currentObj, objPath);
            AnnotationManager.Inst.ExportToAbsolutePath(jsonPath);
            Debug.Log($"FileManager: Exported {objPath} and {jsonPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"FileManager: Export failed - {e.Message}\n{e.StackTrace}");
        }
    }
}
