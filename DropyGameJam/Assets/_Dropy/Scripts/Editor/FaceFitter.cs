using System.Collections.Generic;
using System.IO;
using Dropy.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Dropy.EditorTools
{
    /// <summary>
    /// Karakter yüzlerini kurar: ortak Face prefab'ına nokta gözleri ekler, sonra her karakterde kaş, ağız ve gözleri
    /// o karakterin kafa modelinin yüzeyine oturtur (cildin içine gömülmesin, havada da durmasın).
    /// Yeni karakter eklenince ya da yüz parçaları elle kaydırılınca menüden yeniden çalıştırılır.
    /// </summary>
    public static class FaceFitter
    {
        const string CharacterFolder = "Assets/_Dropy/Prefabs/Characters";
        const string FacePrefab = CharacterFolder + "/Parts/Face.prefab";
        const string EyeMeshPath = "Assets/_Dropy/Art/Models/Face/EyeDot.asset";
        /// <summary>İnce parçanın (göz, ağız) en arka noktası ile cilt arasındaki boşluk (metre).</summary>
        const float Clearance = 0.0015f;
        /// <summary>Bundan derin parçalar (kaş bloğu) arkadan cilde gömülebilir; yalnız ön yüzü bu kadar önde durur (metre).</summary>
        const float ThickPart = 0.01f, ThickRelief = 0.004f;
        /// <summary>Bundan ince katmanlar (gözlük çerçevesi, saç tutamı) cilt sayılmaz, arkasına bakılır (metre).</summary>
        const float MinSkinThickness = 0.025f;
        /// <summary>Göz, kaştan ağıza olan mesafenin bu oranı kadar kaşın altında durur.</summary>
        const float EyeDrop = 0.28f;
        const float EyeWidth = 0.0048f, EyeHeight = 0.0058f, EyeBulge = 0.0025f;
        const int EyeSegments = 10;

        static readonly string[] Parts = { "Brow_L", "Brow_R", "Mouth", "Eye_L", "Eye_R" };

        [MenuItem("Dropy/Karakterler/Yüz Parçalarını Oturt")]
        public static void FitAll()
        {
            EnsureEyes();
            var report = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { CharacterFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == FacePrefab)
                    continue;
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset.GetComponentInChildren<FaceController>(true) == null)
                    continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    report.Add($"{Path.GetFileNameWithoutExtension(path)}: {Fit(root)}");
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log("Yüz parçaları oturtuldu (z kayması, mm):\n" + string.Join("\n", report));
        }

        // ---------------------------------------------------------------- gözler

        /// <summary>Face prefab'ında göz yoksa kaşın kopyasından üretir (aynı malzeme ve gölge ayarı), FaceController'a bağlar.</summary>
        static void EnsureEyes()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(EyeMeshPath);
            if (mesh == null)
            {
                mesh = EyeMesh();
                AssetDatabase.CreateAsset(mesh, EyeMeshPath);
            }

            var root = PrefabUtility.LoadPrefabContents(FacePrefab);
            try
            {
                var face = root.GetComponent<FaceController>();
                var so = new SerializedObject(face);
                foreach (var side in new[] { "L", "R" })
                {
                    var eye = root.transform.Find("Eye_" + side);
                    if (eye == null)
                    {
                        var brow = root.transform.Find("Brow_" + side);
                        eye = Object.Instantiate(brow.gameObject, root.transform).transform;
                        eye.name = "Eye_" + side;
                        eye.localRotation = Quaternion.identity;
                        eye.localScale = Vector3.one;
                        eye.localPosition = new Vector3(brow.localPosition.x, -0.029f, 0f);
                        eye.GetComponent<MeshFilter>().sharedMesh = mesh;
                    }
                    so.FindProperty(side == "L" ? "leftEye" : "rightEye").objectReferenceValue = eye;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, FacePrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Hafif kabarık, köşeli oval nokta; ön yüzü +z'ye (yüzün dışına) bakar.</summary>
        static Mesh EyeMesh()
        {
            var vertices = new List<Vector3>();
            var front = new Vector3(0f, 0f, EyeBulge);
            var back = new Vector3(0f, 0f, -0.0005f);
            for (int i = 0; i < EyeSegments; i++)
            {
                var a = Rim(i);
                var b = Rim(i + 1);
                vertices.Add(front); vertices.Add(a); vertices.Add(b);
                vertices.Add(back); vertices.Add(b); vertices.Add(a);
            }
            var triangles = new int[vertices.Count];
            for (int i = 0; i < triangles.Length; i++)
                triangles[i] = i;
            var mesh = new Mesh { name = "EyeDot" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Vector3 Rim(int i)
        {
            float angle = i * Mathf.PI * 2f / EyeSegments;
            return new Vector3(Mathf.Cos(angle) * EyeWidth, Mathf.Sin(angle) * EyeHeight, 0f);
        }

        // ---------------------------------------------------------------- yerleştirme

        /// <summary>Gözleri kaşın altına dizer, sonra her parçayı yüzün ileri ekseninde cilde yaslar. Dönen metin kaymalardır.</summary>
        static string Fit(GameObject root)
        {
            var face = root.GetComponentInChildren<FaceController>(true).transform;
            var mouth = face.Find("Mouth");
            foreach (var side in new[] { "L", "R" })
            {
                var brow = face.Find("Brow_" + side);
                var eye = face.Find("Eye_" + side);
                if (brow == null || eye == null || mouth == null)
                    continue;
                float y = brow.localPosition.y + (mouth.localPosition.y - brow.localPosition.y) * EyeDrop;
                eye.localPosition = new Vector3(brow.localPosition.x, y, brow.localPosition.z);
            }

            var skin = SkinTriangles(root, face);
            float scale = face.lossyScale.z;
            var shifts = new List<string>();
            foreach (var name in Parts)
            {
                var part = face.Find(name);
                if (part == null)
                    continue;
                float? shift = Shift(part, face, skin, scale);
                if (shift == null)
                {
                    shifts.Add($"{name} ıska");
                    continue;
                }
                part.localPosition += new Vector3(0f, 0f, shift.Value);
                shifts.Add($"{name} {shift.Value * scale * 1000f:+0.0;-0.0}");
            }
            return string.Join(", ", shifts);
        }

        /// <summary>
        /// Parçanın her köşesinin arkasındaki cilt derinliği bulunur; en çok gömülen (ya da en az boşlukta kalan) köşe
        /// tam boşluk kadar önde kalacak z kayması döner (Face yerel ölçüsünde). Kalın parçada yalnız ön yarının köşelerine bakılır.
        /// </summary>
        static float? Shift(Transform part, Transform face, List<Triangle> skin, float faceScale)
        {
            float minThickness = MinSkinThickness / faceScale;
            float? need = null;
            foreach (var filter in part.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null)
                    continue;
                var toFace = face.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                float depth = mesh.bounds.size.z * Mathf.Abs(filter.transform.lossyScale.z);
                bool thick = depth > ThickPart;
                float clearance = (thick ? ThickRelief : Clearance) / faceScale;
                foreach (var v in mesh.vertices)
                {
                    if (thick && v.z < mesh.bounds.center.z)
                        continue;
                    var p = toFace.MultiplyPoint3x4(v);
                    float? skinZ = SkinDepth(skin, p.x, p.y, minThickness);
                    if (skinZ == null)
                        continue;
                    float n = skinZ.Value + clearance - p.z;
                    if (need == null || n > need.Value)
                        need = n;
                }
            }
            return need;
        }

        struct Triangle
        {
            public Vector3 a, b, c;
            public bool front;
            public float minX, maxX, minY, maxY;
        }

        /// <summary>Karakterin bütün gövde/kafa üçgenleri, Face'in yerel uzayında (yüz parçaları hariç).</summary>
        static List<Triangle> SkinTriangles(GameObject root, Transform face)
        {
            var list = new List<Triangle>();
            var toFace = face.worldToLocalMatrix;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.transform.IsChildOf(face))
                    continue;
                Mesh mesh;
                Matrix4x4 matrix;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    mesh = new Mesh();
                    skinned.BakeMesh(mesh, true);
                    matrix = toFace * Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);
                }
                else if (renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                {
                    mesh = filter.sharedMesh;
                    matrix = toFace * renderer.transform.localToWorldMatrix;
                }
                else
                    continue;

                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                var indices = mesh.triangles;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var t = new Triangle { a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]] };
                    t.front = Vector3.Cross(t.b - t.a, t.c - t.a).z > 0f;
                    t.minX = Mathf.Min(t.a.x, t.b.x, t.c.x);
                    t.maxX = Mathf.Max(t.a.x, t.b.x, t.c.x);
                    t.minY = Mathf.Min(t.a.y, t.b.y, t.c.y);
                    t.maxY = Mathf.Max(t.a.y, t.b.y, t.c.y);
                    list.Add(t);
                }
                if (renderer is SkinnedMeshRenderer)
                    Object.DestroyImmediate(mesh);
            }
            return list;
        }

        /// <summary>
        /// (x, y)'den -z yönüne giden ışının değdiği ilk kalın yüzey: önden girilip en az <paramref name="minThickness"/>
        /// sonra çıkılan yüzey cilttir; daha incesi (gözlük, saç tutamı) atlanır.
        /// </summary>
        static float? SkinDepth(List<Triangle> skin, float x, float y, float minThickness)
        {
            var hits = new List<KeyValuePair<float, bool>>();
            foreach (var t in skin)
            {
                if (x < t.minX || x > t.maxX || y < t.minY || y > t.maxY)
                    continue;
                if (ZAt(t, x, y, out float z))
                    hits.Add(new KeyValuePair<float, bool>(z, t.front));
            }
            hits.Sort((p, q) => q.Key.CompareTo(p.Key));
            for (int i = 0; i < hits.Count; i++)
            {
                if (!hits[i].Value)
                    continue;
                float exit = float.NegativeInfinity;
                for (int j = i + 1; j < hits.Count; j++)
                    if (!hits[j].Value)
                    {
                        exit = hits[j].Key;
                        break;
                    }
                if (hits[i].Key - exit >= minThickness)
                    return hits[i].Key;
            }
            return null;
        }

        /// <summary>Üçgen (x, y) noktasını xy düzleminde kapsıyorsa o noktadaki z'yi verir.</summary>
        static bool ZAt(Triangle t, float x, float y, out float z)
        {
            z = 0f;
            float d = (t.b.y - t.c.y) * (t.a.x - t.c.x) + (t.c.x - t.b.x) * (t.a.y - t.c.y);
            if (Mathf.Abs(d) < 1e-12f)
                return false;
            float u = ((t.b.y - t.c.y) * (x - t.c.x) + (t.c.x - t.b.x) * (y - t.c.y)) / d;
            float v = ((t.c.y - t.a.y) * (x - t.c.x) + (t.a.x - t.c.x) * (y - t.c.y)) / d;
            float w = 1f - u - v;
            if (u < 0f || v < 0f || w < 0f)
                return false;
            z = u * t.a.z + v * t.b.z + w * t.c.z;
            return true;
        }
    }
}
