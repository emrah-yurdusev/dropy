using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Dropy.Gameplay;

namespace Dropy.EditorTools
{
    /// <summary>
    /// Ömer'in ofisini ana sahne yapar, Berkant'ın masa / NPC akışını oraya bağlar.
    /// Menü: Dropy / Ana Sahneyi Kur. Kendiliğinden çalışmaz.
    /// </summary>
    public static class BuildMainOffice
    {
        const string OmerScene = "Assets/_Dropy/Scenes/Sandbox/omer.unity";
        const string MainScene = "Assets/_Dropy/Scenes/Main/main.unity";
        const string PrefsKey = "Dropy.MainOfficeBuilt.v1";

        [MenuItem("Dropy/Ana Sahneyi Kur")]
        public static void Build()
        {
            if (!File.Exists(OmerScene))
            {
                Debug.LogError("Ömer sahnesi yok: " + OmerScene);
                return;
            }

            File.Copy(OmerScene, MainScene, true);
            AssetDatabase.ImportAsset(MainScene);
            var scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);

            if (GameObject.Find("DeskManager") != null)
            {
                Debug.Log("Ana sahnede DeskManager zaten var.");
                EditorPrefs.SetBool(PrefsKey, true);
                return;
            }

            var officeFloor = LargestFloor("Office");
            var anteroomFloor = LargestFloor("Anteroom");
            if (officeFloor == null)
            {
                Debug.LogError("Office zemini bulunamadı. Ana sahne kopyalandı ama masa kurulmadı.");
                return;
            }

            var officeBounds = officeFloor.bounds;
            var officeCenter = officeBounds.center;
            var anteroomCenter = anteroomFloor != null ? anteroomFloor.bounds.center : officeCenter + Vector3.forward * 4f;
            var toDoor = anteroomCenter - officeCenter;
            toDoor.y = 0f;
            if (toDoor.sqrMagnitude < 0.01f)
                toDoor = Vector3.forward;
            var dir = toDoor.normalized;
            var reach = Mathf.Abs(dir.x) * officeBounds.extents.x + Mathf.Abs(dir.z) * officeBounds.extents.z;
            var floorY = officeBounds.max.y;

            var chairPos = new Vector3(officeCenter.x, floorY, officeCenter.z);
            var doorPos = chairPos + dir * Mathf.Max(0.6f, reach - 0.4f);
            doorPos.y = floorY;
            var spawnPos = new Vector3(anteroomCenter.x, anteroomFloor != null ? anteroomFloor.bounds.max.y : floorY, anteroomCenter.z);
            var exitPos = spawnPos + dir * 1.5f;

            var root = new GameObject("Gameplay");

            var chair = new GameObject("VisitorChair");
            chair.transform.SetParent(root.transform, false);
            chair.transform.position = chairPos;
            chair.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            var sit = new GameObject("SitPoint");
            sit.transform.SetParent(chair.transform, false);
            sit.transform.localPosition = Vector3.zero;
            var chairComp = chair.AddComponent<Chair>();
            var sitField = typeof(Chair).GetField("sitPoint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            sitField?.SetValue(chairComp, sit.transform);

            var door = new GameObject("Door");
            door.transform.SetParent(root.transform, false);
            door.transform.position = doorPos;
            door.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            var doorComp = door.AddComponent<Door>();

            var spawn = new GameObject("SpawnPoint");
            spawn.transform.SetParent(root.transform, false);
            spawn.transform.position = spawnPos;

            var exit = new GameObject("ExitPoint");
            exit.transform.SetParent(root.transform, false);
            exit.transform.position = exitPos;

            var camera = Camera.main;
            if (camera == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                camera = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            camera.transform.position = chairPos + dir * 1.8f + Vector3.up * 1.55f;
            camera.transform.LookAt(chairPos + Vector3.up * 1.15f);

            var deskGo = new GameObject("DeskManager");
            deskGo.transform.SetParent(root.transform, false);
            var desk = deskGo.AddComponent<DeskManager>();
            var so = new SerializedObject(desk);
            so.FindProperty("deskCamera").objectReferenceValue = camera;
            so.FindProperty("visitorChair").objectReferenceValue = chairComp;
            so.FindProperty("spawnPoint").objectReferenceValue = spawn.transform;
            so.FindProperty("exitPoint").objectReferenceValue = exit.transform;
            so.FindProperty("door").objectReferenceValue = doorComp;
            so.FindProperty("regulations").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<RegulationData>("Assets/_Dropy/ScriptableObjects/Desk/Regulations.asset");

            var casePaths = BuildCaseFiles.Paths;
            var cases = so.FindProperty("cases");
            cases.arraySize = casePaths.Length;
            for (var i = 0; i < casePaths.Length; i++)
                cases.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<BuildingCase>(casePaths[i]);

            var prefabGuids = AssetDatabase.FindAssets("BusinessMale t:Prefab", new[] { "Assets/_Dropy/Prefabs" });
            var prefabs = prefabGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(p => p != null)
                .Take(3)
                .ToArray();
            var contractors = so.FindProperty("defaultContractors");
            contractors.arraySize = prefabs.Length;
            for (var i = 0; i < prefabs.Length; i++)
                contractors.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            var navGo = new GameObject("NavMesh");
            var surface = navGo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var buildScenes = EditorBuildSettings.scenes.ToList();
            buildScenes.RemoveAll(s => s.path == MainScene);
            buildScenes.Insert(0, new EditorBuildSettingsScene(MainScene, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();

            EditorPrefs.SetBool(PrefsKey, true);
            Debug.Log("Ana sahne kuruldu. Ömer ofisi + masa akışı. Play'e basınca müteahhit gelmeli. NavMesh yüzey sayısı: " +
                      Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None).Length);
        }

        static Renderer LargestFloor(string rootName)
        {
            var root = GameObject.Find(rootName);
            if (root == null)
                return null;
            return root.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.gameObject.name.IndexOf("Floor", System.StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(r => r.bounds.size.x * r.bounds.size.z)
                .FirstOrDefault();
        }
    }
}
