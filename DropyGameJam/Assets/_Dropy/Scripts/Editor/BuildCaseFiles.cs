using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Dropy.Gameplay;

namespace Dropy.EditorTools
{
    /// <summary>
    /// Kilitli tasarımın 15 dosyasını ve gün gün açılan yönetmeliği asset olarak yazar,
    /// açık sahnedeki DeskManager'a bağlar. Tekrar çalıştırmak aynı asset'leri günceller.
    /// </summary>
    public static class BuildCaseFiles
    {
        const string Folder = "Assets/_Dropy/ScriptableObjects/Desk/Files";
        const string RegulationsPath = "Assets/_Dropy/ScriptableObjects/Desk/Regulations.asset";

        public static readonly string[] Paths =
        {
            Folder + "/01_AdaKonutlari.asset",
            Folder + "/02_KiyiEvleri.asset",
            Folder + "/03_MaviApart.asset",
            Folder + "/04_SariSite.asset",
            Folder + "/05_PazarMarket.asset",
            Folder + "/06_ZeytinApart.asset",
            Folder + "/07_TepeEvleri.asset",
            Folder + "/08_SahilRezidans.asset",
            Folder + "/09_PalmiyeKonut.asset",
            Folder + "/10_CinarApart.asset",
            Folder + "/11_IstiklalIlkokulu.asset",
            Folder + "/12_OgrenciYurdu.asset",
            Folder + "/13_LimanKonut.asset",
            Folder + "/14_DenizRezidans.asset",
            Folder + "/15_CarsiUstuKonut.asset",
        };

        [MenuItem("Dropy/Dosyaları Üret")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            var cases = new List<BuildingCase>();
            var specs = Specs();
            for (int i = 0; i < specs.Count; i++)
                cases.Add(Write(Paths[i], specs[i]));

            WriteRegulations();
            AssetDatabase.SaveAssets();

            Verify(cases);
            AssignToScene(cases);
        }

        class Spec
        {
            public string building, contractor, note;
            public int floors, claimed, lab, hammer, approveOverride;
            public GroundUse use;
            public float height = 2.8f;
            public Foundation foundation;
            public ColumnState columns;
            public SoilType soil;
            public string[] intro;
            public string approved, rejected, shearWall;
            public Seal expected;
        }

        static Spec File(string building, string contractor, int floors, int claimed, int lab, SoilType soil, Seal expected, params string[] intro) =>
            new Spec { building = building, contractor = contractor, floors = floors, claimed = claimed, lab = lab, soil = soil, expected = expected, intro = intro };

