using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Dropy.Gameplay;

namespace Dropy.EditorTools
{
    /// <summary>
    /// Deprem haritasını kurar: ortada yıkılabilen ofis binası, önünde yol ve kaldırım, çevresinde apartmanlar,
    /// bahçeler, çitler, ağaçlar ve çalılar. Oyunun deprem ara sahnesidir (EarthquakeCinematic.SceneName):
    /// EarthquakeSequence ofisi çökertir, apartmanları sallar; CinematicCaption manşetleri gösterir.
    /// Sahne her çalıştırmada baştan yazılır.
    /// Menü: Dropy / Deprem LowPoly Sahnesini Kur. Kendiliğinden çalışmaz.
    /// </summary>
    public static class BuildDepremLowPoly
    {
        const string ScenePath = "Assets/_Dropy/Scenes/Deprem/Deprem_LowPoly.unity";
        const string PrefabFolder = "Assets/_Dropy/Prefabs/Environment/Deprem/";
        const string SkyPath = "Assets/_Dropy/Art/Materials/Deprem/M_Gokyuzu_LowPoly.mat";
        const string CaptionFontPath = "Assets/_Dropy/Art/Fonts/Oswald SDF.asset";

        // Ofisin önü -Z'ye bakar. Yol X ekseni boyunca uzanır; modül 12 m (7 m asfalt + iki yanda 2.5 m kaldırım).
        const float RoadZ = -12.5f;
        const float NorthWalkEdge = RoadZ + 6f;   // ofis tarafı kaldırımın iç kenarı
        const float SouthWalkEdge = RoadZ - 6f;   // karşı kaldırımın dış kenarı
        const float GrassTop = 0.06f;

        [MenuItem("Dropy/Deprem LowPoly Sahnesini Kur")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Deprem_LowPoly: önce Play modundan çık.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildGround();
            BuildRoad();
            var office = BuildOffice();
            var apartments = BuildApartments();
            BuildFences();
            BuildTrees();
            BuildBushes();
            BuildLighting();
            var camera = BuildCamera();
            BuildEarthquake(office, apartments, camera);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("Deprem_LowPoly sahnesi kuruldu: " + ScenePath);
        }

        static void BuildGround()
        {
            var root = Group("Zemin");
            // Ofis tarafı ve karşı taraf çimi, ikisi de kaldırıma kadar gelir.
            Put("Cim_Parsel_10m", root, new Vector3(0f, 0f, NorthWalkEdge + 40f), 0f, new Vector3(16f, 1f, 8f));
            Put("Cim_Parsel_10m", root, new Vector3(0f, 0f, SouthWalkEdge - 40f), 0f, new Vector3(16f, 1f, 8f));
            // Sisin içinde kaybolan ufuk zemini.
            Put("Cim_Parsel_10m", root, new Vector3(0f, -0.08f, 0f), 0f, new Vector3(60f, 1f, 60f)).name = "Ufuk_Zemin";
        }

        static void BuildRoad()
        {
            var root = Group("Yol");
            for (var x = -75; x <= 75; x += 10)
                Put("Yol_Modul_10m", root, new Vector3(x, 0f, RoadZ), 0f, Vector3.one);
        }

        static CollapsibleBuilding BuildOffice()
        {
            var office = Put("Ofis_Bina_Cokus", null, Vector3.zero, 0f, Vector3.one);
            office.name = "Ofis_Bina_Cokus";

            // Kapı x = 1.2'de; basamaklardan kaldırıma bahçe yolu, iki yanında beton saksılar.
            var garden = Group("Ofis_Bahce");
            Put("Bahce_Yolu", garden, new Vector3(1.2f, 0f, -5.75f), 0f, new Vector3(1f, 1f, 0.52f));
            Put("Saksi_Beton", garden, new Vector3(-1.7f, 0f, -5.45f), 0f, Vector3.one);
            Put("Saksi_Beton", garden, new Vector3(4.1f, 0f, -5.45f), 0f, Vector3.one);

            float[,] planterBushes =
            {
                // x, çalı türü, ölçek
                { -2.6f, 1f, 0.75f }, { -1.65f, 2f, 0.7f }, { -0.8f, 1f, 0.7f },
                { 3.2f, 1f, 0.7f }, { 4.1f, 2f, 0.7f }, { 5.0f, 1f, 0.75f },
            };
            for (var i = 0; i < planterBushes.GetLength(0); i++)
            {
                var x = planterBushes[i, 0];
                Put("Cali_" + (int)planterBushes[i, 1], garden, new Vector3(x, 0.6f, -5.45f), x * 37f % 360f,
                    Vector3.one * planterBushes[i, 2]);
            }
            return office.GetComponent<CollapsibleBuilding>();
        }

