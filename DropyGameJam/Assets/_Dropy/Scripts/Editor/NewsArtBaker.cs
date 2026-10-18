using System.Collections.Generic;
using System.IO;
using Dropy.Gameplay;
using TMPro;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Dropy.EditorTools
{
    /// <summary>
    /// Sabah televizyonu, gazete ve mühür imleci için yer tutucu görselleri üretir ve bağlar:
    /// TextMeshPro fontları, damga imleçleri, gazete kâğıdı, televizyon kasası, 6 haber görseli,
    /// oyuncunun gazete ve sabıka fotoğrafı (karakter modelinden, noktalı baskı) ve spiker videosu (mp4).
    /// Görseller düz PNG/mp4'tür: istenen her dosya kendi çizimiyle değiştirilebilir, bağlantı kopmaz.
    /// Tekrar çalıştırmak dosyaları yeniden yazar.
    /// </summary>
    public static class NewsArtBaker
    {
        const string ArtFolder = "Assets/_Dropy/Art/News";
        const string FontFolder = "Assets/_Dropy/Art/Fonts";
        const string StylePath = "Assets/_Dropy/ScriptableObjects/Desk/NewsStyle.asset";
        const string PlayerPrefab = "Assets/_Dropy/Prefabs/Characters/BusinessMale_4_LowPolyStyle.prefab";
        const string AnchorPrefab = "Assets/_Dropy/Prefabs/Characters/Kadin_LowPoly.prefab";
        /// <summary>Aile fotoğrafı: baba oyuncunun kendisi (Emrah), eşi ve kızı Elif.</summary>
        const string FatherPrefab = "Assets/_Dropy/Prefabs/Characters/Emrah_LowPoly.prefab";
        const string MotherPrefab = "Assets/_Dropy/Prefabs/Characters/Kadin_LowPoly.prefab";
        const string EnvironmentFolder = "Assets/_Dropy/Prefabs/Environment/Deprem/";
        /// <summary>Spikerin kaşları videoda prefab'dakinden bu kadar önde durur ve en fazla bu kadar kalkar (metre).</summary>
        const float AnchorBrowForward = 0.003f, AnchorBrowLift = 0.006f;
        const string ClipsPath = "Assets/_Dropy/Art/Animations/Characters/Humanoid/BusinessMan_Anims_Humanoid.fbx";
        const int BakeLayer = 31;
        const string Charset = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
                               "ÇĞİÖŞÜçğıöşüÂâÎîÛû·•×°’“”…–—«»";

        static readonly Color Paper = new Color(0.91f, 0.88f, 0.81f);
        static readonly Color Ink = new Color(0.1f, 0.09f, 0.07f);
        static readonly Color Navy = new Color(0.06f, 0.18f, 0.32f);
        static readonly Color Alarm = new Color(0.75f, 0.13f, 0.11f);

        [MenuItem("Dropy/Haber Görselleri/Hepsini Üret")]
        public static void BakeAll()
        {
            BakeFonts();
            BakeCursors();
            BakePaperAndFrame();
            BakeNewsGraphics();
            BakePhotos();
            BakeAnchorVideo();
            Debug.Log("Haber görselleri üretildi ve bağlandı: " + ArtFolder);
        }

        // ---------------------------------------------------------------- fontlar

        [MenuItem("Dropy/Haber Görselleri/1 Fontlar")]
        public static void BakeFonts()
        {
            var oswald = MakeFont($"{FontFolder}/Oswald/Oswald-Variable.ttf", "Oswald");
            var playfair = MakeFont($"{FontFolder}/PlayfairDisplay/PlayfairDisplay-Variable.ttf", "PlayfairDisplay");
            var oldRegular = MakeFont($"{FontFolder}/OldStandardTT/OldStandard-Regular.ttf", "OldStandard");
            var oldItalic = MakeFont($"{FontFolder}/OldStandardTT/OldStandard-Italic.ttf", "OldStandard-Italic");
            var oldBold = MakeFont($"{FontFolder}/OldStandardTT/OldStandard-Bold.ttf", "OldStandard-Bold");
            // Yazı stili italik/kalın istenince TextMeshPro sahte eğme yerine fontun kendi italiğini/kalınını kullansın.
            var weights = oldRegular.fontWeightTable;
            weights[4].italicTypeface = oldItalic;
            weights[7].regularTypeface = oldBold;
            EditorUtility.SetDirty(oldRegular);

            // Kalem öğreticisinin el yazısı.
            var caveat = MakeFont($"{FontFolder}/Caveat/Caveat-Variable.ttf", "Caveat");
            var tutorial = AssetDatabase.LoadAssetAtPath<TutorialData>(BuildCaseFiles.TutorialPath);
            if (tutorial != null)
            {
                tutorial.font = caveat;
                EditorUtility.SetDirty(tutorial);
            }

            var style = Style();
            style.tvFont = oswald;
            style.headlineFont = oswald;
            style.mastheadFont = playfair;
            style.bodyFont = oldRegular;
            EditorUtility.SetDirty(style);
            AssetDatabase.SaveAssets();
        }

        static TMP_FontAsset MakeFont(string ttfPath, string name)
        {
            string path = $"{FontFolder}/{name} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null)
                return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            var asset = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = name + " SDF";
            AssetDatabase.CreateAsset(asset, path);
            asset.atlasTextures[0].name = asset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            asset.TryAddCharacters(Charset, out _);
            if (TMP_Settings.defaultFontAsset != null)
                asset.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // ---------------------------------------------------------------- imleç, kâğıt, kasa

        [MenuItem("Dropy/Haber Görselleri/2 İmleç, Kâğıt, Kasa")]
        public static void BakeCursors()
        {
            Directory.CreateDirectory(ArtFolder + "/Cursors");
            var set = new StampCursorSet
            {
                approve = SaveCursor("Damga_Onay", new Color(0.2f, 0.62f, 0.33f)),
                reject = SaveCursor("Damga_Red", new Color(0.8f, 0.2f, 0.17f)),
                shearWall = SaveCursor("Damga_Perde", new Color(0.25f, 0.5f, 0.85f)),
            };
            foreach (var desk in Object.FindObjectsByType<DeskManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(desk);
                var prop = so.FindProperty("stampCursors");
                prop.FindPropertyRelative("approve").objectReferenceValue = set.approve;
                prop.FindPropertyRelative("reject").objectReferenceValue = set.reject;
                prop.FindPropertyRelative("shearWall").objectReferenceValue = set.shearWall;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(desk.gameObject.scene);
                EditorSceneManager.SaveScene(desk.gameObject.scene);
            }
        }

        static Texture2D SaveCursor(string name, Color ink)
        {
            string path = $"{ArtFolder}/Cursors/{name}.png";
            File.WriteAllBytes(path, StampCursorSet.Draw(ink).EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Cursor;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static void BakePaperAndFrame()
        {
            var style = Style();
            style.paperTexture = SaveSprite("Gazete_Kagit", PaperTexture(1024, 1024), Vector4.zero);
            style.tvFrame = SaveSprite("TV_Kasa", TvFrameTexture(256), new Vector4(70, 70, 70, 70));
            EditorUtility.SetDirty(style);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Sararmış gazete kâğıdı: birkaç ölçekte gürültü, kenarlara doğru koyulaşma, lif lekeleri, soluk bir fincan izi.</summary>
        static Texture2D PaperTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            var rng = new System.Random(7);
            var specks = new List<Vector3>();
            for (int i = 0; i < 260; i++)
                specks.Add(new Vector3(rng.Next(w), rng.Next(h), 0.6f + (float)rng.NextDouble() * 1.6f));
            var cup = new Vector2(w * 0.78f, h * 0.24f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.004f, y * 0.004f) * 0.5f + Mathf.PerlinNoise(x * 0.02f + 40f, y * 0.02f) * 0.3f
                          + Mathf.PerlinNoise(x * 0.15f, y * 0.15f + 90f) * 0.2f;
                float edge = Mathf.Max(Mathf.Abs(x / (float)w - 0.5f), Mathf.Abs(y / (float)h - 0.5f)) * 2f;
                float darken = 0.04f * n + 0.07f * Mathf.Pow(edge, 4f);
                float ring = Mathf.Abs(Vector2.Distance(new Vector2(x, y), cup) - 90f);
                if (ring < 6f)
                    darken += 0.035f * (1f - ring / 6f);
                var c = new Color(0.93f - darken, 0.9f - darken * 1.1f, 0.82f - darken * 1.4f);
                pixels[y * w + x] = c;
            }
            foreach (var s in specks)
            {
                int r = Mathf.CeilToInt(s.z);
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int px = (int)s.x + dx, py = (int)s.y + dy;
                    if (px < 0 || py < 0 || px >= w || py >= h || dx * dx + dy * dy > s.z * s.z)
                        continue;
                    var c = (Color)pixels[py * w + px];
                    pixels[py * w + px] = c * 0.88f;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Eski televizyon kasası (9-slice): koyu kahve plastik, kenarda ışık, iç kenarda gölge. Ortası doludur; ekranı kod çizer.</summary>
        static Texture2D TvFrameTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float radius = 46f;
            var body = new Color(0.17f, 0.12f, 0.09f);
            var light = new Color(0.33f, 0.24f, 0.17f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Abs(x + 0.5f - size / 2f), py = Mathf.Abs(y + 0.5f - size / 2f);
                float inner = size / 2f - radius;
                float dx = Mathf.Max(px - inner, 0f), dy = Mathf.Max(py - inner, 0f);
                float outside = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                float a = Mathf.Clamp01(0.5f - outside);
                // Üst kenarda ışık, altta koyuluk; dış kenara yakın ince parlak çizgi.
                float vertical = (y / (float)size);
                var c = Color.Lerp(body * 0.8f, body * 1.15f, vertical);
                if (outside > -6f && outside < -3f)
                    c = Color.Lerp(c, light, 0.6f * vertical);
                float grain = Mathf.PerlinNoise(x * 0.02f, y * 0.4f) * 0.05f;
                c = new Color(c.r + grain, c.g + grain * 0.7f, c.b + grain * 0.4f, a);
                pixels[y * size + x] = c;
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        // ---------------------------------------------------------------- haber görselleri

        [MenuItem("Dropy/Haber Görselleri/3 Haber Görselleri")]
        public static void BakeNewsGraphics()
        {
            var style = Style();
            var font = style.tvFont;
            var story = AssetDatabase.LoadAssetAtPath<StoryData>(BuildCaseFiles.StoryPath);
            // day: görselin bağlandığı sabah haberi (0 = şimdilik hiçbir güne bağlı değil).
            var drawings = new (string file, System.Action<RectTransform, TMP_FontAsset> draw, int day)[]
            {
                ("Haber_1_FayHatti", DrawFault, 1),
                ("Haber_2_Dolap", DrawCabinet, 2),
                ("Haber_7_ToplanmaAlani", DrawAssemblyArea, 3),
                ("Haber_3_Sarsinti", DrawSeismograph, 4),
                ("Haber_4_HasarTespiti", DrawDamageSurvey, 0),
                ("Haber_5_HasarliBina", DrawCrackedBuilding, 0),
                ("Haber_6_Artci", DrawAftershocks, 0),
            };
            for (int i = 0; i < drawings.Length; i++)
            {
                Texture2D tex;
                using (var stage = new Stage(1024, 768, Paper, orthographic: true))
                {
                    var page = stage.Canvas(5f);
                    drawings[i].draw(page, font);
                    tex = stage.Capture();
                }
                var sprite = SaveSprite(drawings[i].file, tex, Vector4.zero);
                var news = story != null && drawings[i].day > 0 ? story.News(drawings[i].day) : null;
                if (news != null)
                    news.graphic = sprite;
            }
            if (story != null)
                EditorUtility.SetDirty(story);
            AssetDatabase.SaveAssets();
        }

        static void Title(RectTransform page, TMP_FontAsset font, string title, string subtitle)
        {
            Box(page, Navy, 512, 718, 1024, 100);
            Label(page, font, title, 64, Color.white, 40, 718, 940, 100, TextAlignmentOptions.Left, FontStyles.Bold);
            if (!string.IsNullOrEmpty(subtitle))
            {
                Box(page, Ink, 512, 40, 1024, 80);
                Label(page, font, subtitle, 34, Paper, 40, 40, 940, 80, TextAlignmentOptions.Left);
            }
        }

        static void DrawFault(RectTransform page, TMP_FontAsset font)
        {
            Box(page, new Color(0.78f, 0.82f, 0.66f), 512, 384, 1024, 768);
            Box(page, new Color(0.42f, 0.6f, 0.76f), 900, 120, 700, 420, -18f);
            var line = new[] { new Vector2(40, 210), new Vector2(200, 330), new Vector2(310, 290), new Vector2(450, 420), new Vector2(590, 380), new Vector2(740, 500), new Vector2(990, 470) };
            for (int i = 1; i < line.Length; i++)
                Line(page, line[i - 1], line[i], 12f, Alarm);
            foreach (float r in new[] { 70f, 140f, 210f })
                Ring(page, new Color(Alarm.r, Alarm.g, Alarm.b, 0.55f), 450, 420, r * 2f);
            Dot(page, Ink, 330, 520, 22);
            Label(page, font, "ŞEHİR MERKEZİ", 30, Ink, 350, 520, 300, 40, TextAlignmentOptions.Left);
            Dot(page, Ink, 640, 250, 18);
            Label(page, font, "LİMAN", 28, Ink, 660, 250, 200, 40, TextAlignmentOptions.Left);
            Title(page, font, "FAY HATTI", "Kent 1. derece deprem kuşağında");
        }

        /// <summary>Yeşil "toplanma alanı" tabelası: dört insan ve yön oku.</summary>
        static void DrawAssemblyArea(RectTransform page, TMP_FontAsset font)
        {
            var green = new Color(0.12f, 0.5f, 0.27f);
            Box(page, new Color(0.62f, 0.6f, 0.55f), 512, 150, 18, 160);
            Box(page, Ink, 512, 400, 548, 388);
            Box(page, green, 512, 400, 536, 376);
            Box(page, Color.white, 512, 400, 500, 340);
            Box(page, green, 512, 400, 484, 324);
            for (int i = 0; i < 4; i++)
            {
                float x = 362 + i * 100;
                Dot(page, Color.white, x, 445, 46);
                Box(page, Color.white, x, 365, 52, 100);
                Box(page, Color.white, x - 14, 290, 18, 64);
                Box(page, Color.white, x + 14, 290, 18, 64);
            }
            Label(page, font, "TOPLANMA ALANI", 44, Color.white, 262, 520, 500, 50, TextAlignmentOptions.Center, FontStyles.Bold);
            Line(page, new Vector2(820, 400), new Vector2(960, 400), 22f, green);
            Line(page, new Vector2(960, 400), new Vector2(915, 445), 22f, green);
            Line(page, new Vector2(960, 400), new Vector2(915, 355), 22f, green);
            Title(page, font, "TOPLANMA ALANI", "Mahallenizdeki alanı bugünden öğrenin");
        }

        static void DrawCabinet(RectTransform page, TMP_FontAsset font)
        {
            foreach (var (x, fixedToWall) in new[] { (256f, false), (768f, true) })
            {
                Box(page, Ink, x, 150, 400, 8);
                Box(page, Ink, x - 196, 400, 8, 500);
                var cabinet = Box(page, new Color(0.55f, 0.36f, 0.2f), fixedToWall ? x - 110 : x - 40, fixedToWall ? 330 : 310, 150, 340, fixedToWall ? 0f : -24f);
                Box(cabinet, new Color(0.42f, 0.27f, 0.15f), 75, 250, 120, 6);
                Box(cabinet, new Color(0.42f, 0.27f, 0.15f), 75, 130, 120, 6);
                if (fixedToWall)
                {
                    Box(page, Ink, x - 175, 490, 50, 10);
                    Box(page, Ink, x - 152, 474, 10, 40);
                }
                Box(page, fixedToWall ? new Color(0.2f, 0.55f, 0.3f) : Alarm, x, 590, 300, 56);
                Label(page, font, fixedToWall ? "DOĞRU: SABİT" : "YANLIŞ: SERBEST", 34, Color.white, x - 150, 590, 300, 56, TextAlignmentOptions.Center, FontStyles.Bold);
            }
            Title(page, font, "DOLABI SABİTLE", "Yaralanmaların çoğu devrilen eşyadan");
        }

        static void DrawSeismograph(RectTransform page, TMP_FontAsset font)
        {
            for (int x = 0; x < 1024; x += 40)
                Box(page, new Color(0.75f, 0.72f, 0.65f), x, 384, 2, 768);
            for (int y = 80; y < 680; y += 40)
                Box(page, new Color(0.75f, 0.72f, 0.65f), 512, y, 1024, 2);
            var rng = new System.Random(3);
            var previous = new Vector2(0, 380);
            for (int x = 8; x <= 1024; x += 8)
            {
                float burst = Mathf.Exp(-Mathf.Pow((x - 480) / 120f, 2f));
                float amp = 12f + 230f * burst;
                var point = new Vector2(x, 380 + ((float)rng.NextDouble() * 2f - 1f) * amp);
                Line(page, previous, point, 4f, Ink);
                previous = point;
            }
            Label(page, font, "BÜYÜKLÜK 3,9  ·  DERİNLİK 7 KM", 34, Alarm, 500, 600, 500, 50, TextAlignmentOptions.Right, FontStyles.Bold);
            Title(page, font, "SARSINTI", "Dün öğleden sonra hissedildi");
        }

        static RectTransform Building(RectTransform page, float x, float y, float w, float h, float tilt)
        {
            var building = Box(page, new Color(0.62f, 0.6f, 0.56f), x, y, w, h, tilt);
            for (float wy = 40; wy < h - 30; wy += 70)
            for (float wx = 35; wx < w - 20; wx += 60)
                Box(building, new Color(0.22f, 0.3f, 0.4f), wx, wy + 20, 34, 40);
            return building;
        }

        static void DrawDamageSurvey(RectTransform page, TMP_FontAsset font)
        {
            Box(page, Ink, 512, 118, 1024, 6);
            var tags = new[] { ("HAFİF", new Color(0.25f, 0.6f, 0.3f), 200f), ("ORTA", new Color(0.92f, 0.7f, 0.15f), 512f), ("AĞIR", Alarm, 824f) };
            foreach (var (label, color, x) in tags)
            {
                Building(page, x, 330, 220, 420, 0f);
                Box(page, color, x, 300, 150, 90, 4f);
                Label(page, font, label, 40, Color.white, x - 75, 300, 150, 90, TextAlignmentOptions.Center, FontStyles.Bold);
            }
            Title(page, font, "HASAR TESPİTİ", "Ekipler binaları tek tek etiketliyor");
        }

        static void DrawCrackedBuilding(RectTransform page, TMP_FontAsset font)
        {
            Box(page, Ink, 512, 118, 1024, 6);
            Building(page, 512, 360, 420, 480, 3f);
            var crack = new[] { new Vector2(470, 600), new Vector2(520, 520), new Vector2(490, 450), new Vector2(560, 360), new Vector2(530, 280), new Vector2(590, 170) };
            for (int i = 1; i < crack.Length; i++)
                Line(page, crack[i - 1], crack[i], 9f, Ink);
            var tape = Box(page, Color.white, 512, 260, 1100, 64, -8f);
            for (int i = 0; i < 18; i++)
                Box(tape, Alarm, 30 + i * 64, 32, 32, 64, 30f);
            Label(page, font, "GİRMEYİN", 56, Ink, 362, 262, 300, 70, TextAlignmentOptions.Center, FontStyles.Bold).rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            Title(page, font, "HASARLI BİNA", "Hasarlı binalara girmeyin");
        }

        static void DrawAftershocks(RectTransform page, TMP_FontAsset font)
        {
            Box(page, Ink, 512, 140, 860, 6);
            Box(page, Ink, 86, 400, 6, 520);
            // 6. günün sabahı: dünkü (5. gün) sayıya kadar.
            int[] counts = { 1, 2, 3, 26, 31 };
            for (int i = 0; i < counts.Length; i++)
            {
                float x = 190 + i * 160;
                float h = counts[i] * 15f;
                Box(page, i >= 3 ? Alarm : new Color(0.45f, 0.45f, 0.42f), x, 143 + h / 2f, 80, h);
                Label(page, font, counts[i].ToString(), 32, Ink, x - 50, 175 + h, 100, 40, TextAlignmentOptions.Center, FontStyles.Bold);
                Label(page, font, $"{i + 1}. gün", 28, Ink, x - 60, 112, 120, 40, TextAlignmentOptions.Center);
            }
            Title(page, font, "ARTÇILAR", "Günlük sarsıntı sayısı");
        }

        // ---------------------------------------------------------------- fotoğraflar

        [MenuItem("Dropy/Haber Görselleri/4 Fotoğraflar")]
        public static void BakePhotos()
        {
            var style = Style();
            var clips = Clips();
            Texture2D portrait, mugshot;

            using (var stage = new Stage(720, 340, new Color(0.78f, 0.77f, 0.74f), orthographic: false))
            {
                var man = stage.Spawn(PlayerPrefab, Vector3.zero, 180f);
                var pose = new Pose(man, clips["SitIdle"]);
                pose.Sample(0f);
                var head = Head(man);
                var desk = stage.Cube(new Vector3(0f, head.y - 0.62f, -0.45f), new Vector3(1.4f, 0.08f, 0.6f), new Color(0.35f, 0.24f, 0.15f));
                stage.Cube(desk.position + new Vector3(0.35f, 0.08f, 0f), new Vector3(0.12f, 0.08f, 0.12f), new Color(0.6f, 0.15f, 0.1f));
                stage.Cube(new Vector3(0f, head.y, 1.2f), new Vector3(6f, 4f, 0.1f), new Color(0.82f, 0.8f, 0.76f));
                stage.Light(new Vector3(35f, 35f, 0f), 0.85f);
                stage.LookAt(head + new Vector3(0f, -0.08f, -2f), head + new Vector3(0f, -0.16f, 0f), 30f);
                portrait = Halftone(stage.Capture(), 6);
                pose.Dispose();
            }

            using (var stage = new Stage(720, 340, new Color(0.97f, 0.97f, 0.95f), orthographic: false))
            {
                var man = stage.Spawn(PlayerPrefab, Vector3.zero, 180f);
                var pose = new Pose(man, clips["Idle"]);
                pose.Sample(0f);
                var head = Head(man);
                // Arkada boy çizgileri, önde isim tabelası.
                var wall = stage.Canvas(6f);
                for (int i = 0; i < 7; i++)
                {
                    Box(wall, Ink, 360, 30 + i * 50, 720, 9);
                    Label(wall, null, (190 - i * 10).ToString(), 34, Ink, 10, 30 + (6 - i) * 50 + 22, 80, 36, TextAlignmentOptions.Left, FontStyles.Bold);
                }
                var front = stage.Canvas(0.6f);
                Box(front, Ink, 360, 34, 200, 60);
                Box(front, Color.white, 360, 34, 192, 52);
                Label(front, null, "0317", 34, Ink, 264, 34, 192, 52, TextAlignmentOptions.Center, FontStyles.Bold);
                stage.Light(new Vector3(25f, 30f, 0f), 0.6f);
                stage.LookAt(head + new Vector3(0f, -0.06f, -1.75f), head + new Vector3(0f, -0.1f, 0f), 34f);
                mugshot = Halftone(stage.Capture(), 6);
                pose.Dispose();
            }

            style.playerPhoto = SaveSprite("Gazete_Foto_Memur", portrait, Vector4.zero);
            style.mugshotPhoto = SaveSprite("Gazete_Foto_Sabika", mugshot, Vector4.zero);
            EditorUtility.SetDirty(style);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Fotoğrafı gazete baskısına çevirir: gri ton, sonra her hücrede koyuluğa göre büyüyen mürekkep noktası.</summary>
        static Texture2D Halftone(Texture2D source, int cell)
        {
            int w = source.width, h = source.height;
            var src = source.GetPixels();
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            // Otomatik seviye: en koyu ve en açık %2'yi uçlara çek ki soluk çekim de gazete gibi kontrastlı olsun.
            var lums = new float[src.Length];
            for (int i = 0; i < src.Length; i++)
                lums[i] = src[i].r * 0.3f + src[i].g * 0.59f + src[i].b * 0.11f;
            var sorted = (float[])lums.Clone();
            System.Array.Sort(sorted);
            float low = sorted[(int)(sorted.Length * 0.02f)], high = sorted[(int)(sorted.Length * 0.98f)];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int cx = Mathf.Min(w - 1, (x / cell) * cell + cell / 2), cy = Mathf.Min(h - 1, (y / cell) * cell + cell / 2);
                float lum = Mathf.InverseLerp(low, high, lums[cy * w + cx]);
                float dark = Mathf.Clamp01(1f - Mathf.Pow(lum, 0.8f));
                float radius = cell * 0.62f * Mathf.Sqrt(dark);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx + 0.5f, cy + 0.5f));
                float a = Mathf.Clamp01(radius - d + 0.5f);
                pixels[y * w + x] = Color.Lerp(Paper, Ink, a);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // ---------------------------------------------------------------- aile fotoğrafı

        /// <summary>
        /// Akşam ekranındaki "Biz, geçen bayram" fotoğrafı: bahçede, arkada çit, ağaç ve apartman; Emrah, eşi ve kızı Elif gülümser.
        /// Çocuk modeli olmadığı için Elif, kadın modelinin küçültülmüş ve başı biraz büyütülmüş hâli.
        /// </summary>
        [MenuItem("Dropy/Haber Görselleri/6 Aile Fotoğrafı")]
        public static void BakeFamilyPhoto()
        {
            // Akşam ekranındaki çerçevenin resim alanı 308 × 228; iki katı çözünürlükte çekilir.
            const int width = 616, height = 456;
            var clips = Clips();
            Texture2D photo;
            using (var stage = new Stage(width, height, new Color(0.6f, 0.78f, 0.93f), orthographic: false))
            {
                var father = stage.Spawn(FatherPrefab, new Vector3(-0.38f, 0f, 0f), 172f);
                var mother = stage.Spawn(MotherPrefab, new Vector3(0.34f, 0f, 0.02f), 188f);
                mother.transform.localScale = Vector3.one * 0.94f;
                var child = stage.Spawn(MotherPrefab, new Vector3(0.0f, 0f, -0.32f), 180f);
                child.transform.localScale = Vector3.one * 0.6f;
                var poses = new[]
                {
                    new Pose(father, clips["Idle"]),
                    new Pose(mother, clips["Idle"]),
                    new Pose(child, clips["Idle"]),
                };
                for (int i = 0; i < poses.Length; i++)
                    poses[i].Sample(0.6f * i);
                var childHead = child.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
                childHead.localScale *= 1.25f;
                foreach (var person in new[] { father, mother, child })
                {
                    Smile(person);
                    StripHandProps(person);
                }
                DressChild(child);

                // Arka plan deprem sahnesinin modellerinden: çimen, bahçe çiti, çalılar, ağaç ve apartman.
                var grass = new Color(0.42f, 0.62f, 0.33f);
                stage.Cube(new Vector3(0f, -0.05f, 10f), new Vector3(60f, 0.1f, 60f), grass);
                for (int i = -4; i < 4; i++)
                {
                    stage.Spawn(EnvironmentFolder + "Cit_2m.prefab", new Vector3(i * 2f, 0f, 3.4f), 0f);
                    stage.Spawn(EnvironmentFolder + "Cit_Direk.prefab", new Vector3(i * 2f, 0f, 3.4f), 0f);
                }
                stage.Spawn(EnvironmentFolder + "Cali_2.prefab", new Vector3(-1.9f, 0f, 2.6f), 20f);
                stage.Spawn(EnvironmentFolder + "Cali_1.prefab", new Vector3(2.1f, 0f, 2.8f), 0f);
                stage.Spawn(EnvironmentFolder + "Agac_Yuvarlak_1.prefab", new Vector3(-4.6f, 0f, 7f), 30f);
                stage.Spawn(EnvironmentFolder + "Apartman_E.prefab", new Vector3(9f, 0f, 18f), 180f);

                stage.Light(new Vector3(38f, -28f, 0f), 1.25f);
                RenderSettings.ambientLight = new Color(0.5f, 0.52f, 0.56f);
                var sun = GameObject.Find("BakeLight")?.GetComponent<Light>();
                if (sun != null)
                {
                    sun.shadows = LightShadows.Soft;
                    sun.color = new Color(1f, 0.96f, 0.88f);
                }
                stage.LookAt(new Vector3(0.05f, 1.15f, -5.2f), new Vector3(0f, 0.95f, 0f), 30f);
                photo = PhotoFinish(stage.Capture());
                foreach (var pose in poses)
                    pose.Dispose();
            }
            var style = Style();
            style.familyPhoto = SaveSprite("Aile_Foto", photo, Vector4.zero);
            EditorUtility.SetDirty(style);
            AssetDatabase.SaveAssets();
        }

        /// <summary>FaceController'ın "Happy" ifadesi elle: ağız köşeleri yukarı, kaşlar hafif kalkık.</summary>
        static void Smile(GameObject character)
        {
            var face = character.GetComponentInChildren<FaceController>(true);
            if (face == null)
                return;
            var t = face.transform;
            Tilt(t.Find("Mouth/Mouth_L"), -1f, 20f);
            Tilt(t.Find("Mouth/Mouth_R"), 1f, 20f);
            Tilt(t.Find("Brow_L"), -1f, -6f);
            Tilt(t.Find("Brow_R"), 1f, -6f);
        }

        static void Tilt(Transform part, float side, float degrees)
        {
            if (part != null)
                part.localRotation *= Quaternion.Euler(0f, 0f, side * degrees);
        }

        /// <summary>
        /// Modelin elindeki eşyaları (çanta, tablet, kâğıt) fotoğraf için siler: ele bağlı üçgenlerden dokusu renksiz
        /// (gri/siyah/beyaz) olanlar eşyadır; ten ve manşet renkli olanlar el olarak kalır. Asıl model değişmez.
        /// </summary>
        static void StripHandProps(GameObject character)
        {
            foreach (var skinned in character.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var texture = Readable(BaseMap(skinned.sharedMaterial));
                if (texture == null)
                    continue;
                var mesh = Object.Instantiate(skinned.sharedMesh);
                var uv = mesh.uv;
                var weights = mesh.boneWeights;
                var bones = skinned.bones;
                var triangles = mesh.triangles;
                var kept = new List<int>(triangles.Length);
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    bool onHand = true;
                    for (int j = 0; j < 3 && onHand; j++)
                    {
                        string bone = bones[weights[triangles[t + j]].boneIndex0].name;
                        onHand = bone.Contains("LeftHand") || bone.Contains("RightHand");
                    }
                    if (onHand)
                    {
                        var center = (uv[triangles[t]] + uv[triangles[t + 1]] + uv[triangles[t + 2]]) / 3f;
                        var color = texture.GetPixelBilinear(center.x, center.y);
                        float saturation = Mathf.Max(color.r, color.g, color.b) - Mathf.Min(color.r, color.g, color.b);
                        if (saturation < 0.12f)
                            continue;
                    }
                    kept.Add(triangles[t]);
                    kept.Add(triangles[t + 1]);
                    kept.Add(triangles[t + 2]);
                }
                mesh.SetTriangles(kept, 0);
                skinned.sharedMesh = mesh;
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>Elif annesinin küçük kopyası gibi durmasın: lacivert takım sarıya, kırmızı gömlek beyaza boyanır.</summary>
        static void DressChild(GameObject child)
        {
            var dress = new Color(0.98f, 0.76f, 0.26f);
            var blouse = new Color(0.97f, 0.96f, 0.92f);
            foreach (var skinned in child.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var texture = Readable(BaseMap(skinned.sharedMaterial));
                if (texture == null)
                    continue;
                var pixels = texture.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    var c = pixels[i];
                    float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                    if (c.b > c.r + 0.1f && c.b > c.g + 0.05f)
                        pixels[i] = dress * Mathf.Clamp(lum / 0.24f, 0.55f, 1.15f);
                    else if (c.r - c.g > 0.35f)
                        pixels[i] = blouse * Mathf.Clamp(lum / 0.33f, 0.7f, 1.05f);
                    pixels[i].a = 1f;
                }
                texture.SetPixels(pixels);
                texture.Apply();
                var material = new Material(skinned.sharedMaterial);
                material.SetTexture("_BaseMap", texture);
                material.mainTexture = texture;
                skinned.sharedMaterial = material;
            }
        }

        static Texture BaseMap(Material material) =>
            material == null ? null : material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null ? material.GetTexture("_BaseMap") : material.mainTexture;

        /// <summary>Okunamayan (sıkıştırılmış) dokunun okunabilir kopyası.</summary>
        static Texture2D Readable(Texture source)
        {
            if (source == null)
                return null;
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, rt);
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }

        /// <summary>Basılı fotoğraf hissi: hafif sıcak ton ve kenarlarda koyulaşma.</summary>
        static Texture2D PhotoFinish(Texture2D source)
        {
            int w = source.width, h = source.height;
            var pixels = source.GetPixels();
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w - 0.5f, dy = (y + 0.5f) / h - 0.5f;
                float vignette = 1f - 0.35f * Mathf.Pow(Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) * 1.45f), 2.2f);
                var c = pixels[y * w + x];
                c = new Color(c.r * 1.04f + 0.02f, c.g * 1.0f + 0.01f, c.b * 0.92f, 1f) * vignette;
                c.a = 1f;
                pixels[y * w + x] = c;
            }
            source.SetPixels(pixels);
            source.Apply();
            return source;
        }

        // ---------------------------------------------------------------- spiker videosu

        [MenuItem("Dropy/Haber Görselleri/5 Spiker Videosu")]
        public static void BakeAnchorVideo()
        {
            const int width = 640, height = 576, fps = 25, seconds = 8;
            string path = $"{ArtFolder}/Spiker.mp4";
            Directory.CreateDirectory(ArtFolder);
            var clips = Clips();
            using (var stage = new Stage(width, height, Navy, orthographic: false))
            {
                var anchor = stage.Spawn(AnchorPrefab, Vector3.zero, 180f);
                var pose = new Pose(anchor, clips["SitIdle"]);
                pose.Sample(0f);
                var animator = anchor.GetComponentInChildren<Animator>();
                var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                var neckBone = animator.GetBoneTransform(HumanBodyBones.Neck);
                var chestBone = animator.GetBoneTransform(HumanBodyBones.Chest);
                var head = headBone.position;

                // Stüdyo: arkada lacivert paneller, kent silueti ve logo; önde haber masası.
                var studio = stage.Canvas(7f);
                Box(studio, new Color(0.05f, 0.15f, 0.27f), 320, 288, 640, 576);
                for (int i = 0; i < 9; i++)
                {
                    float bw = 40 + (i * 37) % 50, bh = 90 + (i * 53) % 150;
                    Box(studio, new Color(0.09f, 0.22f, 0.37f), 30 + i * 72, 180 + bh / 2f, bw, bh);
                }
                Box(studio, new Color(0.12f, 0.3f, 0.5f), 320, 470, 640, 6);
                Label(studio, Style().tvFont, "K6", 150, new Color(0.12f, 0.3f, 0.5f), 400, 420, 240, 150, TextAlignmentOptions.Center, FontStyles.Bold);
                var desk = stage.Cube(new Vector3(0f, head.y - 0.62f, -0.42f), new Vector3(1.8f, 0.5f, 0.5f), new Color(0.07f, 0.1f, 0.16f));
                stage.Cube(desk.position + new Vector3(0f, 0.05f, -0.26f), new Vector3(1.8f, 0.06f, 0.02f), new Color(0.75f, 0.13f, 0.11f));
                stage.Light(new Vector3(30f, 15f, 0f), 1.15f);
                stage.LookAt(head + new Vector3(0.12f, -0.12f, -1.8f), head + new Vector3(0.12f, -0.16f, 0f), 36f);

                // Gözler ve ağız sabit; yalnız kaşlar konuşmaya eşlik eder. Baş sallanınca alna gömülmesinler diye biraz öndeler.
                var face = anchor.GetComponentInChildren<FaceController>(true)?.transform;
                var brows = new[] { face?.Find("Brow_L"), face?.Find("Brow_R") };
                var browPos = new Vector3[2];
                var browRot = new Quaternion[2];
                for (int i = 0; i < 2; i++)
                {
                    if (brows[i] == null)
                        continue;
                    brows[i].localPosition += new Vector3(0f, 0f, AnchorBrowForward / face.lossyScale.z);
                    browPos[i] = brows[i].localPosition;
                    browRot[i] = brows[i].localRotation;
                }

                var headRest = headBone.localRotation;
                var neckRest = neckBone != null ? neckBone.localRotation : Quaternion.identity;
                var chestRest = chestBone != null ? chestBone.localRotation : Quaternion.identity;
                var attributes = new VideoTrackAttributes
                {
                    frameRate = new MediaRational(fps),
                    width = width,
                    height = height,
                    includeAlpha = false,
                    bitRateMode = VideoBitrateMode.High
                };
                if (File.Exists(path))
                    AssetDatabase.DeleteAsset(path);
                using (var encoder = new MediaEncoder(path, attributes))
                {
                    int frames = fps * seconds;
                    for (int f = 0; f < frames; f++)
                    {
                        // Döngü dikişsiz olsun diye bütün hareketler 8 saniyenin tam katı periyotlarda.
                        float t = f / (float)fps;
                        float phase = t / seconds * Mathf.PI * 2f;
                        float talk = 0.5f + 0.5f * Mathf.Sin(phase * 3f);
                        float nod = Mathf.Sin(phase * 22f) * 2.2f * talk + Mathf.Sin(phase * 2f) * 1.5f;
                        float yaw = Mathf.Sin(phase) * 7f + Mathf.Sin(phase * 5f) * 2f;
                        float tilt = Mathf.Sin(phase * 2f + 1f) * 2.5f;
                        headBone.localRotation = headRest * Quaternion.Euler(nod, yaw, tilt);
                        if (neckBone != null)
                            neckBone.localRotation = neckRest * Quaternion.Euler(nod * 0.4f, yaw * 0.4f, 0f);
                        if (chestBone != null)
                            chestBone.localRotation = chestRest * Quaternion.Euler(Mathf.Sin(phase * 4f) * 0.8f, Mathf.Sin(phase) * 1.5f, 0f);
                        // Kaş: vurgulu kelimelerde kalkar, iç uçları hafifçe yukarı döner (FaceController'ın eğim yönüyle aynı).
                        float lift = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phase * 11f)), 3f) * talk + Mathf.Max(0f, Mathf.Sin(phase * 4f + 0.5f)) * 0.3f;
                        for (int i = 0; i < 2; i++)
                        {
                            if (brows[i] == null)
                                continue;
                            float side = i == 0 ? -1f : 1f;
                            brows[i].localPosition = browPos[i] + new Vector3(0f, lift * AnchorBrowLift / face.lossyScale.y, 0f);
                            brows[i].localRotation = browRot[i] * Quaternion.Euler(0f, 0f, side * -7f * lift);
                        }
                        var frame = stage.Capture();
                        encoder.AddFrame(frame);
                        Object.DestroyImmediate(frame);
                    }
                }
                pose.Dispose();
            }
            AssetDatabase.ImportAsset(path);
            // Windows'un H.264 çözücüsü bu dosyayı oynatmıyor; Unity'nin her platformda çalışan VP8'ine çevrilir.
            var importer = (VideoClipImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultTargetSettings;
            settings.enableTranscoding = true;
            settings.codec = VideoCodec.VP8;
            importer.defaultTargetSettings = settings;
            importer.SaveAndReimport();
            var style = Style();
            style.defaultAnchorClip = AssetDatabase.LoadAssetAtPath<VideoClip>(path);
            EditorUtility.SetDirty(style);
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- ortak

        static NewsStyle Style()
        {
            var style = AssetDatabase.LoadAssetAtPath<NewsStyle>(StylePath);
            if (style == null)
            {
                style = ScriptableObject.CreateInstance<NewsStyle>();
                AssetDatabase.CreateAsset(style, StylePath);
            }
            return style;
        }

        static Dictionary<string, AnimationClip> Clips()
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ClipsPath))
                if (o is AnimationClip clip && !clip.name.StartsWith("__preview"))
                    clips[clip.name] = clip;
            return clips;
        }

        static Vector3 Head(GameObject character)
        {
            var animator = character.GetComponentInChildren<Animator>();
            var bone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            return bone != null ? bone.position : character.transform.position + Vector3.up * 1.6f;
        }

        static Sprite SaveSprite(string name, Texture2D texture, Vector4 border)
        {
            Directory.CreateDirectory(ArtFolder);
            string path = $"{ArtFolder}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // UI çizim yardımcıları: koordinatlar piksel, sol alttan; kutular merkezden yerleşir.
        static RectTransform Box(RectTransform parent, Color color, float x, float y, float w, float h, float rotation = 0f, Sprite sprite = null)
        {
            var go = new GameObject("Box", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            rt.localRotation = Quaternion.Euler(0f, 0f, rotation);
            return rt;
        }

        static void Dot(RectTransform parent, Color color, float x, float y, float size) => Box(parent, color, x, y, size, size, 0f, PrintArt.Disc);

        static void Ring(RectTransform parent, Color color, float x, float y, float size)
        {
            Box(parent, color, x, y, size, size, 0f, Ui.Ellipse);
        }

        static void Line(RectTransform parent, Vector2 a, Vector2 b, float width, Color color)
        {
            var mid = (a + b) / 2f;
            var delta = b - a;
            Box(parent, color, mid.x, mid.y, delta.magnitude + width * 0.5f, width, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        /// <summary>Yazı: x sol kenar, y dikey orta.</summary>
        static TextMeshProUGUI Label(RectTransform parent, TMP_FontAsset font, string text, float size, Color color,
            float x, float y, float w, float h, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
                tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = style;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return tmp;
        }

        /// <summary>
        /// Geçici çekim sahnesi: ayrı bir sahnede kamera, ışık, karakter ve UI kurulur, tek kare RenderTexture'a
        /// çizilip okunur, iş bitince sahne kaydedilmeden kapanır. Ofis sahnesi görüntüye girmez (ayrı katman).
        /// </summary>
        sealed class Stage : System.IDisposable
        {
            readonly Scene scene;
            readonly Scene previous;
            readonly Camera camera;
            readonly RenderTexture target;
            readonly int width, height;

            public Stage(int w, int h, Color background, bool orthographic)
            {
                width = w;
                height = h;
                previous = SceneManager.GetActiveScene();
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var go = new GameObject("BakeCamera");
                camera = go.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = background;
                camera.cullingMask = 1 << BakeLayer;
                camera.orthographic = orthographic;
                camera.orthographicSize = h / 2f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 50f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                target = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                camera.targetTexture = target;
            }

            public RectTransform Canvas(float distance)
            {
                var go = new GameObject("BakeCanvas", typeof(RectTransform));
                var canvas = go.AddComponent<UnityEngine.Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = distance;
                var scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;
                var page = new GameObject("Page", typeof(RectTransform));
                page.transform.SetParent(go.transform, false);
                var rt = (RectTransform)page.transform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(width, height);
                return rt;
            }

            public GameObject Spawn(string prefabPath, Vector3 position, float yaw)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                // Editörde tek karede art arda çekim yapılıyor: deri her çekimde yeniden hesaplanmazsa kemikler dönerken
                // gövde ilk pozda kalır, kafaya bağlı kaş/göz/ağız yüzün içine girer.
                foreach (var skinned in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    skinned.forceMatrixRecalculationPerRender = true;
                return go;
            }

            public Transform Cube(Vector3 position, Vector3 size, Color color)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.position = position;
                cube.transform.localScale = size;
                var renderer = cube.GetComponent<Renderer>();
                var material = new Material(renderer.sharedMaterial) { color = color };
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", color);
                renderer.sharedMaterial = material;
                return cube.transform;
            }

            public void Light(Vector3 euler, float intensity)
            {
                var go = new GameObject("BakeLight");
                var light = go.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = intensity;
                light.cullingMask = 1 << BakeLayer;
                go.transform.rotation = Quaternion.Euler(euler);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.32f, 0.32f, 0.35f);
            }

            public void LookAt(Vector3 eye, Vector3 target, float fieldOfView)
            {
                camera.transform.position = eye;
                camera.transform.LookAt(target);
                camera.fieldOfView = fieldOfView;
            }

            public Texture2D Capture()
            {
                foreach (var root in scene.GetRootGameObjects())
                    SetLayer(root.transform);
                UnityEngine.Canvas.ForceUpdateCanvases();
                foreach (var text in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
                    if (text.gameObject.scene == scene)
                        text.ForceMeshUpdate();
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                    RenderPipeline.SubmitRenderRequest(camera, request);
                else
                    camera.Render();
                var previousActive = RenderTexture.active;
                RenderTexture.active = target;
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = previousActive;
                return tex;
            }

            static void SetLayer(Transform t)
            {
                t.gameObject.layer = BakeLayer;
                foreach (Transform child in t)
                    SetLayer(child);
            }

            public void Dispose()
            {
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                if (previous.IsValid())
                    SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>İnsansı karakteri düzenleme modunda bir animasyonun bir anına sokar (PlayableGraph ile).</summary>
        sealed class Pose : System.IDisposable
        {
            PlayableGraph graph;
            readonly AnimationClipPlayable playable;

            public Pose(GameObject character, AnimationClip clip)
            {
                var animator = character.GetComponentInChildren<Animator>();
                graph = PlayableGraph.Create("BakePose");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
                playable = AnimationClipPlayable.Create(graph, clip);
                output.SetSourcePlayable(playable);
            }

            public void Sample(float time)
            {
                playable.SetTime(time);
                graph.Evaluate(0f);
            }

            public void Dispose()
            {
                if (graph.IsValid())
                    graph.Destroy();
            }
        }
    }
}