        static List<Spec> Specs()
        {
            var list = new List<Spec>();

            // 1. gün: beton
            var s = File("Ada Konutları", "Kemal Bey", 3, 30, 30, SoilType.Kaya, Seal.Approve,
                "Günaydın! Ada Konutları. Üç kat, sağlam iş.",
                "Balkon on beş santim taşmış, ona takılmazsınız herhalde.");
            s.note = "Balkon 15 cm taşıyor";
            list.Add(s);

            list.Add(File("Kıyı Evleri", "Nuri Usta", 2, 25, 25, SoilType.Kil, Seal.Approve,
                "İki katlı ufak bir iş. Kâğıtlar tamam, buyurun."));

            s = File("Mavi Apart", "Selma Hanım", 4, 20, 30, SoilType.Kil, Seal.Approve,
                "Künyeye yanlışlıkla C20 yazmışlar.",
                "Laboratuvar raporu ekte, numune C30 çıktı.");
            s.note = "Künyedeki beton sınıfı yanlış yazılmış";
            list.Add(s);

            // 2. gün: yumuşak kat, kesik kolon
            s = File("Sarı Site", "Rıza Bey", 3, 30, 30, SoilType.Kaya, Seal.Approve,
                "Cephe boyası biraz soldu ama binaya bir şey olmaz, değil mi?");
            s.note = "Cephe boyası solmuş";
            list.Add(s);

            s = File("Pazar Market", "Cengiz Bey", 4, 30, 30, SoilType.Kil, Seal.ShearWall,
                "Altı market, üstü daire.",
                "Market tavanını yüksek tuttuk, müşteri ferah sever.");
            s.use = GroundUse.Market;
            s.height = 4.5f;
            list.Add(s);

            list.Add(File("Zeytin Apart", "Ayten Hanım", 3, 35, 35, SoilType.Kaya, Seal.Approve,
                "C35 döktük, sağlam olsun dedik."));

            // 3. gün: kum zemin, çekiç
            s = File("Tepe Evleri", "Hüseyin Usta", 3, 30, 30, SoilType.Kaya, Seal.Approve,
                "Yeni çekiç mi gelmiş? Vurun bakalım, korkmuyoruz.");
            s.hammer = 30;
            list.Add(s);

            s = File("Sahil Rezidans", "Tarık Bey", 6, 30, 30, SoilType.Kum, Seal.Reject,
                "Altı kat, deniz manzaralı.",
                "Radye döktük, kazığa ne gerek var?");
            s.note = "Deniz kumu";
            list.Add(s);

            s = File("Palmiye Konut", "Fikret Bey", 4, 25, 25, SoilType.Kil, Seal.Approve,
                "Korkuluk biraz alçak kaldı, sonra yükseltiriz.");
            s.hammer = 25;
            s.note = "Korkuluk alçak";
            list.Add(s);

            // 4. gün: rüşvet
            s = File("Çınar Apart", "Gül Hanım", 3, 30, 30, SoilType.Kaya, Seal.Approve,
                "Kapıları yeşil boyadık, projede kahverengi yazıyordu. Sorun olmaz değil mi?");
            s.hammer = 30;
            s.note = "Kapı rengi projeden farklı";
            list.Add(s);

            s = File("İstiklal İlkokulu", "Erdal Bey", 4, 30, 30, SoilType.Kil, Seal.ShearWall,
                "Altına market koyduk, okul üst katlarda.",
                "Kira geliri okula gider, herkes kazanır.");
            s.hammer = 30;
            s.use = GroundUse.Market;
            s.height = 4.5f;
            list.Add(s);

            s = File("Öğrenci Yurdu", "Ozan Bey", 5, 30, 30, SoilType.Kaya, Seal.Reject,
                "Yurt acil, dönem başlıyor. Çocuklar kapıda kalmasın.",
                "Siz onayı basın, gerisini ben hallederim. Zahmetinize karşılık... dört bin.");
            s.hammer = 30;
            s.foundation = Foundation.Kazik;
            s.columns = ColumnState.Kesik;
            s.approveOverride = 4000;
            s.approved = "Akıllı adamsınız. Kimse bir şey demez.";
            list.Add(s);

            // 5. gün: para yok
            s = File("Liman Konut", "Bekir Usta", 4, 25, 25, SoilType.Kil, Seal.Approve,
                "Hızlıca bakıverin hocam, millet ev bekliyor.");
            s.hammer = 25;
            list.Add(s);

            s = File("Deniz Rezidans", "Levent Bey", 5, 30, 30, SoilType.Kaya, Seal.Reject,
                "Kâğıtlar tertemiz, iki rapor da C30.",
                "Çekiçle uğraşmayın, vaktiniz kıymetli.");
            s.hammer = 16;
            s.foundation = Foundation.Kazik;
            list.Add(s);

            s = File("Çarşı Üstü Konut", "Sevgi Hanım", 3, 35, 35, SoilType.Kaya, Seal.ShearWall,
                "Alt kat çarşı, beş metre tavan. Ferah oldu.");
            s.hammer = 35;
            s.use = GroundUse.Market;
            s.height = 5f;
            list.Add(s);

            return list;
        }

        static BuildingCase Write(string path, Spec spec)
        {
            var c = AssetDatabase.LoadAssetAtPath<BuildingCase>(path);
            if (c == null)
            {
                c = ScriptableObject.CreateInstance<BuildingCase>();
                AssetDatabase.CreateAsset(c, path);
            }
            c.contractorName = spec.contractor;
            c.buildingName = spec.building;
            c.floors = spec.floors;
            c.claimedConcrete = spec.claimed;
            c.groundUse = spec.use;
            c.groundFloorHeight = spec.height;
            c.foundation = spec.foundation;
            c.columns = spec.columns;
            c.hasShearWall = false;
            c.labConcrete = spec.lab;
            c.soil = spec.soil;
            c.note = spec.note;
            c.hammerConcrete = spec.hammer;
            c.approveFeeOverride = spec.approveOverride;
            c.introLines = new List<string>(spec.intro);
            if (spec.approved != null)
                c.approvedLine = spec.approved;
            EditorUtility.SetDirty(c);
            return c;
        }