        static Transform BuildApartments()
        {
            var root = Group("Binalar");
            // Ofisle aynı sıra (ön cephe kaldırımdan ~3 m geride). Ofisin yeri boş bırakılır.
            Row(root, 4f, 0f, new[] { "Apartman_G", "Apartman_A", "Apartman_F", "Apartman_B", "Apartman_D", "Apartman_C" },
                new[] { -56f, -38f, -20.5f, 19.5f, 36.5f, 53.5f });
            // Arka sıralar, ofisin üstünden görünür.
            Row(root, 27f, 0f, new[] { "Apartman_C", "Apartman_E", "Apartman_D", "Apartman_G", "Apartman_F", "Apartman_B" },
                new[] { -50f, -31f, -11f, 9f, 28f, 47f });
            Row(root, 50f, 0f, new[] { "Apartman_A", "Apartman_B", "Apartman_E", "Apartman_C", "Apartman_D" },
                new[] { -40f, -20f, 0f, 20f, 40f });
            // Yolun karşısı, yüzleri yola dönük.
            Row(root, -30f, 180f, new[] { "Apartman_B", "Apartman_D", "Apartman_F", "Apartman_A", "Apartman_E", "Apartman_G", "Apartman_C" },
                new[] { -54f, -36f, -18f, 0f, 18f, 36f, 54f });
            return root;
        }

        static void Row(Transform root, float z, float rotY, string[] names, float[] xs)
        {
            for (var i = 0; i < names.Length; i++)
                Put(names[i], root, new Vector3(xs[i], 0f, z), rotY, Vector3.one);
        }

        static void BuildFences()
        {
            var root = Group("Citler");
            // Ofisin önü açık kalır, komşu bahçeler çitli.
            FenceLine(root, -62f, -8f, NorthWalkEdge - 0.25f, false);
            FenceLine(root, 8f, 62f, NorthWalkEdge - 0.25f, false);
            FenceLine(root, -62f, 62f, SouthWalkEdge - 0.25f, true);
        }

        /// <summary>Cit_2m'nin pivotu sol ucunda, +X yönüne 2 m uzanır. Ters yüzde 180° döndürülüp sağ uca konur.</summary>
        static void FenceLine(Transform root, float x0, float x1, float z, bool flipped)
        {
            for (var x = x0; x < x1 - 0.5f; x += 2f)
            {
                if (flipped)
                    Put("Cit_2m", root, new Vector3(x + 2f, GrassTop, z), 180f, Vector3.one);
                else
                    Put("Cit_2m", root, new Vector3(x, GrassTop, z), 0f, Vector3.one);
                Put("Cit_Direk", root, new Vector3(x, GrassTop, z), 0f, Vector3.one);
            }
            Put("Cit_Direk", root, new Vector3(x1, GrassTop, z), 0f, Vector3.one);
        }

        static void BuildTrees()
        {
            var root = Group("Agaclar");
            float[,] trees =
            {
                // x, z, ağaç türü, ölçek
                { -9f, -3.2f, 1f, 1f }, { -8.5f, 2.5f, 2f, 0.9f }, { 9.5f, -3f, 3f, 1f }, { 8.8f, 2.8f, 1f, 0.9f },
                { -15f, -4.8f, 2f, 0.75f }, { 15.5f, -4.8f, 1f, 0.75f }, { -29.5f, -4.8f, 3f, 0.9f }, { 28f, -4.8f, 2f, 0.9f },
                { -47f, -4.8f, 1f, 0.9f }, { 45f, -4.8f, 3f, 0.9f }, { -65f, -4.8f, 2f, 1f }, { 62f, -4.8f, 1f, 1f },
                { -21f, 15f, 1f, 1.1f }, { 0f, 16f, 3f, 1.1f }, { 19f, 15f, 2f, 1.1f }, { -40f, 15f, 3f, 1f }, { 38f, 15f, 1f, 1f },
                { -45f, -20.8f, 1f, 0.8f }, { -27f, -20.8f, 2f, 0.8f }, { -9f, -20.8f, 3f, 0.8f },
                { 9f, -20.8f, 1f, 0.8f }, { 27f, -20.8f, 2f, 0.8f }, { 45f, -20.8f, 3f, 0.8f },
            };
            for (var i = 0; i < trees.GetLength(0); i++)
            {
                var x = trees[i, 0];
                Put("Agac_Yuvarlak_" + (int)trees[i, 2], root, new Vector3(x, GrassTop, trees[i, 1]), x * 53f % 360f,
                    Vector3.one * trees[i, 3]);
            }
        }

