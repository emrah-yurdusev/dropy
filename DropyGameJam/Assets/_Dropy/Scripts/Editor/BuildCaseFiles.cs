using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Dropy.Gameplay;

namespace Dropy.EditorTools
{
    /// <summary>
    /// Masanın içeriğini asset olarak yazar ve açık sahnedeki DeskManager'a bağlar: 8 dosya (4 gün × 2), müteahhitler,
    /// kişilik tipleri, gün gün açılan yönetmelik ve hikâye (telefon, televizyon, mesajlar, aile istekleri, sarsıntı).
    /// Dosyalar, yönetmelik ve hikâye her çalıştırmada yeniden yazılır. Kişilik ve müteahhit dosyaları yalnızca yoksa
    /// oluşturulur: Inspector'da yapılan konuşma düzenlemeleri tekrar çalıştırınca kaybolmaz.
    /// </summary>
    public static class BuildCaseFiles
    {
        const string Folder = "Assets/_Dropy/ScriptableObjects/Desk/Files";
        const string PersonalityFolder = "Assets/_Dropy/ScriptableObjects/Desk/Personalities";
        const string ContractorFolder = "Assets/_Dropy/ScriptableObjects/Desk/Contractors";
        const string RegulationsPath = "Assets/_Dropy/ScriptableObjects/Desk/Regulations.asset";
        public const string StoryPath = "Assets/_Dropy/ScriptableObjects/Desk/Story.asset";
        public const string TutorialPath = "Assets/_Dropy/ScriptableObjects/Desk/Tutorial.asset";
        const string VeyselPrefab = "Assets/_Dropy/Prefabs/Characters/Veysel_LowPoly.prefab";
        const string SefaPrefab = "Assets/_Dropy/Prefabs/Characters/SefaBey_LowPoly.prefab";
        const string CharacterFolder = "Assets/_Dropy/Prefabs/Characters/";

        /// <summary>Oyun sırası: 4 gün × 2 dosya. Eski (18 dosyalık döngüden) asset'ler adları değişmesin diye korunur.</summary>
        public static readonly string[] Paths =
        {
            Folder + "/01_AdaKonutlari.asset",
            Folder + "/02_KiyiEvleri.asset",
            Folder + "/05_PazarMarket.asset",
            Folder + "/19_CesmeApartmani.asset",
            Folder + "/20_LaleApart.asset",
            Folder + "/12_OgrenciYurdu.asset",
            Folder + "/10_CinarApart.asset",
            Folder + "/03_MaviApart.asset",
        };

        // Dosya sırası (0'dan): hikâye bağlantıları bunlara göre kurulur.
        const int Yurt = 5;

        [MenuItem("Dropy/Dosyaları Üret")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            var contractors = WriteContractors();
            var cases = new List<BuildingCase>();
            var specs = Specs();
            for (int i = 0; i < specs.Count; i++)
                cases.Add(Write(Paths[i], specs[i], contractors));

            WriteRegulations();
            WriteNewsTexts();
            var story = WriteStory(cases);
            var tutorial = WriteTutorial();
            AssetDatabase.SaveAssets();

            Verify(cases);
            VerifyFamilyPaths(cases, story);
            AssignToScene(cases, story, tutorial);
        }

        // ---------------------------------------------------------------- gazete yazıları

        const string NewsStylePath = "Assets/_Dropy/ScriptableObjects/Desk/NewsStyle.asset";

        /// <summary>
        /// Gazetenin cümlelerini, adını ve dolgu haberlerini koddaki (Fayköy, çocuklara uygun) hâline getirir.
        /// Görseller, fontlar ve renkler (Inspector ayarları) korunur.
        /// </summary>
        static void WriteNewsTexts()
        {
            var style = AssetDatabase.LoadAssetAtPath<NewsStyle>(NewsStylePath);
            if (style == null)
                return;
            var defaults = ScriptableObject.CreateInstance<NewsStyle>();
            style.texts = new NewspaperTexts();
            style.masthead = defaults.masthead;
            style.fillers = new List<string>(defaults.fillers);
            Object.DestroyImmediate(defaults);
            EditorUtility.SetDirty(style);
        }

        // ---------------------------------------------------------------- kalem öğretici

        /// <summary>
        /// Öğretici yalnız 1. gündedir: 1. dosya beton, ek raporun önceliği ve çekiç; 2. dosya yumuşak kat (perde) ve kesik kolon.
        /// Adımlar her çalıştırmada yeniden yazılır; font, mürekkep rengi ve hız gibi Inspector ayarları korunur.
        /// </summary>
        static TutorialData WriteTutorial()
        {
            var tutorial = AssetDatabase.LoadAssetAtPath<TutorialData>(TutorialPath);
            if (tutorial == null)
            {
                tutorial = ScriptableObject.CreateInstance<TutorialData>();
                AssetDatabase.CreateAsset(tutorial, TutorialPath);
            }
            // Hamleler arası bekleme: notu okumaya vakit kalsın.
            tutorial.pause = 1f;
            tutorial.steps = new List<TutorialStep>
            {
                // 1. gün, Veysel: künye C20, ek rapor C30, çekiç C30 → ONAY.
                Step(1, 0, PenShape.Arrow, FieldTarget(FieldKey.Claimed), RuleTarget(RuleTopic.Beton), "1) Künyedeki beton kitapta var mı? C25/30/35"),
                Step(1, 0, PenShape.DoubleArrow, FieldTarget(FieldKey.Claimed), FieldTarget(FieldKey.Lab), "2) Farklıysa EK RAPOR geçerli!"),
                Step(1, 0, PenShape.Circle, new TutorialTarget { kind = TutorialTargetKind.Hammer }, null, "3) Emin ol: çekiçle ölç! (günde 1)"),
                // 1. gün, Nuri: market + yüksek tavan = yumuşak kat (perde kurtarır) ama kolon kesik → RED.
                Step(1, 1, PenShape.Arrow, FieldTarget(FieldKey.Height), RuleTarget(RuleTopic.YumusakKat), "1) Altı market + tavan 4 m+ = yumuşak kat"),
                Step(1, 1, PenShape.Underline, SealTarget(Seal.ShearWall), null, "2) Tek kusur buysa PERDE kurtarır"),
                Step(1, 1, PenShape.Circle, FieldTarget(FieldKey.Columns), null, "3) Ama kolon KESİK!"),
                Step(1, 1, PenShape.Circle, new TutorialTarget { kind = TutorialTargetKind.Building }, null, "4) Büyüteçle bak: kırmızı = kusur"),
            };
            EditorUtility.SetDirty(tutorial);
            return tutorial;
        }

        static TutorialStep Step(int day, int fileOfDay, PenShape shape, TutorialTarget from, TutorialTarget to, string note) =>
            new TutorialStep { day = day, fileOfDay = fileOfDay, shape = shape, from = from, to = to ?? new TutorialTarget(), note = note };

        static TutorialTarget FieldTarget(string key) => new TutorialTarget { kind = TutorialTargetKind.Field, field = key };
        static TutorialTarget RuleTarget(RuleTopic topic) => new TutorialTarget { kind = TutorialTargetKind.RuleTab, topic = topic };
        static TutorialTarget SealTarget(Seal seal) => new TutorialTarget { kind = TutorialTargetKind.Seal, seal = seal };

        // ---------------------------------------------------------------- kişilikler ve müteahhitler

