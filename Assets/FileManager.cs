using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SimpleFileBrowser;
using Dummiesman;

public class FileManager : MonoBehaviour
{
    public static GameObject currentObj = null;

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

            AnnotationManager.Inst.modelRoot = currentObj.transform;
            AnnotationManager.Inst.targetCollider = mc;
            AnnotationManager.Inst.ClearAnnotations();
            AnnotationManager.Inst.ComputeCenters();

            AnnotationManager.CurrentState = GameState.RUNNING;            

            Debug.Log("Object Created");
        }
    }
}