        static void BuildBushes()
        {
            var root = Group("Calilar");
            float[,] bushes =
            {
                // x, z, çalı türü
                { -6.5f, -5.6f, 1f }, { -11.5f, -5.6f, 2f }, { -12.8f, -5.4f, 1f }, { -17.5f, -5.7f, 3f }, { -24f, -5.6f, 2f },
                { 6.8f, -5.6f, 2f }, { 11.8f, -5.5f, 1f }, { 13f, -5.7f, 3f }, { 18f, -5.6f, 2f }, { 24f, -5.5f, 1f },
                { -33f, -5.6f, 1f }, { -42f, -5.6f, 3f }, { 33f, -5.6f, 2f }, { 41f, -5.6f, 1f }, { -52f, -5.6f, 2f }, { 51f, -5.6f, 3f },
                { -6f, 1.5f, 1f }, { 6.5f, 0.5f, 2f }, { -7f, -0.8f, 3f }, { 7.2f, -1.5f, 1f },
            };
            for (var i = 0; i < bushes.GetLength(0); i++)
            {
                var x = bushes[i, 0];
                Put("Cali_" + (int)bushes[i, 2], root, new Vector3(x, GrassTop, bushes[i, 1]), x * 71f % 360f, Vector3.one);
            }
        }

        static void BuildLighting()
        {
            var sunGo = new GameObject("Gunes");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.4f;
            sun.shadows = LightShadows.Soft;
            // Kamera tarafından, hafif soldan: ön cephe aydınlık, gölgeler sağa-arkaya düşer.
            sunGo.transform.rotation = Quaternion.Euler(42f, 28f, 0f);
            RenderSettings.sun = sun;

            RenderSettings.skybox = SkyMaterial();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.74f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.56f, 0.6f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.34f, 0.26f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.95f);
            RenderSettings.fogStartDistance = 80f;
            RenderSettings.fogEndDistance = 260f;
        }

        static Material SkyMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(mat, SkyPath);
            }
            mat.SetFloat("_SunDisk", 1f);
            mat.SetFloat("_SunSize", 0.03f);
            mat.SetFloat("_AtmosphereThickness", 0.9f);
            mat.SetColor("_SkyTint", new Color(0.45f, 0.6f, 0.85f));
            mat.SetColor("_GroundColor", new Color(0.55f, 0.6f, 0.65f));
            mat.SetFloat("_Exposure", 1.3f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Camera BuildCamera()
        {
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.farClipPlane = 600f;
            // Karşı bahçeden ofise bakış: yol ve iki kaldırım kadrajın altında kalır.
            camGo.transform.position = new Vector3(1.2f, 4.2f, SouthWalkEdge - 5f);
            camGo.transform.LookAt(new Vector3(1.2f, 5.2f, 0f));
            camGo.AddComponent<CameraShake>();
            return cam;
        }

        /// <summary>
        /// Eski Deprem sahnesindeki ayarlar: 2 sn sonra başlar (tek başına açılınca), 8 sn sürer.
        /// Kameranın gördüğü apartmanlar (ofis sırası ve arkası) sallanır, ofis çöker.
        /// </summary>
        static void BuildEarthquake(CollapsibleBuilding office, Transform apartments, Camera camera)
        {
            var go = new GameObject("Deprem");
            var sequence = go.AddComponent<EarthquakeSequence>();
            var caption = go.AddComponent<CinematicCaption>();

            var swaying = apartments.Cast<Transform>().Where(t => t.position.z > 0f).ToArray();
            var so = new SerializedObject(sequence);
            so.FindProperty("cameraShake").objectReferenceValue = camera.GetComponent<CameraShake>();
            var sway = so.FindProperty("swayingBuildings");
            sway.arraySize = swaying.Length;
            for (var i = 0; i < swaying.Length; i++)
                sway.GetArrayElementAtIndex(i).objectReferenceValue = swaying[i];
            var collapsing = so.FindProperty("collapsibleBuildings");
            collapsing.arraySize = 1;
            collapsing.GetArrayElementAtIndex(0).objectReferenceValue = office;
            so.ApplyModifiedPropertiesWithoutUndo();

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CaptionFontPath);
            if (font == null)
                Debug.LogWarning("Deprem_LowPoly: manşet yazı tipi yok: " + CaptionFontPath);
            var cso = new SerializedObject(caption);
            cso.FindProperty("font").objectReferenceValue = font;
            cso.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform Group(string name)
        {
            return new GameObject(name).transform;
        }

        static GameObject Put(string prefabName, Transform parent, Vector3 position, float rotY, Vector3 scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + prefabName + ".prefab");
            if (prefab == null)
            {
                Debug.LogError("Deprem_LowPoly: prefab yok: " + prefabName);
                return new GameObject(prefabName + "_EKSIK");
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (parent != null)
                go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotY, 0f));
            go.transform.localScale = scale;
            return go;
        }
    }
}