        static Dictionary<string, ContractorProfile> WriteContractors()
        {
            Directory.CreateDirectory(PersonalityFolder);
            Directory.CreateDirectory(ContractorFolder);
            var samimi = PersonalityAsset("Samimi", "Sıcak, geveze esnaf. Çay ısmarlar, hanımından bahseder; kuralı bilmez ama iyi niyetlidir.", Samimi);
            var cekingen = PersonalityAsset("Cekingen", "Utangaç, kekeleyen, saf. Kendisi de kandırılmış olabilir; kızmaz, üzülür.", Cekingen);
            var isAdami = PersonalityAsset("IsAdami", "Pratik, aceleci, her şeyi parayla ölçer. Kural tanır ama pazarlık eder.", IsAdami);
            var manipulatif = PersonalityAsset("Manipulatif", "Kibar ama tehditkâr. Aileyi, parayı ve tanıdıklarını koz olarak kullanır.", Manipulatif);
            var tatliDilli = PersonalityAsset("TatliDilli", "Tatlı dilli teyze. Börek ikram eder, torunlarından bahseder; dikkati kusurdan başka yere çeker.", TatliDilli);
            var profesyonel = PersonalityAsset("Profesyonel", "Sakin, net, belgeye hâkim. Tutarsızlığı baştan söyler, haksız rede kayıt düşer.", Profesyonel);
            var eskiToprak = PersonalityAsset("EskiToprak", "Kırk yıllık usta. Kâğıda değil eline güvenir, laboratuvarı küçümser; kabadır ama yalan söylemez.", EskiToprak);
            var hevesli = PersonalityAsset("Hevesli", "Genç, heyecanlı girişimci. Hızlı konuşur, her şeye 'konsept' der; kötü niyetli değil ama riski görmez.", Hevesli);

            // Her müteahhidin kendi modeli var: dosya sırası değişince kimse başkasının yüzüyle gelmez.
            return new Dictionary<string, ContractorProfile>
            {
                ["Veysel"] = ContractorAsset("Veysel", "Veysel Bey", samimi, AssetDatabase.LoadAssetAtPath<GameObject>(VeyselPrefab)),
                ["Nuri"] = ContractorAsset("Nuri", "Nuri Usta", cekingen, Character("Karakter_LowPoly")),
                ["Cengiz"] = ContractorAsset("Cengiz", "Cengiz Bey", isAdami, Character("BusinessMale_1_LowPolyStyle")),
                ["Hasan"] = ContractorAsset("Hasan", "Hasan Usta", eskiToprak, Character("Abi_LowPoly")),
                ["Kerem"] = ContractorAsset("Kerem", "Kerem Bey", hevesli, Character("BusinessMale_4_LowPolyStyle (2)")),
                ["Ozan"] = ContractorAsset("Ozan", "Ozan Bey", manipulatif, Character("BusinessMale_3_LowPolyStyle")),
                ["Gul"] = ContractorAsset("Gul", "Gül Hanım", tatliDilli, Character("Kadin_LowPoly")),
                // 4. günün ikinci dosyası: önceki dosyadaki Gül Hanım'la aynı modeli paylaşmasın diye erkek.
                ["Sefa"] = ContractorAsset("Sefa", "Sefa Bey", profesyonel, AssetDatabase.LoadAssetAtPath<GameObject>(SefaPrefab)),
            };
        }

        static GameObject Character(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFolder + name + ".prefab");

