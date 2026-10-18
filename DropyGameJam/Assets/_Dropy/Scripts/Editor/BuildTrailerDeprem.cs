using System.Collections.Generic;
using System.Linq;
using Dropy.Gameplay;
using Dropy.Trailer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dropy.EditorTools
{
    /// <summary>
    /// "Dropy > Trailer > 4": deprem haritasının (Deprem_LowPoly) trailer kopyası. Asıl sahneye dokunmaz; açık ve
    /// kaydedilmemiş hâliyle kopyalar. Kopyada ofisin arkasındaki sokağa yol döşenir, ufuk zemini aşağı ve geriye
    /// çekilir (yukarıdan bakınca çimle titreşmesin, kenarı görünmesin), kamera sağ üstten ofise yavaşça yaklaşır.
    /// </summary>
    static class BuildTrailerDeprem
    {
        const string SourcePath = "Assets/_Dropy/Scenes/Deprem/Deprem_LowPoly.unity";
        const string TargetPath = "Assets/_Dropy/Scenes/Trailer/Trailer_Deprem_SagUst.unity";
        const string RoadPrefab = "Assets/_Dropy/Prefabs/Environment/Deprem/Yol_Modul_10m.prefab";
        const float GrassTop = 0.06f;
        static readonly Vector3 StartPosition = new Vector3(24.4f, 39.7f, -36.2f);
        static readonly Vector3 StartEuler = new Vector3(40.75f, -34.3f, 0f);

        [MenuItem("Dropy/Trailer/4 - Deprem: sağ üstten")]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Trailer_Deprem: önce Play modundan çık.");
                return;
            }

            // Asıl sahne açıksa ekrandaki (kaydedilmemiş olabilir) hâli kopyalanır, değilse diskteki.
            AssetDatabase.DeleteAsset(TargetPath);
            var source = SceneManager.GetSceneByPath(SourcePath);
            if (source.isLoaded)
                EditorSceneManager.SaveScene(source, TargetPath, true);
            else
                AssetDatabase.CopyAsset(SourcePath, TargetPath);

            // Kopya yanına açılır; açık sahnelere ve kaydedilmemiş değişikliklere dokunulmaz.
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(TargetPath, OpenSceneMode.Additive);
            // Yeni nesneler kopyaya düşsün.
            SceneManager.SetActiveScene(scene);
            var roots = scene.GetRootGameObjects();

            LayFlat(roots);
            PushHorizonBack(roots);
            BuildBackStreet(roots);
            BuildCamera(roots);

            EditorSceneManager.SaveScene(scene);
            if (previous.IsValid() && previous != scene)
                SceneManager.SetActiveScene(previous);
            if (SceneManager.sceneCount > 1)
                EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"Trailer sahnesi hazır: {TargetPath}");
        }

        static GameObject Root(GameObject[] roots, string name) => roots.FirstOrDefault(r => r.name == name);

        /// <summary>
        /// Çim parseli, bahçe yolu ve saksı prefab'larının kökünde -90° X dönüşü var; harita kurucusu (BuildDepremLowPoly.Put)
        /// kökü Euler(0, y, 0)'a sıfırladığı için bunlar dik duruyordu (ufuk zemini yolun ortasında 600 m'lik bir duvardı).
        /// Prefab'ın kendi dönüşü geri eklenir; ölçekteki derinlik (Z) dönüşle Y eksenine geçtiği için Y/Z yer değiştirir.
        /// </summary>
        static void LayFlat(GameObject[] roots)
        {
            int fixedCount = 0;
            foreach (var groupName in new[] { "Zemin", "Ofis_Bahce" })
            {
                var group = Root(roots, groupName);
                if (group == null)
                    continue;
                foreach (Transform t in group.transform)
                {
                    var asset = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                    if (asset == null)
                        continue;
                    var assetRotation = asset.transform.localRotation;
                    if (Mathf.Abs(Mathf.DeltaAngle(assetRotation.eulerAngles.x, 270f)) > 1f || Mathf.Abs(Mathf.DeltaAngle(t.eulerAngles.x, 0f)) > 1f)
                        continue;
                    t.rotation = Quaternion.Euler(0f, t.eulerAngles.y, 0f) * assetRotation;
                    var s = t.localScale;
                    t.localScale = new Vector3(s.x, s.z, s.y);
                    fixedCount++;
                }
            }
            Debug.Log($"Trailer_Deprem: {fixedCount} dik duran zemin/bahçe parçası yere yatırıldı.");
        }

        /// <summary>Ufuk zemini büyütülür, kenarı sisin çok dışına ve kameranın baktığı yönde geriye taşınır, çimin altına iner.</summary>
        static void PushHorizonBack(GameObject[] roots)
        {
            var ground = Root(roots, "Zemin");
            var horizon = ground != null ? ground.transform.Find("Ufuk_Zemin") : null;
            if (horizon == null)
            {
                Debug.LogWarning("Trailer_Deprem: Zemin/Ufuk_Zemin bulunamadı.");
                return;
            }
            var before = Bounds(horizon.gameObject);
            var s = horizon.localScale;
            horizon.localScale = new Vector3(s.x * 2f, s.y * 2f, s.z);
            var after = Bounds(horizon.gameObject);
            var p = horizon.position;
            p.x += before.center.x - after.center.x;
            p.z += before.center.z - after.center.z + 150f;
            // Çim parselleriyle (üstü 0.06) titreşmesin.
            p.y = -0.35f;
            horizon.position = p;
            Debug.Log($"Trailer_Deprem: Ufuk_Zemin {before.min} - {before.max} → {Bounds(horizon.gameObject).min} - {Bounds(horizon.gameObject).max}");
        }

        /// <summary>Ofis sırasıyla arkasındaki apartman sırası arasındaki çim şeride yol döşer, yoldaki ağaç ve çalıları kaldırır.</summary>
        static void BuildBackStreet(GameObject[] roots)
        {
            var buildings = Root(roots, "Binalar");
            if (buildings == null)
            {
                Debug.LogWarning("Trailer_Deprem: Binalar bulunamadı.");
                return;
            }
            var children = buildings.transform.Cast<Transform>().ToList();
            var front = children.Where(t => t.position.z > -2f && t.position.z < 12f).ToList();
            var back = children.Where(t => t.position.z >= 12f && t.position.z < 38f).ToList();
            var office = Root(roots, "Ofis_Bina_Cokus");
            float frontEdge = front.Select(t => Bounds(t.gameObject).max.z)
                .Concat(office != null ? new[] { Bounds(office).max.z } : new float[0]).DefaultIfEmpty(10f).Max();
            float backEdge = back.Select(t => Bounds(t.gameObject).min.z).DefaultIfEmpty(21f).Min();

            var street = new GameObject("Arka_Sokak_Yol").transform;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoadPrefab);
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab, street);
            var module = Bounds(probe);
            Object.DestroyImmediate(probe);

            float gap = backEdge - frontEdge;
            float depth = Mathf.Min(module.size.z, gap - 1f);
            float zScale = depth / module.size.z;
            float centerZ = (frontEdge + backEdge) / 2f;

            for (var x = -75; x <= 75; x += 10)
            {
                var road = (GameObject)PrefabUtility.InstantiatePrefab(prefab, street);
                road.transform.localScale = new Vector3(1f, 1f, zScale);
                // Çimin (0.06) üstünde dursun; pivotun modülün ortasında olup olmadığı ölçüden düzeltilir.
                road.transform.position = new Vector3(x, GrassTop + 0.01f, centerZ);
                var b = Bounds(road);
                road.transform.position += new Vector3(0f, GrassTop + 0.005f - b.min.y, centerZ - b.center.z);
            }

            float half = depth / 2f + 0.6f;
            int removed = 0;
            foreach (var groupName in new[] { "Agaclar", "Calilar" })
            {
                var group = Root(roots, groupName);
                if (group == null)
                    continue;
                foreach (var t in group.transform.Cast<Transform>().ToList())
                {
                    if (Mathf.Abs(t.position.z - centerZ) < half)
                    {
                        Object.DestroyImmediate(t.gameObject);
                        removed++;
                    }
                }
            }
            Debug.Log($"Trailer_Deprem: arka sokak z={centerZ:0.0}, boşluk {gap:0.0} m, yol derinliği {depth:0.0} m; {removed} ağaç/çalı kaldırıldı.");
        }

        /// <summary>
        /// Kamera bir "rig"in çocuğu olur: rig'i TrailerCamera sürer, kameradaki CameraShake depremde yerel konumu sarsar.
        /// </summary>
        static void BuildCamera(GameObject[] roots)
        {
            var cameraGo = roots.FirstOrDefault(r => r.CompareTag("MainCamera"));
            var office = Root(roots, "Ofis_Bina_Cokus");
            if (cameraGo == null || office == null)
            {
                Debug.LogWarning("Trailer_Deprem: Main Camera ya da Ofis_Bina_Cokus bulunamadı.");
                return;
            }

            // Başlangıç pozu Unity'de elle seçildi: yolun karşısından, sağ üstten, karşı sıradaki apartmanların üstünden.
            var start = StartPosition;
            var startRotation = Quaternion.Euler(StartEuler);
            // Odak bu bakışın ofis yüksekliğinde zemine indiği nokta; kamera bakış yönünü bozmadan ona yaklaşır.
            float focusHeight = Bounds(office).size.y * 0.45f;
            var forward = startRotation * Vector3.forward;
            var focus = start + forward * ((focusHeight - start.y) / forward.y);

            var rig = new GameObject("TrailerKamera_Rig");
            rig.transform.SetPositionAndRotation(start, startRotation);
            var marker = new GameObject("TrailerKamera_Baslangic");
            marker.transform.SetPositionAndRotation(start, startRotation);

            cameraGo.transform.SetParent(rig.transform, false);
            cameraGo.transform.localPosition = Vector3.zero;
            cameraGo.transform.localRotation = Quaternion.identity;
            var cam = cameraGo.GetComponent<Camera>();
            cam.fieldOfView = 42f;
            cam.farClipPlane = 900f;

            var trailer = rig.AddComponent<TrailerCamera>();
            trailer.target = office.transform;
            trailer.useBone = false;
            trailer.targetOffset = focus - office.transform.position;
            trailer.approachFromFront = false;
            trailer.startPoint = marker.transform;
            trailer.delay = 0f;
            // Deprem 2. saniyede başlar, 8 sn sürer; kamera o boyunca yavaşça süzülür.
            trailer.duration = 11f;
            // Sonunda ofis kadrajı doldursun (yaklaşık 25 m).
            trailer.endDistance = Vector3.Distance(start, focus) * 0.47f;
            trailer.endShift = new Vector2(-2f, -1f);
            trailer.handheld = 0.06f;
            trailer.handheldRotation = 0.15f;
            trailer.handheldSpeed = 0.35f;
        }

        static Bounds Bounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers)
                b.Encapsulate(r.bounds);
            return b;
        }
    }
}
