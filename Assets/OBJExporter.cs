using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Dummiesman
{
    public static class OBJExporter
    {
        public static void Export(GameObject root, string path)
        {
            Export(root, path, Vector3.zero, Quaternion.identity, 1f, null);
        }

        public static void Export(
            GameObject root,
            string path,
            Vector3 localPivotOffset,
            float uniformScale,
            HashSet<Transform> excludeSubtreeRoots)
        {
            Export(root, path, localPivotOffset, Quaternion.identity, uniformScale, excludeSubtreeRoots);
        }

        public static void Export(
            GameObject root,
            string path,
            Vector3 localPivotOffset,
            Quaternion localOrientation,
            float uniformScale,
            HashSet<Transform> excludeSubtreeRoots)
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
                sw.WriteLine(string.Format(ci, "# pivot offset: {0} {1} {2}", localPivotOffset.x, localPivotOffset.y, localPivotOffset.z));
                sw.WriteLine(string.Format(ci, "# uniform scale: {0}", uniformScale));
                Vector3 eul = localOrientation.eulerAngles;
                sw.WriteLine(string.Format(ci, "# orientation euler: {0} {1} {2}", eul.x, eul.y, eul.z));

                int offset = 0;

                foreach (MeshFilter mf in filters)
                {
                    Mesh mesh = mf.sharedMesh;
                    if (mesh == null) continue;
                    if (IsUnderExcludedRoot(mf.transform, excludeSubtreeRoots)) continue;

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
                        Vector3 vRootLocal = meshToRootLocal.MultiplyPoint3x4(verts[i]);
                        Vector3 v = localOrientation * (vRootLocal - localPivotOffset) * uniformScale;
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
                            n = localOrientation * n;
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

        public static bool TryComputeRootLocalBounds(
            GameObject root,
            HashSet<Transform> excludeSubtreeRoots,
            out Bounds bounds)
        {
            bounds = new Bounds();
            if (root == null) return false;

            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>();
            if (filters.Length == 0) return false;

            Matrix4x4 rootWorldToLocal = root.transform.worldToLocalMatrix;

            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            bool any = false;

            foreach (MeshFilter mf in filters)
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null) continue;
                if (IsUnderExcludedRoot(mf.transform, excludeSubtreeRoots)) continue;

                Vector3[] verts = mesh.vertices;
                Matrix4x4 meshToRootLocal = rootWorldToLocal * mf.transform.localToWorldMatrix;

                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 p = meshToRootLocal.MultiplyPoint3x4(verts[i]);
                    if (p.x < min.x) min.x = p.x;
                    if (p.y < min.y) min.y = p.y;
                    if (p.z < min.z) min.z = p.z;
                    if (p.x > max.x) max.x = p.x;
                    if (p.y > max.y) max.y = p.y;
                    if (p.z > max.z) max.z = p.z;
                    any = true;
                }
            }

            if (!any) return false;

            bounds = new Bounds((min + max) * 0.5f, max - min);
            return true;
        }

        static bool IsUnderExcludedRoot(Transform t, HashSet<Transform> excludeSubtreeRoots)
        {
            if (excludeSubtreeRoots == null || excludeSubtreeRoots.Count == 0) return false;
            Transform cur = t;
            while (cur != null)
            {
                if (excludeSubtreeRoots.Contains(cur)) return true;
                cur = cur.parent;
            }
            return false;
        }
    }
}