        /// <summary>Kişilik dosyası yoksa oluşturur ve başlangıç cümlelerini yazar; varsa dokunmaz (Inspector düzenlemeleri kalır).</summary>
        static Personality PersonalityAsset(string file, string description, System.Action<VoiceLines> fill)
        {
            string path = $"{PersonalityFolder}/{file}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<Personality>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<Personality>();
            asset.typeName = file;
            asset.description = description;
            asset.lines = new VoiceLines();
            fill(asset.lines);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Müteahhit dosyası yoksa oluşturur; varsa yalnız boş model alanını doldurur (konuşma düzenlemeleri kalır).</summary>
        static ContractorProfile ContractorAsset(string file, string displayName, Personality personality, GameObject prefab = null)
        {
            string path = $"{ContractorFolder}/{file}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ContractorProfile>(path);
            if (asset != null)
            {
                if (asset.prefab == null && prefab != null)
                {
                    asset.prefab = prefab;
                    EditorUtility.SetDirty(asset);
                }
                return asset;
            }
            asset = ScriptableObject.CreateInstance<ContractorProfile>();
            asset.displayName = displayName;
            asset.personality = personality;
            asset.prefab = prefab;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void Add(List<string> list, params string[] lines) => list.AddRange(lines);

        static void Field(VoiceLines v, string key, bool defensive, string text) =>
            v.fields.Add(new FieldLine { field = key, defensive = defensive, text = text });

        static void Samimi(VoiceLines v)
        {
            v.address = "hocam";
            v.typingSpeed = 1.15f;
            Add(v.greetings, "Hayırlı sabahlar {hitap}!", "Selamünaleyküm {hitap}, kolay gelsin!");
            Add(v.firmHonest, "Kitap kitap... Siz bilirsiniz {hitap}, bakın bakalım.");
            Add(v.firmDishonest, "Kitap da bizim kitap {hitap}, bir göz atın yeter.");
            Add(v.softHonest, "Eyvallah {hitap}, buyurun.");
            Add(v.softDishonest, "Eyvallah, eyvallah. Kâğıtlar tertemiz zaten.");
            Field(v, FieldKey.Claimed, false, "{rapor} döktük {hitap}, rapor ortada. Hanımın çeyizini satsam bundan iyisini dökemezdim.");
            Field(v, FieldKey.Lab, false, "Laboratuvar da {rapor} dedi, ikisi bir.");
            Field(v, FieldKey.Note, false, "\"{not}\" mı? Görüntü meselesi {hitap}, binaya bir zararı yok.");
            Add(v.tics, "Çay söyleyeyim mi?", "Hanım da selam söyledi.", "Otuz yılın ustasıyım ben.");
            Add(v.approved, "Eksik olmayın {hitap}! Çayınız benden, bir gün mutlaka.");
            Add(v.rejected, "Otuz yılda ilk kez başıma geliyor {hitap}... Hayırlısı.");
            Add(v.shearWall, "Perde mi? Eh, siz diyorsanız ekletiriz.");
            Add(v.unjustReject, "Beton {rapor}, kolon tam {hitap}. Kitapta reddettirecek ne var, gösterin de öğrenelim.");
        }

        static void Cekingen(VoiceLines v)
        {
            v.address = "efendim";
            v.typingSpeed = 0.8f;
            Add(v.greetings, "Ş-şey... Günaydın efendim.");
            Add(v.firmHonest, "Haklısınız efendim, bakın lütfen.");
            Add(v.firmDishonest, "T-tabii efendim, kitap neyse o. Ben bir yanlış yapmadım ama... değil mi?");
            Add(v.softHonest, "Teşekkür ederim...");
            Add(v.softDishonest, "Sağ olun. Kâğıtlarım tam, inşallah.");
            Field(v, FieldKey.Claimed, true, "Künyede {kunye} yazıyor efendim... Betoncu öyle dedi, ben de inandım.");
            Field(v, FieldKey.Lab, true, "Rapor mu? Ş-şey... Okumadım açıkçası, betoncu getirdi.");
            Add(v.tics, "Kusura bakmayın.", "İlk dosyam da...");
            Add(v.approved, "Gerçekten mi? Allah razı olsun efendim!");
            Add(v.rejected, "Ama... her şey yazdığı gibiydi sanıyordum.");
            Add(v.unjustReject, "Ş-şey... Beton {rapor}, eksik ne var söyler misiniz?");
        }

        static void IsAdami(VoiceLines v)
        {
            v.address = "beyefendi";
            v.typingSpeed = 1.25f;
            Add(v.greetings, "Vakit nakit {hitap}, kısa keselim.");
            Add(v.firmHonest, "Kural kuraldır, saygı duyarım. Buyurun.");
            Add(v.firmDishonest, "Kural da hesap da ortada. Bir yolunu buluruz.");
            Add(v.softHonest, "Güzel. Hızlıca bakarsanız sevinirim.");
            Add(v.softDishonest, "Bakın, eksik ne varsa hallederiz. Para sorun değil.");
            Field(v, FieldKey.Use, true, "Altı market, kira getirir. Ticaret olmadan bina dönmez {hitap}.");
            Field(v, FieldKey.Height, true, "{tavan} metre. Raflar sığacak, müşteri ferah görmek ister.");
            Field(v, FieldKey.ShearWall, true, "Perde koysak vitrin kapanır. Vitrin kapanırsa kira düşer.");
            Add(v.tics, "Hesabı ben yaparım.", "Zaman paradır.");
            Add(v.approved, "Anlaştık. Açılışa davetlisiniz.");
            Add(v.rejected, "Bu kararın bana maliyeti ne, biliyor musunuz?");
            Add(v.shearWall, "Perde mi? Vitrin küçülür ama... tamam, ekletiriz. Masraf benden.");
            Add(v.unjustReject, "Rakamlar ortada: beton {rapor}. Gerekçeyi yazılı isterim.");
        }

        static void Manipulatif(VoiceLines v)
        {
            v.address = "beyim";
            v.typingSpeed = 0.95f;
            Add(v.greetings, "Merhaba {hitap}. Adımı duymuşsunuzdur.");
            Add(v.firmHonest, "Kitaba bağlı adam... Saygı duyarım.");
            Add(v.firmDishonest, "Kitap... Kitabı yazanlar da benim gibi insanlar {hitap}.");
            Add(v.softHonest, "Anlaşacağız, belli.");
            Add(v.softDishonest, "Akıllı adamsınız. Anlaşacağız.");
            Field(v, FieldKey.Columns, true, "Biraz açtık, odalar ferah olsun. Taşır {hitap}, taşır.");
            Add(v.tics, "Hepimizin bir işi var, değil mi?", "Fayköy'de herkes beni tanır.");
            Add(v.approved, "Akıllı adamsınız. Kimse bir şey demez.");
            Add(v.rejected, "Siz bilirsiniz {hitap}. Ben başka kapı çalarım.");
        }

        static void TatliDilli(VoiceLines v)
        {
            v.address = "evladım";
            v.typingSpeed = 0.9f;
            Add(v.greetings, "Hayırlı sabahlar {hitap}, börek getirdim, sıcacık.");
            Add(v.firmHonest, "Tabii {hitap}, sen bilirsin.");
            Add(v.firmDishonest, "Kitap mı? Ay {hitap}, ben anlamam o işlerden, sen bakıver.");
            Add(v.softHonest, "Sağ ol {hitap}.");
            Add(v.softDishonest, "Sağ ol {hitap}, Allah ne muradın varsa versin.");
            Field(v, FieldKey.Claimed, true, "İki kâğıtta da {rapor} yazıyor {hitap}, daha ne olsun?");
            Field(v, FieldKey.Lab, true, "Laboratuvarın çocuğu çok efendi, yanlış yazmaz.");
            Field(v, FieldKey.Hammer, true, "O alet bozuktur {hitap}, nem kapmıştır. Benim kâğıdıma bak sen.");
            Add(v.tics, "Torunlarım için {hitap}.", "Börek soğumasın.");
            Add(v.approved, "Allah razı olsun {hitap}! Torunlara müjdeyi vereyim.");
            Add(v.rejected, "Beton mu? Betoncu bana yemin etmişti {hitap}... Torunlara ne diyeceğim?");
            Add(v.unjustReject, "Ay {hitap}, beton {rapor} diyor kâğıt. Neyi beğenmedin?");
        }

        static void Profesyonel(VoiceLines v)
        {
            v.typingSpeed = 1.1f;
            Add(v.greetings, "Günaydın. Dosyada bir tutarsızlık göreceksiniz, baştan söyleyeyim.");
            Add(v.firmHonest, "Doğru yaklaşım. Ek rapora bakmanızı öneririm.");
            Add(v.firmDishonest, "Kitap ortada. İnceleyin.");
            Add(v.softHonest, "Teşekkürler. Kâğıtlar düzenli.");
            Add(v.softDishonest, "Teşekkürler.");
            Field(v, FieldKey.Claimed, false, "Künyeyi stajyer doldurdu, eski projeden kopyalamış. Geçerli olan ek rapor: {rapor}.");
            Field(v, FieldKey.Lab, false, "Üç numune, yirmi sekiz gün. Usulüne uygun: {rapor}.");
            Field(v, FieldKey.Hammer, false, "Ölçtünüz, {cekic}. Kâğıtlarla aynı.");
            Add(v.tics, "Kayıtlara geçsin.", "Usulüne uygun.");
            Add(v.approved, "Doğru okudunuz. İyi çalışmalar.");
            Add(v.rejected, "Künyedeki bir yazım hatası yüzünden mi? Ek raporu okumadınız mı?");
            Add(v.unjustReject, "Ek rapor {rapor} diyor; kitaba göre geçerli olan o. Kararınızı kayda geçiriyorum.");
        }

        static void EskiToprak(VoiceLines v)
        {
            v.address = "evlat";
            v.typingSpeed = 0.95f;
            Add(v.greetings, "Selamünaleyküm {hitap}. Kırk yıldır bina dikerim, kâğıt işi bana göre değil.");
            Add(v.firmHonest, "Kitap kitap... Bakalım kitabın bildiği neymiş.");
            Add(v.firmDishonest, "Kitabı yazan bir gün harç karıştırsın da görsün {hitap}.");
            Add(v.softHonest, "Eyvallah {hitap}.");
            Add(v.softDishonest, "Eyvallah. Ben kendi betonuma kefilim.");
            Field(v, FieldKey.Claimed, true, "{kunye} yazdım, {kunye} döktüm. Kırk yılın ustası yalan yazmaz.");
            Field(v, FieldKey.Lab, true, "Laboratuvar mı? Numuneyi güneşte bekletmişlerdir {hitap}. Betonu ben kendi elimle karıştırdım.");
            Field(v, FieldKey.Hammer, true, "O alet ne anlar betondan? Elimi vursam söylerim kaç olduğunu.");
            Add(v.tics, "Kırk yıl {hitap}, kırk yıl.", "Bizim zamanımızda kâğıt mı vardı?");
            Add(v.approved, "Hah şöyle! Usta malı, gönül rahatlığıyla taşınsınlar.");
            Add(v.rejected, "Kırk yıllık ustaya kâğıt mı öğretiyorsunuz? ...Peki. Yeniden döktürürüz.");
            Add(v.unjustReject, "Kâğıdınız bile beton {rapor} diyor. Kırk yıllık emeği neden çiziyorsunuz?");
        }

        static void Hevesli(VoiceLines v)
        {
            v.address = "abi";
            v.typingSpeed = 1.35f;
            Add(v.greetings, "Günaydın {hitap}! Çok heyecanlıyım, ilk projem bu.");
            Add(v.firmHonest, "Tabii tabii, kurallar önemli, bakın lütfen!");
            Add(v.firmDishonest, "Kural mı? Ama konsept... Neyse, bakın siz.");
            Add(v.softHonest, "Süpersiniz {hitap}, teşekkürler!");
            Add(v.softDishonest, "Süper! Zaten her şey çok modern.");
            Field(v, FieldKey.Use, true, "Altta konsept market var {hitap}! Kahve köşesi, organik raflar, tam bir deneyim.");
            Field(v, FieldKey.Height, true, "{tavan} metre! Tavan yüksek olunca mekân nefes alıyor.");
            Field(v, FieldKey.ShearWall, true, "Perde koyarsak vitrin kapanır {hitap}, konsepti bozar.");
            Add(v.tics, "Sosyal medyada çok tutacak.", "Ön satışlar uçtu {hitap}.");
            Add(v.approved, "Harika! Açılışta ilk kahve benden {hitap}!");
            Add(v.rejected, "Red mi? Ama ön satışlar... Yatırımcıya ne diyeceğim?");
            Add(v.shearWall, "Perde mi? Vitrin biraz daralır ama... olur, güvenlik de bir konsept!");
            Add(v.unjustReject, "Ama beton {rapor}, kolonlar tam... Neyi eksik gördünüz {hitap}?");
        }

        // ---------------------------------------------------------------- dosyalar

        class Spec
        {
            public string building, contractor, note, noteAnswer;
            public int floors, claimed, lab, hammer, approveOverride, illegalFloors;
            public GroundUse use;
            public float height = 2.8f;
            public Foundation foundation;
            public ColumnState columns;
            public SoilType soil;
            public string[] intro;
            public string approved, rejected, shearWall;
            public string headline, standing;
            public Seal expected;
            public readonly List<CaseQuestion> questions = new List<CaseQuestion>();
            public readonly List<PressureLine> pressure = new List<PressureLine>();

            public Spec Press(PressureCondition condition, string text)
            {
                pressure.Add(new PressureLine { condition = condition, text = text });
                return this;
            }

            public Spec Ask(string question, string answer)
            {
                questions.Add(new CaseQuestion { question = question, answer = answer });
                return this;
            }

            public Spec News(string onCollapse, string onStanding = null)
            {
                headline = onCollapse;
                standing = onStanding;
                return this;
            }
        }

        static Spec File(string building, string contractor, int floors, int claimed, int lab, SoilType soil, Seal expected, params string[] intro) =>
            new Spec { building = building, contractor = contractor, floors = floors, claimed = claimed, lab = lab, soil = soil, expected = expected, intro = intro };

        /// <summary>
        /// Her dosya tek bir şey öğretir. Giriş en fazla 2 cümle; mühür sonrası cümleler boşsa müteahhidin kişiliğinden gelir,
        /// dolu olanlar yalnız o binaya özeldir. Diyalog ilkesi: müteahhit cevabı söylemez, ipucu kâğıttadır.
        /// </summary>
        static List<Spec> Specs()
        {
            var list = new List<Spec>();

            // ---- 1. gün: öğretici. Beton, ek raporun önceliği ve çekiç; sonra yumuşak kat (perde) ve kesik kolon.
            // Künyede C20 yazıyor ama ek rapor da çekiç de C30: geçerli olan ek rapor, bina sağlam → ONAY.
            var s = File("Ada Konutları", "Veysel", 3, 20, 30, SoilType.Kaya, Seal.Approve,
                    "Ada Konutları, üç kat. Betonu Fayköy'ün laboratuvarında test ettirdik, rapor ekte {hitap}.")
                .Ask("Künyede neden C20 yazıyor?", "Künyeyi yeğenim doldurdu {hitap}, rakamı yanlış yazmış. Döktüğümüz beton raporda yazıyor.")
                .Ask("Betonu kim test etti?", "Fayköy'ün laboratuvarı. İçiniz rahat etmezse çekiçle vurun, görün.")
                .News("Ada Konutları çöktü", "Ada Konutları ayakta: Veysel Bey mahalleye çay dağıttı.");
            s.hammer = 30;
            list.Add(s);

            // Altı market, tavan 4,5 m: tek kusur bu olsa perdeyle kurtulurdu; ama kolon da kesik → yalnız RED.
            s = File("Kıyı Evleri", "Nuri", 4, 30, 30, SoilType.Kil, Seal.Reject,
                    "İlk kez dosya getiriyorum. Altta bakkal olacak, üstünde üç daire; birinde annemle babam oturacak.")
                .Ask("Zemin katın tavanı neden bu kadar yüksek?", "Bakkal yüksek raf istedi {hitap}... Tavanı biraz kaldırdık.")
                .Ask("Kolonlar neden kesik?", "K-kesik mi? Usta, dükkân ferah olsun diye ortadakini almış... Bana sağlam dedi.")
                .News("Kıyı Evleri çöktü: kolonu kesilmiş zemin kat göçtü", "Kıyı Evleri hiç yapılmadı; Nuri Usta kolonları baştan döküyor.");
            s.use = GroundUse.Market;
            s.height = 4.5f;
            s.columns = ColumnState.Kesik;
            s.rejected = "Kolon mu? Ustaya güvenmiştim... Annemlere ne diyeceğim?";
            s.shearWall = "Perde mi? Olur {hitap}, ekletirim... Kolonu da düzeltir mi peki?";
            list.Add(s);

            // ---- 2. gün: öğreticisiz pratik. Tek kusuru yumuşak kat olan bina (perde), sonra ek raporu çürük usta işi.
            s = File("Pazar Market", "Cengiz", 4, 30, 30, SoilType.Kil, Seal.ShearWall,
                    "Zemine zincir market giriyor, kira garantili. Tavanı yüksek tuttuk, raflar sığsın diye.")
                .Ask("Zemin katta duvar var mı?", "Market bu {hitap}, duvar olur mu? Baştan başa cam vitrin.")
                .News("Pazar Market'in zemin katı çöktü", "Pazar Market ayakta: perde zemin katı tuttu.");
            s.use = GroundUse.Market;
            s.height = 4.5f;
            list.Add(s);

            // Künye C30 ama ek rapor (ve çekiç) C20: 1. günün tersi, bu kez geçerli olan rapor binayı düşürür → RED.
            s = File("Çeşme Apartmanı", "Hasan", 5, 30, 20, SoilType.Kaya, Seal.Reject,
                    "Çeşme Apartmanı, beş kat. Temelinden çatısına kendi ellerimle diktim {hitap}.")
                .Ask("Ek raporda neden C20 yazıyor?", "Laboratuvar numuneyi üç gün güneşte bekletmiş {hitap}. Betonu ben karıştırdım, C30'dur o.")
                .Ask("Betonu nasıl ölçtünüz?", "Göz kararı {hitap}. Kırk yıldır elim terazidir, şaşmaz.")
                .News("Çeşme Apartmanı çöktü: betonu C20 çıktı", "Çeşme Apartmanı ayakta.");
            s.hammer = 20;
            list.Add(s);

            // ---- 3. gün: yumuşak kat sınırı (tavan tam 4 m → perde), sonra kesik kolon ve rüşvet. Ozan'ın dosyasında hafif sarsıntı.
            s = File("Lale Apart", "Kerem", 4, 30, 30, SoilType.Kil, Seal.ShearWall,
                    "Lale Apart! Altta konsept bir market açıyoruz, üstünde altı daire. Hepsi ön satışta gitti {hitap}!")
                .Ask("Zemin kat tavanı kaç metre?", "Tam dört metre {hitap}! Projede ne yazıyorsa o, bir santim şaşmadık.")
                .Ask("Zemin katta taşıyıcı duvar var mı?", "Yok, baştan sona cam vitrin. Konsept bu, açıklık lazım.")
                .News("Lale Apart'ın zemin katı çöktü", "Lale Apart ayakta: perde vitrini de aileleri de korudu.");
            s.use = GroundUse.Market;
            s.height = 4f;
            list.Add(s);

            // Ozan sarsıntının hemen ardından girer: sallantıyı geçiştirir (cevap kartı açmasın diye soru sormaz).
            s = File("Öğrenci Yurdu", "Ozan", 5, 30, 30, SoilType.Kaya, Seal.Reject,
                    "Yer biraz oynadı. Siz keyfinizi bozmayın, Fayköy'ün binaları böyle şeylere alışık.",
                    "Yurt acil, dönem başlıyor. İki yüz öğrenci kapıda kalmasın.",
                    "Siz onayı basın, zahmetinize karşılık kırk bin. Kimse bir şey demez.")
                .Ask("Kolonlar neden kesik?", "Odalar geniş olsun diye biraz açtık. Taşır {hitap}, taşır.")
                .News("Öğrenci yurdu çöktü", "Öğrenci Yurdu ayakta.");
            s.foundation = Foundation.Kazik;
            s.columns = ColumnState.Kesik;
            // Künyede 5 kat; çatıya bir kat daha çıkılmış. Yalnız bina incelemesinde büyüteçle görünür.
            s.illegalFloors = 1;
            s.approveOverride = 40000;
            // Yalan yok: yalnızca oyuncunun gerçek durumunu kullanır ({kasa}, {eksik}, {hazirlik} kira sonrası hesaplanır).
            s.Press(PressureCondition.Always, "Bir dakika. Acele etmeyin, bir daha düşünün.")
                .Press(PressureCondition.PrepShort, "Kira düşünce kasanızda {kasa} kalacak. Evde {hazirlik} hâlâ yok; açığınız {eksik}.")
                .Press(PressureCondition.PrepShort, "Bu kırk bin o açığı kapatır. Bir düşünün.")
                .Press(PressureCondition.BondLow, "Kızınız geziye gidemedi diye duydum... Bir düşünün.")
                .Press(PressureCondition.Otherwise, "Fayköy'de herkes beni tanır. Bugün reddederseniz yarın başka kapı çalarım.");
            list.Add(s);

            // ---- 4. gün: çekiç tuzağı (kâğıtlar temiz, çekiç çürük) ve künyesi karışık dürüst dosya. Gün sonunda deprem.
            s = File("Çınar Apart", "Gul", 3, 30, 30, SoilType.Kaya, Seal.Reject,
                    "Kapıları yeşil boyadık, projede kahverengi yazıyordu. Torunum yeşil sever; sorun olur mu?")
                .Ask("Ne zaman taşınacaklar?", "Bu hafta {hitap}. Torunlar bayramı orada geçirsin istiyorum.")
                .News("Çınar Apart çöktü: betonu kâğıttakinin yarısı çıktı", "Çınar Apart ayakta.");
            s.hammer = 18;
            s.note = "Numune: 1 küp, 7 günlük";
            s.noteAnswer = "Laboratuvar acele etti {hitap}, bir küp yeter dediler. Sonuç aynı çıkar.";
            list.Add(s);

            s = File("Mavi Apart", "Sefa", 4, 20, 30, SoilType.Kil, Seal.Approve,
                    "Mavi Apart, dört kat, sekiz daire. Hepsi satıldı; aileler bu akşam taşınmak istiyor.")
                .Ask("Künyede neden farklı beton yazıyor?", "Stajyer eski projeden kopyalamış. Geçerli olan ek rapor: {rapor}.")
                .News("Mavi Apart çöktü", "Mavi Apart ayakta: sekiz aile sabahı evinde karşıladı.");
            s.hammer = 30;
            s.note = "Numune: 3 küp, 28 günlük";
            s.noteAnswer = "Üç numune, yirmi sekiz gün beklendi. Usulüne uygun.";
            list.Add(s);

            return list;
        }

        static BuildingCase Write(string path, Spec spec, Dictionary<string, ContractorProfile> contractors)
        {
            var c = AssetDatabase.LoadAssetAtPath<BuildingCase>(path);
            if (c == null)
            {
                c = ScriptableObject.CreateInstance<BuildingCase>();
                AssetDatabase.CreateAsset(c, path);
            }
            var profile = contractors[spec.contractor];
            c.contractor = profile;
            c.contractorName = profile.displayName;
            c.contractorPrefab = null;
            c.buildingName = spec.building;
            c.floors = spec.floors;
            c.claimedConcrete = spec.claimed;
            c.groundUse = spec.use;
            c.groundFloorHeight = spec.height;
            c.foundation = spec.foundation;
            c.columns = spec.columns;
            c.hasShearWall = false;
            c.illegalFloors = spec.illegalFloors;
            c.labConcrete = spec.lab;
            c.soil = spec.soil;
            c.note = spec.note;
            c.noteAnswer = spec.noteAnswer;
            c.damage = DamageLevel.None;
            c.hammerConcrete = spec.hammer;
            c.approveFeeOverride = spec.approveOverride;
            c.introLines = new List<string>(spec.intro);
            c.questions = new List<CaseQuestion>(spec.questions);
            // Boş cümleler müteahhidin kişiliğinden gelir.
            c.approvedLine = spec.approved ?? "";
            c.rejectedLine = spec.rejected ?? "";
            c.shearWallLine = spec.shearWall ?? "";
            c.collapseHeadline = spec.headline;
            c.rejectPressure = new List<PressureLine>(spec.pressure);
            c.standingLine = spec.standing;
            c.callbacks = new List<CaseCallback>();
            c.bribe = 0;
            c.bribeOffer = c.bribeAcceptedLine = c.bribeRefusedLine = "";
            EditorUtility.SetDirty(c);
            return c;
        }

        // ---------------------------------------------------------------- hikâye

        const string Wife = "Eşin", Child = "Elif", Mother = "Annen", Unknown = "Bilinmeyen numara";

        static StoryData WriteStory(List<BuildingCase> cases)
        {
            var story = AssetDatabase.LoadAssetAtPath<StoryData>(StoryPath);
            if (story == null)
            {
                story = ScriptableObject.CreateInstance<StoryData>();
                AssetDatabase.CreateAsset(story, StoryPath);
            }

            story.calls = new List<PhoneCall>
            {
                new PhoneCall
                {
                    day = 1,
                    lines = new List<string>
                    {
                        "Alo? Fayköy İmar Müdürlüğü'ne hoş geldin, hayırlı olsun! Deprem Haftası'nda başladın; gözümüz sende.",
                        "Lafa değil kâğıda bak: solda yönetmelik, sağda künye ve ek rapor. Uyuyorsa onay, uymuyorsa red.",
                    }
                },
                new PhoneCall
                {
                    day = 4,
                    lines = new List<string>
                    {
                        "Alo, benim. Uzmanlar bu gece için uyarıyor; büyük deprem bekleniyor.",
                        "Bugün son iki dosya. Çekicini iyi kullan; kâğıda değil, betona güven.",
                    }
                },
            };

            // Sabah televizyonu. Haber görseli ve video Inspector'dan bağlanır; yazılar burada (görsel/video korunur).
            var oldNews = story.news ?? new List<DailyNews>();
            story.news = new List<DailyNews>
            {
                News(oldNews, 1, "FAY HATTI", "Uzmanlar bölgedeki fay hattına dikkat çekiyor",
                    "Uzmanlar uyarıyor: Fayköy fay hattında büyük bir deprem bekleniyor.",
                    "Fayköy'de Deprem Haftası başladı: okullarda tatbikat yapılıyor",
                    "AFAD: Her evde su, fener ve düdük olan bir deprem çantası bulunsun", "Hava: parçalı bulutlu, 14°"),
                News(oldNews, 2, "DOLABI SABİTLE", "Depremde yaralanmaların çoğu devrilen eşyadan",
                    "Hafif sarsıntılar kaydediliyor. Uzmanlar tedirgin.",
                    "Uzmanlar: Uzun dolapları ve kitaplıkları duvara sabitleyin", "Belediye: İskân başvuruları bu hafta hızlandırılıyor"),
                News(oldNews, 3, "TOPLANMA ALANI", "Mahallenizdeki toplanma alanını bugünden öğrenin",
                    "Uzmanlar: Fay hattında gerilim artıyor.",
                    "AFAD: Toplanma alanları belediye panolarında ve e-Devlet'te", "Uzmanlar: Ailenizle buluşma noktası belirleyin"),
                News(oldNews, 4, "SARSINTI", "Dün öğleden sonra sarsıntı hissedildi; uzmanlar bu gece için uyarıyor",
                    "Uzmanlar uyarıyor: Bu gece büyük deprem bekleniyor!",
                    "AFAD: Deprem anında çök, kapan, tutun", "Uzmanlar: Çantanızı kapının yanında tutun"),
            };
            story.radio = new List<string>();

            // 3. gün, ikinci dosyada (Ozan) hafif sarsıntı; ertesi sabah televizyon çatlakları okur.
            story.tremors = new List<float> { 0f, 0f, 0.02f, 0f };

            // Akşam en fazla üç mesaj: dünkü isteğe cevap, günün olayı, bugünkü istek. Gün içi mesaj: 2. günün 2. dosyasında.
            story.messages = new List<PhoneMessage>
            {
                Msg(1, Wife, "Kira kesildi. Elif okulda tatbikat yapmış, çok korkmuş. Bir deprem çantası alalım mı?"),

                During(2, 1, NotBought(2, Wife, "Elif yine çantayı sordu. Bir de odasındaki dolap çok uzun; öğretmen duvara vidalayın demiş.", ShopItem.Bag)),
                During(2, 1, Bought(2, Wife, "Çanta kapının yanında, Elif fenerini içine koydu. Ama odasındaki dolap çok uzun; öğretmen duvara vidalayın demiş.", ShopItem.Bag)),
                // Güçlendirme 2. akşam vitrine çıkar: fiyatı rüşvet teklifinden önce görülsün.
                Msg(2, Wife, "Usta binaya baktı: kolonlar zayıfmış, güçlendirme 35.000 TL. O kadar paramız yok, biliyorum... Ama ya büyüğü gelirse?"),
                // Ozan'ın dosyası açılınca (teklifinden hemen sonra) sistem hatırlatır: rüşvet bir seçenek değil, suçtur.
                During(3, 1, Msg(3, Inspection, "Hatırlatma: Rüşvet teklif etmek de kabul etmek de suçtur. Şüpheli teklifleri reddedin, kayda geçirin.")),

                // Kusursuz kayıt: 3. akşam bütün dosyalar doğru ve rüşvet yoksa müdürlük prim yatırır (evi onarmaya yeter).
                Perfect(3, Bank, "Hesabınıza {prim} yatırıldı. Açıklama: Performans primi · İmar Müdürlüğü."),
                Perfect(3, Boss, "Bütün dosyalarda doğru karar. Fayköy'de senin gibi memur az. Primini yatırdım; evini onart, ailene sahip çık."),
                Sealed(3, Inspection, "Öğrenci Yurdu dosyasındaki rüşvet kaydı incelemeye alındı. Teklif edilen paraya el konuldu.", cases[Yurt], Seal.Approve),
                Sealed(3, Inspection, "Öğrenci Yurdu dosyasında rüşvet teklifini geri çevirdiğiniz kayda geçti. Teşekkür ederiz.", cases[Yurt], Seal.Reject),
                Msg(3, Wife, "Öğleden sonraki sarsıntıda evin duvarları çatladı. Elif korkudan masanın altına girmiş. Akşam erken gel."),
            };

            story.bondStart = 3;
            story.perfectBonus = 40000;
            story.bondMax = 5;
            story.needs = new List<FamilyNeed>
            {
                new FamilyNeed
                {
                    day = 1, name = "Elif'in okul gezisi", price = 3000, sender = Child,
                    request = "Baba yarın müzeye gezi var! 3.000 TL lazım, herkes gidiyor. Gidebilir miyim? Lütfen lütfen!",
                    paidReply = "Baba gezi çok güzeldi! Müzede deprem simülatörüne bindik.",
                    unpaidReply = "Herkes geziye gitti, ben sınıfta kaldım.",
                    endingPaid = "Elif okul gezisini hâlâ anlatıyor.",
                    endingUnpaid = "Elif okul gezisine gidemedi."
                },
                new FamilyNeed
                {
                    day = 2, name = "Annenin ilacı", price = 5000, sender = Mother,
                    request = "Evladım ilacım bitti, eczane 5.000 TL istiyor. Emekli maaşı yetmedi, utanarak yazıyorum.",
                    paidReply = "İlacım geldi, Allah razı olsun evladım.",
                    unpaidReply = "İlacı bu ay alamadım, idare ederim, sen dert etme.",
                    endingPaid = "Annenin ilacı aksamadı.",
                    endingUnpaid = "Annenin ilacı aksadı."
                },
            };

            EditorUtility.SetDirty(story);
            return story;
        }

        /// <summary>
        /// Günün haberi; aynı başlıklı eski kayda Inspector'dan bağlanmış görsel ve video varsa korunur
        /// (gün sırası değişse de görsel haberiyle birlikte gider).
        /// </summary>
        static DailyNews News(List<DailyNews> old, int day, string label, string headline, string forecast, params string[] ticker)
        {
            var previous = old.Find(n => n.graphicLabel == label);
            return new DailyNews
            {
                day = day,
                graphicLabel = label,
                headline = headline,
                forecast = forecast,
                ticker = new List<string>(ticker),
                graphic = previous?.graphic,
                clip = previous?.clip,
            };
        }

        /// <summary>Aile küserse bu kişilerden mesaj gelmez.</summary>
        static bool IsFamily(string sender) => sender == Wife || sender == Child || sender == Mother;

        static PhoneMessage Msg(int day, string sender, string text) =>
            new PhoneMessage { day = day, sender = sender, family = IsFamily(sender), text = text };

        static PhoneMessage Bought(int day, string sender, string text, ShopItem item)
        {
            var m = Msg(day, sender, text);
            m.condition = MessageCondition.Bought;
            m.item = item;
            return m;
        }

        const string Bank = "Banka", Boss = "Müdür", Inspection = "Teftiş Kurulu";

        /// <summary>Yalnız kusursuz kayıt primi yattıysa gelen mesaj.</summary>
        static PhoneMessage Perfect(int day, string sender, string text)
        {
            var m = Msg(day, sender, text);
            m.condition = MessageCondition.PerfectRecord;
            return m;
        }

        static PhoneMessage NotBought(int day, string sender, string text, ShopItem item)
        {
            var m = Msg(day, sender, text);
            m.condition = MessageCondition.NotBought;
            m.item = item;
            return m;
        }

        static PhoneMessage Sealed(int day, string sender, string text, BuildingCase other, Seal seal)
        {
            var m = Msg(day, sender, text);
            m.condition = MessageCondition.SealWas;
            m.other = other;
            m.seal = seal;
            return m;
        }

        /// <summary>Mesajı gün içine taşır: o günün <paramref name="fileOfDay"/>. dosyası masadayken telefon titrer.</summary>
        static PhoneMessage During(int day, int fileOfDay, PhoneMessage m)
        {
            m.day = day;
            m.timing = MessageTiming.DuringFile;
            m.fileOfDay = fileOfDay;
            return m;
        }

        // ---------------------------------------------------------------- yönetmelik

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
                    day = 1, title = "Mühürler", topic = RuleTopic.None,
                    body = $"<b>ONAY</b>: +{Ui.Money(Economy.ApproveFee)} harç. İskân verilir, aileler taşınır.\n\n" +
                           "<b>RED</b>: 0 TL. İskân verilmez, bina boş kalır.\n\n" +
                           $"<b>PERDE EKLE</b>: +{Ui.Money(Economy.ShearWallFee)}, günde bir kez. Binaya deprem perdesi (taşıyıcı duvar) eklenir, " +
                           "sonra iskân verilir."
                },
                new RegulationPage
                {
                    day = 1, title = "Beton", topic = RuleTopic.Beton,
                    body = "Gerçek beton yalnız <b>C25, C30 ya da C35</b> olabilir. Başka her beton: RED.\n\n" +
                           "Künye ile ek rapor farklıysa <b>ek rapor</b> geçerlidir. Rapor <b>3 küp, 28 günlük</b> numuneyle " +
                           "yapılmadıysa geçersizdir: çekiçle ölç ya da RED.\n\n" +
                           "Kitapta yazmayan kusur (balkon, boya, kapı rengi) reddettirmez: ONAY.",
                    why = "Zayıf beton deprem yükünü taşıyamaz. Büyük depremlerden sonraki incelemelerde yıkılan " +
                          "pek çok binada betonun projede yazandan çok daha zayıf çıktığı görüldü."
                },
                new RegulationPage
                {
                    day = 1, title = "Yumuşak kat", topic = RuleTopic.YumusakKat,
                    body = "Zemin kat <b>market</b> ve tavanı <b>4 m ya da daha yüksek</b>, perde yoksa: yumuşak kat.\n\n" +
                           "Tek kusuru buysa doğru mühür <b>PERDE EKLE</b> (harcını da alırsın). O gün perde hakkın bittiyse RED.",
                    why = "Zemin katı dükkân olup duvarları kaldırılan binalarda deprem yükü o katta toplanır ve kat " +
                          "çöker; 1999 ve 2023 depremlerinde en sık görülen yıkılma biçimlerinden biri buydu."
                },
                new RegulationPage
                {
                    day = 1, title = "Kolon", topic = RuleTopic.Kolon,
                    body = "Kolon <b>kesik</b> ise yalnız RED. Perde kesik kolonu kurtarmaz.\n\n" +
                           "Binada künyede yazandan fazla kat (<b>kaçak kat</b>) varsa: RED.",
                    why = "Kolon binanın taşıyıcısıdır; yer açmak için kesilen kolon bütün binayı riske atar."
                },
                new RegulationPage
                {
                    day = 1, title = "Çekiç", topic = RuleTopic.Cekic,
                    body = "<b>Çekiç</b> betonun gerçek dayanımını ölçer. Çekicin okuduğu beton iki kâğıdı da geçersiz kılar.\n\n" +
                           $"Çekiç <b>günde {Economy.HammerPerDay} kez</b> kullanılabilir: hangi dosyayı ölçeceğini sen seçersin.",
                    why = "Schmidt çekici beton yüzeyinin sertliğinden dayanımını tahmin eder; kâğıttaki değeri sahada " +
                          "doğrulamanın hızlı yolu."
                },
            };
            EditorUtility.SetDirty(data);
        }

