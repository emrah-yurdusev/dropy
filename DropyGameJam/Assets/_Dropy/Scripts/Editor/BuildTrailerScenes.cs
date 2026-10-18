using Dropy.Gameplay;
using Dropy.Trailer;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Dropy.EditorTools
{
    /// <summary>
    /// "Dropy > Trailer" menüsü: trailer çekimi için oyun sahnelerinin kopyalarını kurar (asıl sahnelere dokunmaz).
    /// Hapishanede mühendisin yüzüne yaklaşan kamera, ofiste anlatan müteahhit, masada mühür vuruşu.
    /// Her menü kendi sahnesini sıfırdan yeniden kurar; sahnede elle yapılan ayarlar silinir.
    /// </summary>
    static class BuildTrailerScenes
    {
        const string Folder = "Assets/_Dropy/Scenes/Trailer";
        const string MaterialFolder = "Assets/_Dropy/Art/Materials/Trailer";
        const string PrisonSource = "Assets/_Dropy/Scenes/Hapishane/Hapishane.unity";
        const string OfficeSource = "Assets/_Dropy/Scenes/Main/main.unity";
        // Yüzü (FaceController) ve humanoid iskeleti olan müteahhit; kollar ve ağız ancak böyle oynar.
        const string ContractorPrefab = "Assets/_Dropy/Prefabs/Characters/SefaBey_LowPoly.prefab";
        const string StampModel = "Assets/PolygonalMind/LowPolyOffice/Props/mesh_stamp.fbx";
        const string HeadlineFont = "Assets/_Dropy/Art/Fonts/Oswald SDF.asset";
        const string BodyFont = "Assets/_Dropy/Art/Fonts/OldStandard SDF.asset";

        static readonly Color ApproveInk = new Color(0.2f, 0.62f, 0.33f);
        static readonly Color PaperColor = new Color(0.96f, 0.94f, 0.88f);

        [MenuItem("Dropy/Trailer/Hepsini kur")]
        static void All()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Prison();
            Office();
            Stamp();
        }

        [MenuItem("Dropy/Trailer/1 - Hapishane: yüze yaklaşma")]
        static void PrisonMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                Prison();
        }

        [MenuItem("Dropy/Trailer/2 - Ofis: anlatan müteahhit")]
        static void OfficeMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                Office();
        }

        [MenuItem("Dropy/Trailer/3 - Ofis: mühür vuruşu")]
        static void StampMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                Stamp();
        }

        // ---------------------------------------------------------------- Hapishane

        static void Prison()
        {
            var scene = CopyScene(PrisonSource, "Trailer_Hapishane_Yuz");

            // Kapı yine çarpsın ama kamerayı biz sürüyoruz.
            var sequence = Object.FindFirstObjectByType<CellSequence>();
            if (sequence != null)
                SetRef(sequence, "cameraTransform", null);

            var npc = Object.FindFirstObjectByType<NpcController>();
            var cam = Camera.main;
            SetFace(npc.gameObject, FaceExpression.Sad);
            var life = npc.GetComponent<SeatedLife>();
            if (life != null)
                life.lookTarget = cam.transform;

            var chair = new SerializedObject(npc.GetComponent<SnapSeatOnStart>()).FindProperty("chair").objectReferenceValue as Chair;
            var seat = chair != null ? chair.SitPoint : npc.transform;
            var face = seat.position + Vector3.up * 1.05f;

            // Hücrenin içinden, kapının hemen arkasından ve biraz yandan başla.
            var door = Object.FindFirstObjectByType<CellDoor>();
            float doorZ = door != null ? Bounds(door.gameObject).max.z : face.z - 2.5f;
            var start = new Vector3(face.x + 0.75f, 1.55f, doorZ + 0.35f);

            var camera = cam.gameObject.AddComponent<TrailerCamera>();
            camera.target = npc.transform;
            camera.startPoint = Marker("TrailerKamera_Baslangic", start, face);
            camera.delay = 1.2f;
            camera.duration = 7f;
            camera.endDistance = 0.55f;
            camera.endShift = new Vector2(0.05f, 0.02f);
            cam.transform.SetPositionAndRotation(start, Quaternion.LookRotation(face - start));

            Save(scene);
        }

        // ---------------------------------------------------------------- Ofis: anlatan müteahhit

        static void Office()
        {
            var scene = CopyScene(OfficeSource, "Trailer_Ofis_Muteahhit");
            var cam = Camera.main;
            var npc = SeatContractor(cam.transform, out _);
            var talker = npc.AddComponent<TrailerTalker>();
            talker.expression = FaceExpression.Happy;

            var camera = cam.gameObject.AddComponent<TrailerCamera>();
            camera.target = npc.transform;
            camera.startLookAt = 0.3f;
            camera.delay = 1f;
            camera.duration = 6.5f;
            // Göğüs hizasına bakıp biraz geride durur ki anlatan eller de kadrajda kalsın.
            camera.targetOffset = new Vector3(0f, -0.14f, 0f);
            camera.endDistance = 1.15f;
            camera.endShift = new Vector2(0.15f, 0.04f);

            Save(scene);
        }

        // ---------------------------------------------------------------- Ofis: mühür vuruşu

        static void Stamp()
        {
            var scene = CopyScene(OfficeSource, "Trailer_Muhur");
            var cam = Camera.main;
            var npc = SeatContractor(cam.transform, out var desk);
            var talker = npc.AddComponent<TrailerTalker>();
            talker.weight = 0.7f;
            talker.expression = FaceExpression.Suspicious;

            var deskBounds = Bounds(desk);
            float top = deskBounds.max.y;
            // Oyuncu kameranın olduğu taraftadır; kâğıt masanın o yarısına konur.
            float towardPlayer = Mathf.Sign(cam.transform.position.z - deskBounds.center.z);
            var paperPosition = new Vector3(deskBounds.center.x - 0.05f, top, deskBounds.center.z + towardPlayer * 0.18f);
            paperPosition.y = SurfaceAt(paperPosition, top) + 0.002f;
            // Kamera masanın üstünden çapraz bakar; kâğıt ona dönük, yazının üstü kameradan uzağa.
            var cameraOffset = new Vector3(0.3f, 0.36f, towardPlayer * 0.42f);
            var paperYaw = Quaternion.LookRotation(new Vector3(-cameraOffset.x, 0f, -cameraOffset.z)) * Quaternion.Euler(0f, 8f, 0f);

            var paper = new GameObject("Trailer_Kagit").transform;
            paper.SetPositionAndRotation(paperPosition, paperYaw);
            var sheet = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sheet.name = "Sayfa";
            Object.DestroyImmediate(sheet.GetComponent<Collider>());
            sheet.transform.SetParent(paper, false);
            sheet.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            sheet.transform.localScale = new Vector3(0.21f, 0.297f, 1f);
            sheet.GetComponent<MeshRenderer>().sharedMaterial = Material("Trailer_Kagit", "Universal Render Pipeline/Lit", PaperColor);

            var body = Text3D(paper, "Belge", BodyFont,
                "<b><size=140%>İSKÂN BAŞVURUSU</size></b>\n<size=80%>Fayköy Belediyesi · İmar Müdürlüğü</size>\n\n" +
                "Yapı: Güneş Apartmanı\nKat sayısı: 6\nBeton sınıfı: C20\nZemin etüdü: —\nMüteahhit: Kerem Bey\n\n" +
                "<size=80%>Yukarıda bilgileri verilen yapının iskânının verilmesini arz ederim.</size>",
                0.085f, new Color(0.12f, 0.11f, 0.1f), new Vector2(0.18f, 0.26f), TextAlignmentOptions.TopLeft);
            body.transform.localPosition = new Vector3(0f, 0.001f, 0f);

            // Mürekkep izi: çerçeveli yazı, hafif eğik.
            var ink = new GameObject("Murekkep").transform;
            ink.SetParent(paper, false);
            ink.localPosition = new Vector3(0.015f, 0.0015f, -0.07f);
            ink.localRotation = Quaternion.Euler(0f, -14f, 0f);
            var inkMaterial = Material("Trailer_Murekkep", "Universal Render Pipeline/Unlit", ApproveInk);
            const float w = 0.15f, h = 0.07f, line = 0.006f;
            Bar(ink, inkMaterial, new Vector3(0f, 0f, h / 2f), new Vector2(w, line));
            Bar(ink, inkMaterial, new Vector3(0f, 0f, -h / 2f), new Vector2(w, line));
            Bar(ink, inkMaterial, new Vector3(w / 2f, 0f, 0f), new Vector2(line, h + line));
            Bar(ink, inkMaterial, new Vector3(-w / 2f, 0f, 0f), new Vector2(line, h + line));
            var stampText = Text3D(ink, "Yazi", HeadlineFont, "<b>İSKÂN\nONAYLANDI</b>", 0.2f, ApproveInk, new Vector2(w - 0.01f, h - 0.008f), TextAlignmentOptions.Center);
            stampText.lineSpacing = -25f;
            stampText.transform.localPosition = new Vector3(0f, 0.0005f, 0f);

            // Damga: kök kâğıda değdiği nokta, model tabanı kökte duracak şekilde ortalanır.
            // Önce dünya merkezinde düz kurulur (ölçüler eksenlere göre doğru çıksın), sonra izin üstüne taşınır.
            var root = new GameObject("Trailer_Muhur").transform;
            var pivot = new GameObject("Damga").transform;
            pivot.SetParent(root, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StampModel));
            model.transform.SetParent(pivot, false);
            var mb = Bounds(model);
            // Tabanın uzun kenarı izin uzun kenarıyla (yerel X) aynı yöne baksın.
            if (mb.size.z > mb.size.x)
                model.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * model.transform.rotation;
            mb = Bounds(model);
            // Taban, mürekkep çerçevesinden biraz büyük olsun ki iz tam altından çıksın.
            model.transform.localScale *= (w + 0.012f) / Mathf.Max(0.001f, mb.size.x);
            mb = Bounds(model);
            model.transform.position -= new Vector3(mb.center.x, mb.min.y, mb.center.z);
            root.SetPositionAndRotation(ink.position, ink.rotation);
            foreach (var c in model.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);

            var dust = Dust(root);

            var stamp = root.gameObject.AddComponent<TrailerStamp>();
            stamp.stamp = pivot;
            stamp.paper = paper;
            stamp.ink = ink;
            stamp.dust = dust;

            // Kamera: masanın üstünden, kâğıda çapraz bakıyor; vuruşa doğru yavaşça yaklaşır.
            var focus = paperPosition + Vector3.up * 0.02f;
            var start = focus + cameraOffset;
            var camera = cam.gameObject.AddComponent<TrailerCamera>();
            camera.target = paper;
            camera.useBone = false;
            camera.targetOffset = new Vector3(0f, 0.03f, 0f);
            camera.approachFromFront = false;
            camera.startPoint = Marker("TrailerKamera_Baslangic", start, focus);
            camera.delay = 0f;
            camera.duration = 3.2f;
            camera.endDistance = 0.45f;
            camera.endShift = Vector2.zero;
            stamp.shakeCamera = camera;
            cam.transform.SetPositionAndRotation(start, Quaternion.LookRotation(focus - start));

            Save(scene);
        }

        // ---------------------------------------------------------------- Ortak

        /// <summary>main sahnesinin kopyasından oyun akışını kaldırır, müteahhidi misafir koltuğuna oturtur.</summary>
        static GameObject SeatContractor(Transform lookTarget, out GameObject desk)
        {
            var manager = Object.FindFirstObjectByType<DeskManager>(FindObjectsInactive.Include);
            var chair = manager != null ? new SerializedObject(manager).FindProperty("visitorChair").objectReferenceValue as Chair : null;
            if (chair == null)
                chair = Object.FindFirstObjectByType<Chair>();

            foreach (var name in new[] { "MainMenu", "Gameplay", "Sfx.OneShot" })
            {
                var go = GameObject.Find(name);
                if (go != null && go.transform.parent == null)
                    Object.DestroyImmediate(go);
            }
            desk = GameObject.Find("Ofis_Oda/Mobilya/Masa");

            var npc = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ContractorPrefab));
            npc.name = "Muteahhit";
            npc.transform.SetPositionAndRotation(chair.SitPoint.position, chair.SitPoint.rotation);
            if (npc.TryGetComponent<NavMeshAgent>(out var agent))
                agent.enabled = false;
            var snap = npc.AddComponent<SnapSeatOnStart>();
            SetRef(snap, "chair", chair);
            if (!npc.TryGetComponent<SeatedLife>(out var life))
                life = npc.AddComponent<SeatedLife>();
            life.lookTarget = lookTarget;
            return npc;
        }

        static Scene CopyScene(string source, string name)
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/_Dropy/Scenes", "Trailer");
            var path = $"{Folder}/{name}.unity";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset(source, path);
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        static void Save(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Trailer sahnesi hazır: {scene.path}");
        }

        static Transform Marker(string name, Vector3 position, Vector3 lookAt)
        {
            var marker = new GameObject(name).transform;
            marker.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
            return marker;
        }

        static void SetRef(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFace(GameObject character, FaceExpression expression)
        {
            var face = character.GetComponentInChildren<FaceController>();
            if (face == null)
                return;
            var so = new SerializedObject(face);
            so.FindProperty("expression").enumValueIndex = (int)expression;
            so.ApplyModifiedPropertiesWithoutUndo();
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

        /// <summary>Masanın üstündeki ince parçalar (deri altlık gibi) dahil, o noktadaki en üst yüzey.</summary>
        static float SurfaceAt(Vector3 point, float deskTop)
        {
            float top = deskTop;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var b = r.bounds;
                bool over = point.x >= b.min.x && point.x <= b.max.x && point.z >= b.min.z && point.z <= b.max.z;
                if (over && b.size.y < 0.06f && b.max.y > top && b.max.y < deskTop + 0.05f)
                    top = b.max.y;
            }
            return top;
        }

        static Material Material(string name, string shader, Color color)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/_Dropy/Art/Materials", "Trailer");
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Kâğıt düzleminde (yerel XZ) ince dikdörtgen.</summary>
        static void Bar(Transform parent, Material material, Vector3 position, Vector2 size)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bar.name = "Cerceve";
            Object.DestroyImmediate(bar.GetComponent<Collider>());
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = position;
            bar.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            bar.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = bar.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Kâğıdın üstüne yatık, yukarıdan okunan 3D yazı.</summary>
        static TextMeshPro Text3D(Transform parent, string name, string fontPath, string text, float size, Color color, Vector2 rect, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var tmp = go.AddComponent<TextMeshPro>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if (font != null)
                tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.rectTransform.sizeDelta = rect;
            return tmp;
        }

        static ParticleSystem Dust(Transform parent)
        {
            var go = new GameObject("Toz");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.003f, 0.009f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.82f, 0.76f), new Color(1f, 1f, 1f));
            main.gravityModifier = 0.35f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 45) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 80f;
            shape.radius = 0.07f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Material("Trailer_Toz", "Universal Render Pipeline/Particles/Unlit", new Color(0.9f, 0.88f, 0.82f));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }
    }
}
