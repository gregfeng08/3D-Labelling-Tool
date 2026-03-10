using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SerializableVector3
{
    public float x;
    public float y;
    public float z;

    public SerializableVector3() { }

    public SerializableVector3(Vector3 v)
    {
        x = v.x;
        y = v.y;
        z = v.z;
    }

    public Vector3 ToVector3() => new Vector3(x, y, z);
}

[Serializable]
public class AnnotationData
{
    public string title;
    public string description;
    public SerializableVector3 localPosition;
}

[Serializable]
public class ModelAnnotationExport
{
    public string modelId;
    public List<AnnotationData> annotations = new List<AnnotationData>();
}