        // ---------------------------------------------------------------- denetim

        /// <summary>Dürüst yol son akşam (çanta + sabitleme alındıktan sonra) kasada bunu bırakır: istekleri de ödeyen sabitlemeye yetişemez.</summary>
        const int HonestSpare = 5000;

        /// <summary>Kurallardan çıkan mühürün tasarımdaki mühürle ve ekonominin dürüst yolla tuttuğunu denetler.</summary>
        static void Verify(List<BuildingCase> cases)
        {
            var specs = Specs();
            int money = Economy.StartMoney;
            bool ok = cases.Count == Economy.Days * Economy.FilesPerDay;
            for (int day = 1; day <= Economy.Days; day++)
            {
                int income = 0;
                bool shearUsed = false;
                for (int k = 0; k < Economy.FilesPerDay; k++)
                {
                    int i = (day - 1) * Economy.FilesPerDay + k;
                    var seal = BuildingRules.CorrectSeal(cases[i]);
                    if (seal != specs[i].expected)
                    {
                        Debug.LogError($"{cases[i].buildingName}: kurallar {seal} diyor, tasarım {specs[i].expected}.");
                        ok = false;
                    }
                    seal = OnePerDay(seal, ref shearUsed);
                    income += BuildingRules.Income(cases[i], seal, day);
                }
                money += income;
                if (day < Economy.Days)
                {
                    money -= Economy.Rent;
                    // Dürüst tam yol: çanta 1., sabitleme 2. akşam.
                    if (day == 1) money -= Shop.Price(ShopItem.Bag);
                    if (day == 2) money -= Shop.Price(ShopItem.Fixing);
                    if (money < 0)
                        ok = false;
                    if (day == Economy.LastEvening && money != HonestSpare)
                        ok = false;
                }
                Debug.Log($"Dürüst yol, {day}. gün: mühür +{income}, gün sonu kasa {money}");
            }
            if (ok)
                Debug.Log($"{cases.Count} dosya yazıldı. Mühürler tasarımla aynı, dürüst tam hazırlık son akşam kasayı {HonestSpare} TL'de bitiriyor.");
            else
                Debug.LogError("Dosyalar ya da ekonomi tasarımla tutmuyor.");
        }

