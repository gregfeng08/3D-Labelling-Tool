using System.Globalization;
using System.IO;
using UnityEngine;

namespace Dummiesman
{
    /// <summary>
    /// Minimal OBJ writer that serializes a loaded model GameObject (as produced by
    /// OBJLoader) back out to a Wavefront .obj file. Vertices are emitted in the
    /// supplied root's local space, which matches the coordinate system the
    /// Dummiesman loader uses on re-import (outer root carries a -1 X scale, child
    /// transforms are identity), giving a round-trip-safe result.
    /// </summary>
    public static class OBJExporter
    {
        public static void Export(GameObject root, string path)
        {
            if (root == null)
            {
                Debug.LogWarning("OBJExporter: root is null, nothing to export.");
                return;
            }

            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>();
            if (filters.Length == 0)
            {
                Debug.LogWarning("OBJExporter: no MeshFilters found on root.");
                return;
            }

            CultureInfo ci = CultureInfo.InvariantCulture;
            Matrix4x4 rootWorldToLocal = root.transform.worldToLocalMatrix;

            using (StreamWriter sw = new StreamWriter(path))
            {
                sw.NewLine = "\n";
                sw.WriteLine("# Exported by ObjLabelTool");

                int offset = 0;

                foreach (MeshFilter mf in filters)
                {
                    Mesh mesh = mf.sharedMesh;
                    if (mesh == null) continue;

                    Vector3[] verts = mesh.vertices;
                    Vector3[] norms = mesh.normals;
                    Vector2[] uvs = mesh.uv;

                    bool hasNormals = norms != null && norms.Length == verts.Length;
                    bool hasUvs = uvs != null && uvs.Length == verts.Length;

                    Matrix4x4 meshLocalToWorld = mf.transform.localToWorldMatrix;
                    Matrix4x4 meshToRootLocal = rootWorldToLocal * meshLocalToWorld;

                    sw.WriteLine("o " + mf.gameObject.name);

                    for (int i = 0; i < verts.Length; i++)
                    {
                        Vector3 v = meshToRootLocal.MultiplyPoint3x4(verts[i]);
                        sw.WriteLine(string.Format(ci, "v {0} {1} {2}", v.x, v.y, v.z));
                    }

                    if (hasUvs)
                    {
                        for (int i = 0; i < uvs.Length; i++)
                        {
                            sw.WriteLine(string.Format(ci, "vt {0} {1}", uvs[i].x, uvs[i].y));
                        }
                    }

                    if (hasNormals)
                    {
                        for (int i = 0; i < norms.Length; i++)
                        {
                            Vector3 n = meshToRootLocal.MultiplyVector(norms[i]).normalized;
                            sw.WriteLine(string.Format(ci, "vn {0} {1} {2}", n.x, n.y, n.z));
                        }
                    }

                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        int[] tris = mesh.GetTriangles(sub);
                        for (int i = 0; i < tris.Length; i += 3)
                        {
                            int a = tris[i] + 1 + offset;
                            int b = tris[i + 1] + 1 + offset;
                            int c = tris[i + 2] + 1 + offset;

                            if (hasUvs && hasNormals)
                            {
                                sw.WriteLine(string.Format(ci,
                                    "f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}", a, b, c));
                            }
                            else if (hasNormals)
                            {
                                sw.WriteLine(string.Format(ci,
                                    "f {0}//{0} {1}//{1} {2}//{2}", a, b, c));
                            }
                            else if (hasUvs)
                            {
                                sw.WriteLine(string.Format(ci,
                                    "f {0}/{0} {1}/{1} {2}/{2}", a, b, c));
                            }
                            else
                            {
                                sw.WriteLine(string.Format(ci,
                                    "f {0} {1} {2}", a, b, c));
                            }
                        }
                    }

                    offset += verts.Length;
                }
            }

            Debug.Log("OBJExporter: wrote " + path);
        }
    }
}