        static void WriteRegulations()
        {
            var data = AssetDatabase.LoadAssetAtPath<RegulationData>(RegulationsPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<RegulationData>();
                AssetDatabase.CreateAsset(data, RegulationsPath);
            }
            data.bookTitle = "YÖNETMELİK";
            data.pages = new List<RegulationPage>
            {
                new RegulationPage
                {
                    day = 1, title = "Mühürler",
                    body = "<b>ONAY</b>: +1000 TL. Bina yapılır.\n\n" +
                           "<b>RED</b>: 0 TL. Bina yapılmaz.\n\n" +
                           "<b>PERDE EKLE</b>: +500 TL, günde bir kez. Binaya deprem perdesi (taşıyıcı duvar) eklenir, " +
                           "sonra onaylanır."
                },
                new RegulationPage
                {
                    day = 1, title = "Beton",
                    body = "Gerçek beton yalnız <b>C25, C30 ya da C35</b> olabilir. Başka her beton: RED.\n\n" +
                           "Künye ile ek rapor farklıysa <b>ek rapor</b> geçerlidir.\n\n" +
                           "Kitapta yazmayan kusur (balkon, boya, kapı rengi, korkuluk) reddettirmez: ONAY."
                },
                new RegulationPage
                {
                    day = 2, title = "Yumuşak kat",
                    body = "Zemin kat <b>market</b> ve tavanı <b>4 m ya da daha yüksek</b>, perde yoksa: yumuşak kat. " +
                           "PERDE EKLE ya da RED.\n\n" +
                           "Kolon <b>kesik</b> ise yalnız RED. Perde kesik kolonu kurtarmaz."
                },
                new RegulationPage
                {
                    day = 3, title = "Zemin ve çekiç",
                    body = "<b>Kum</b> zemin ve <b>5 ya da daha fazla kat</b>: kazık temel şart. Radye temel: RED.\n\n" +
                           "<b>Çekiç</b> betonun gerçek dayanımını ölçer. Çekicin okuduğu beton iki kâğıdı da geçersiz kılar."
                },
            };
            EditorUtility.SetDirty(data);
        }

        /// <summary>Kurallardan çıkan mühürün tasarımdaki mühürle ve ekonominin dürüst yolla tuttuğunu denetler.</summary>
        static void Verify(List<BuildingCase> cases)
        {
            var specs = Specs();
            int money = Economy.StartMoney;
            bool ok = true;
            for (int day = 1; day <= Economy.Days; day++)
            {
                int income = 0;
                for (int k = 0; k < Economy.FilesPerDay; k++)
                {
                    int i = (day - 1) * Economy.FilesPerDay + k;
                    var seal = BuildingRules.CorrectSeal(cases[i]);
                    if (seal != specs[i].expected)
                    {
                        Debug.LogError($"{cases[i].buildingName}: kurallar {seal} diyor, tasarım {specs[i].expected}.");
                        ok = false;
                    }
                    income += BuildingRules.Fee(cases[i], seal, day);
                }
                money += income;
                if (day <= Economy.LastPaidDay)
                    money -= Economy.Rent;
                if (day == 1) money -= Shop.Price(ShopItem.Bag);
                if (day == 2) money -= Shop.Price(ShopItem.Fixing);
                if (day == 4) money -= Shop.Price(ShopItem.Retrofit);
                Debug.Log($"Dürüst yol, {day}. gün: mühür +{income}, akşam kasa {money}");
                if (money < 0)
                    ok = false;
            }
            if (money != 0)
                ok = false;
            if (ok)
                Debug.Log("15 dosya yazıldı. Mühürler tasarımla aynı, dürüst tam yol kasayı 0'da bitiriyor.");
            else
                Debug.LogError($"Dosyalar tasarımla tutmuyor. Dürüst yol sonu kasa: {money}");
        }

        static void AssignToScene(List<BuildingCase> cases)
        {
            var desks = Object.FindObjectsByType<DeskManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var desk in desks)
            {
                var so = new SerializedObject(desk);
                var prop = so.FindProperty("cases");
                prop.arraySize = cases.Count;
                for (int i = 0; i < cases.Count; i++)
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = cases[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(desk.gameObject.scene);
                EditorSceneManager.SaveScene(desk.gameObject.scene);
                Debug.Log($"{desk.gameObject.scene.name} sahnesindeki DeskManager 15 dosyaya bağlandı.");
            }
        }
    }
}