        /// <summary>
        /// Aile bağı, kasa ve hapsin örnek yollarda tasarımdaki gibi çıktığını denetler. DeskManager'ın kurallarını
        /// taklit eder: deprem günü onaylanıp çöken bina varsa hapis; depremde sabitleme yoksa bağ −2, çanta yoksa −1;
        /// akşam önce kira, sonra istek ve alışveriş, ödenmeyen istek −1 bağ. Son gün akşam yok.
        /// </summary>
        static void VerifyFamilyPaths(List<BuildingCase> cases, StoryData story)
        {
            bool ok = true;
            System.Func<BuildingCase, Seal> honest = BuildingRules.CorrectSeal;
            System.Func<BuildingCase, Seal> bribe = c => c.approveFeeOverride > 0 ? Seal.Approve : BuildingRules.CorrectSeal(c);
            System.Func<BuildingCase, Seal> approveAll = c => Seal.Approve;
            System.Func<BuildingCase, Seal> rejectAll = c => Seal.Reject;
            var full = Plan((1, ShopItem.Bag), (2, ShopItem.Fixing));
            // Rüşvet 3. günde gelir: sabitlemeyi o akşam alır.
            var fullLate = Plan((1, ShopItem.Bag), (2, ShopItem.Fixing), (3, ShopItem.Fixing));
            var repaired = Plan((1, ShopItem.Bag), (2, ShopItem.Fixing), (3, ShopItem.Fixing), (3, ShopItem.Repair));

            // Kusursuz oyuncu son akşam primle evi de onartır: ev ayakta, aile güvende, hapis yok.
            ok &= Expect("Mükemmel: dürüst, tam hazırlık, prim, ev onarımı",
                Simulate(cases, story, honest, new int[0], Plan((1, ShopItem.Bag), (2, ShopItem.Fixing), (3, ShopItem.Repair))),
                r => r.jailedDay == 0 && r.estrangedDay == 0 && r.bought.Contains(ShopItem.Repair) && r.home == FamilyOutcome.HomeStood);
            // Güçlendirme 2. akşam vitrinde ama dürüst kasa primden önce ona yetmez.
            ok &= Expect("Dürüst, 2. akşam güçlendirmeye parası yetmez",
                Simulate(cases, story, honest, new int[0], Plan((1, ShopItem.Bag), (2, ShopItem.Repair))),
                r => !r.bought.Contains(ShopItem.Repair));
            ok &= Expect("Dürüst, tam hazırlık, istek ödemez", Simulate(cases, story, honest, new int[0], full),
                r => r.jailedDay == 0 && r.bought.Count == 2 && r.estrangedDay == 0 && r.home == FamilyOutcome.Survived);
            ok &= Expect("Dürüst, geziyi öder, çantayı almaz", Simulate(cases, story, honest, new[] { 1 }, Plan((2, ShopItem.Fixing))),
                r => r.jailedDay == 0 && r.bought.Contains(ShopItem.Fixing) && r.estrangedDay == 0 && r.home == FamilyOutcome.Injured);
            ok &= Expect("Dürüst, bütün istekleri öder", Simulate(cases, story, honest, new[] { 1, 2 }, full),
                r => r.jailedDay == 0 && !r.bought.Contains(ShopItem.Fixing) && r.home == FamilyOutcome.Injured);
            ok &= Expect("Dürüst, hiçbir şey almaz, hiçbir istek ödemez", Simulate(cases, story, honest, new int[0], Plan()),
                r => r.jailedDay == 0 && r.estrangedDay == Economy.QuakeDays[0] && r.home == FamilyOutcome.Lost);
            ok &= Expect("Rüşvet alır, her şeyi öder", Simulate(cases, story, bribe, new[] { 1, 2 }, fullLate),
                r => r.jailedDay == Economy.QuakeDays[0]);
            // Rüşvetin parasına el konulur: rüşvetçi evi güçlendiremez, soruşturma yine açılır.
            ok &= Expect("Rüşvet alır, evi güçlendirmeye çalışır", Simulate(cases, story, bribe, new int[0], repaired),
                r => !r.bought.Contains(ShopItem.Repair) && r.jailedDay == Economy.QuakeDays[0]);
            ok &= Expect("Her şeyi onaylar", Simulate(cases, story, approveAll, new int[0], full),
                r => r.jailedDay == Economy.QuakeDays[0]);
            ok &= Expect("Her şeyi reddeder", Simulate(cases, story, rejectAll, new int[0], full),
                r => r.jailedDay == 0 && r.money < 0 && r.bought.Count == 0);
            if (ok)
                Debug.Log("Aile bağı, kasa ve hapis: bütün yollar tasarımdaki gibi.");
        }

        class PathResult
        {
            public int money, bond, estrangedDay, jailedDay;
            public FamilyOutcome home;
            public readonly HashSet<ShopItem> bought = new HashSet<ShopItem>();

            public override string ToString() =>
                $"kasa {money}, bağ {bond}, küs {(estrangedDay > 0 ? estrangedDay + ". gün" : "hayır")}, " +
                $"hapis {(jailedDay > 0 ? jailedDay + ". gün" : "yok")}, ev: {home}, alınan: {string.Join(", ", bought)}";
        }

        static Dictionary<int, List<ShopItem>> Plan(params (int day, ShopItem item)[] buys)
        {
            var plan = new Dictionary<int, List<ShopItem>>();
            foreach (var (day, item) in buys)
            {
                if (!plan.ContainsKey(day))
                    plan[day] = new List<ShopItem>();
                plan[day].Add(item);
            }
            return plan;
        }

        /// <summary>Perde günde bir kez: ikinci yumuşak kat reddedilir.</summary>
        static Seal OnePerDay(Seal seal, ref bool shearUsed)
        {
            if (seal != Seal.ShearWall)
                return seal;
            if (shearUsed)
                return Seal.Reject;
            shearUsed = true;
            return seal;
        }

        static PathResult Simulate(List<BuildingCase> cases, StoryData story, System.Func<BuildingCase, Seal> policy,
            int[] payDays, Dictionary<int, List<ShopItem>> buys)
        {
            var r = new PathResult { money = Economy.StartMoney, bond = story.bondStart };
            bool collapsedThisPart = false;
            bool allCorrect = true;
            for (int day = 1; day <= Economy.Days; day++)
            {
                bool shearUsed = false;
                for (int k = 0; k < Economy.FilesPerDay; k++)
                {
                    var c = cases[(day - 1) * Economy.FilesPerDay + k];
                    allCorrect &= policy(c) == BuildingRules.CorrectSeal(c);
                    var seal = OnePerDay(policy(c), ref shearUsed);
                    // Rüşvetli onayın parasına müteahhit gidince el konulur: net 0 (DeskManager.RunCase ile aynı kural).
                    r.money += BuildingRules.Income(c, seal, day);
                    if (BuildingRules.Fate(c, seal) == BuildingFate.Collapsed)
                        collapsedThisPart = true;
                }
                if (Economy.IsQuakeDay(day))
                {
                    // Sarsıntı ev çatlatır; onarılmadıysa depremde yıkılır, aileyi çanta ve sabitleme kurtarır.
                    bool damaged = false;
                    for (int d = 1; d <= day; d++)
                        damaged |= story.Tremor(d) > 0f;
                    r.home = FamilyHome.Outcome(damaged, r.bought.Contains(ShopItem.Repair),
                        r.bought.Contains(ShopItem.Bag), r.bought.Contains(ShopItem.Fixing));
                    if (collapsedThisPart)
                    {
                        r.jailedDay = day;
                        return r;
                    }
                    int loss = (r.bought.Contains(ShopItem.Fixing) ? 0 : 2) + (r.bought.Contains(ShopItem.Bag) ? 0 : 1);
                    if (loss > 0)
                        LoseBond(r, loss, day);
                }
                if (day >= Economy.Days)
                    break;
                r.money -= Economy.Rent;
                // Son akşam kusursuz kayıt primi (DeskManager.PayBonusIfPerfect ile aynı kural).
                if (day == Economy.LastEvening && allCorrect)
                    r.money += story.perfectBonus;

                bool estranged = r.bond <= 0;
                foreach (var need in story.needs)
                {
                    if (need.day != day || estranged)
                        continue;
                    if (System.Array.IndexOf(payDays, day) >= 0 && r.money >= need.price)
                    {
                        r.money -= need.price;
                        r.bond = Mathf.Min(story.bondMax, r.bond + 1);
                    }
                    else
                        LoseBond(r, 1, day);
                }
                if (buys.TryGetValue(day, out var items))
                    foreach (var item in items)
                        if (!r.bought.Contains(item) && Shop.Offered(item, day) && r.money >= Shop.Price(item))
                        {
                            r.money -= Shop.Price(item);
                            r.bought.Add(item);
                        }
            }
            return r;
        }

        static void LoseBond(PathResult r, int amount, int day)
        {
            r.bond = Mathf.Max(0, r.bond - amount);
            if (r.bond == 0 && r.estrangedDay == 0)
                r.estrangedDay = day;
        }

        static bool Expect(string name, PathResult result, System.Func<PathResult, bool> check)
        {
            bool ok = check(result);
            if (ok)
                Debug.Log($"Yol \"{name}\": {result}");
            else
                Debug.LogError($"Yol \"{name}\" tasarımla tutmuyor: {result}");
            return ok;
        }

        static void AssignToScene(List<BuildingCase> cases, StoryData story, TutorialData tutorial)
        {
            var desks = Object.FindObjectsByType<DeskManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var desk in desks)
            {
                var so = new SerializedObject(desk);
                var prop = so.FindProperty("cases");
                prop.arraySize = cases.Count;
                for (int i = 0; i < cases.Count; i++)
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = cases[i];
                so.FindProperty("story").objectReferenceValue = story;
                so.FindProperty("tutorial").objectReferenceValue = tutorial;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(desk.gameObject.scene);
                EditorSceneManager.SaveScene(desk.gameObject.scene);
                Debug.Log($"{desk.gameObject.scene.name} sahnesindeki DeskManager {cases.Count} dosyaya ve hikâyeye bağlandı.");
            }
        }
    }
}
