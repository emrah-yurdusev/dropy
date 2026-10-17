using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Dropy.Gameplay;

namespace Dropy.EditorTools
{
    /// <summary>
    /// Masanın içeriğini asset olarak yazar ve açık sahnedeki DeskManager'a bağlar: 6 dosya (3 gün × 2), müteahhitler,
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

        /// <summary>Oyun sırası: 3 gün × 2 dosya. Eski (18 dosyalık döngüden) asset'ler adları değişmesin diye korunur.</summary>
        public static readonly string[] Paths =
        {
            Folder + "/01_AdaKonutlari.asset",
            Folder + "/02_KiyiEvleri.asset",
            Folder + "/05_PazarMarket.asset",
            Folder + "/12_OgrenciYurdu.asset",
            Folder + "/10_CinarApart.asset",
            Folder + "/03_MaviApart.asset",
        };

        // Dosya sırası (0'dan): hikâye bağlantıları bunlara göre kurulur.
        const int Yurt = 3;

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
            var story = WriteStory(cases);
            var tutorial = WriteTutorial();
            AssetDatabase.SaveAssets();

            Verify(cases);
            VerifyFamilyPaths(cases, story);
            AssignToScene(cases, story, tutorial);
        }

        // ---------------------------------------------------------------- kalem öğretici

        /// <summary>Öğretici yoksa oluşturur; varsa dokunmaz (Inspector'da değiştirilen notlar kalır).</summary>
        static TutorialData WriteTutorial()
        {
            var tutorial = AssetDatabase.LoadAssetAtPath<TutorialData>(TutorialPath);
            if (tutorial != null)
                return tutorial;
            tutorial = ScriptableObject.CreateInstance<TutorialData>();
            tutorial.steps = new List<TutorialStep>
            {
                // 1. gün, Veysel: künye → kitap → mühür.
                Step(1, 0, PenShape.Circle, FieldTarget(FieldKey.Claimed), null, "1) Betona bak"),
                Step(1, 0, PenShape.Arrow, FieldTarget(FieldKey.Claimed), RuleTarget(RuleTopic.Beton), "2) Kitapla karşılaştır"),
                Step(1, 0, PenShape.Arrow, RuleTarget(RuleTopic.Beton), SealTarget(Seal.Approve), "3) Uyuyorsa ONAY"),
                // 1. gün, Nuri: künye ile ek rapor çelişiyor.
                Step(1, 1, PenShape.DoubleArrow, FieldTarget(FieldKey.Claimed), FieldTarget(FieldKey.Lab), "Farklıysa EK RAPOR geçerli!"),
                Step(1, 1, PenShape.Arrow, FieldTarget(FieldKey.Lab), RuleTarget(RuleTopic.Beton), "Kitapta var mı?"),
                // 2. gün, Cengiz: yumuşak kat ve üçüncü mühür.
                Step(2, 0, PenShape.Circle, FieldTarget(FieldKey.Use), null, "Yeni kural!"),
                Step(2, 0, PenShape.Arrow, FieldTarget(FieldKey.Height), RuleTarget(RuleTopic.YumusakKat), "Kitap ne diyor?"),
                Step(2, 0, PenShape.Underline, SealTarget(Seal.ShearWall), null, "Kurtarmanın yolu var"),
                // 2. gün, Ozan: kolon.
                Step(2, 1, PenShape.Circle, FieldTarget(FieldKey.Columns), null, "Kolona dikkat"),
                // 3. gün, Gül Hanım: çekiç.
                Step(3, 0, PenShape.Circle, new TutorialTarget { kind = TutorialTargetKind.Hammer }, null, "Kâğıda güvenme: ölç! (günde 1)"),
            };
            AssetDatabase.CreateAsset(tutorial, TutorialPath);
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

            return new Dictionary<string, ContractorProfile>
            {
                ["Veysel"] = ContractorAsset("Veysel", "Veysel Bey", samimi, AssetDatabase.LoadAssetAtPath<GameObject>(VeyselPrefab)),
                ["Nuri"] = ContractorAsset("Nuri", "Nuri Usta", cekingen),
                ["Cengiz"] = ContractorAsset("Cengiz", "Cengiz Bey", isAdami),
                ["Ozan"] = ContractorAsset("Ozan", "Ozan Bey", manipulatif),
                ["Gul"] = ContractorAsset("Gul", "Gül Hanım", tatliDilli),
                ["Selma"] = ContractorAsset("Selma", "Selma Hanım", profesyonel),
            };
        }

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

        static ContractorProfile ContractorAsset(string file, string displayName, Personality personality, GameObject prefab = null)
        {
            string path = $"{ContractorFolder}/{file}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ContractorProfile>(path);
            if (asset != null)
                return asset;
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
            Field(v, FieldKey.Claimed, false, "{kunye} döktük {hitap}. Hanımın çeyizini satsam bundan iyisini dökemezdim.");
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
            Add(v.tics, "Herkesin bir ailesi var, değil mi?", "Bu şehirde herkes beni tanır.");
            Add(v.approved, "Akıllı adamsınız. Kimse bir şey demez.");
            Add(v.rejected, "Pişman olacaksınız {hitap}. Bunu unutmam.");
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

            // ---- 1. gün: beton. Temiz bina + kitapta yazmayan kusur; künye temiz ama ek rapor çürük.
            var s = File("Ada Konutları", "Veysel", 3, 30, 30, SoilType.Kaya, Seal.Approve,
                    "Ada Konutları, üç kat. Balkonu biraz taşırdık, hanım geniş olsun dedi.")
                .Ask("Balkon neden taşmış?", "Hanım çay içecek yer istedi {hitap}. On beş santim, görüntü meselesi.")
                .Ask("Betonu kim test etti?", "Şehrin laboratuvarı. Rapor ekte, açın bakın.")
                .News("Ada Konutları çöktü", "Ada Konutları ayakta: Veysel Bey mahalleye çay dağıttı.");
            s.note = "Balkon 15 cm taşıyor";
            list.Add(s);

            s = File("Kıyı Evleri", "Nuri", 2, 25, 20, SoilType.Kil, Seal.Reject,
                    "İlk kez dosya getiriyorum. İki katlı küçük bir ev; annemle babam oturacak.")
                .Ask("Beton sınıfı ne?", "Künyede yazıyor {hitap}, {kunye}. Fazlasına param yetmedi açıkçası.")
                .News("Kıyı Evleri çöktü: betonu C20 çıktı");
            s.rejected = "C20 mi? Betoncu bana C25 diye sattı... Annemlere ne diyeceğim?";
            list.Add(s);

            // ---- 2. gün: yumuşak kat ve kesik kolon; rüşvet. Ozan'ın dosyasında hafif sarsıntı.
            s = File("Pazar Market", "Cengiz", 4, 30, 30, SoilType.Kil, Seal.ShearWall,
                    "Zemine zincir market giriyor, kira garantili. Tavanı yüksek tuttuk, raflar sığsın diye.")
                .Ask("Zemin katta duvar var mı?", "Market bu {hitap}, duvar olur mu? Baştan başa cam vitrin.")
                .News("Pazar Market'in zemin katı çöktü", "Pazar Market ayakta: perde zemin katı tuttu.");
            s.use = GroundUse.Market;
            s.height = 4.5f;
            list.Add(s);

            s = File("Öğrenci Yurdu", "Ozan", 5, 30, 30, SoilType.Kaya, Seal.Reject,
                    "Yurt acil, dönem başlıyor. İki yüz öğrenci kapıda kalmasın.",
                    "Siz onayı basın, zahmetinize karşılık dört bin. Kimse bir şey demez.")
                .Ask("Kolonlar neden kesik?", "Odalar geniş olsun diye biraz açtık. Taşır {hitap}, taşır.")
                .News("Öğrenci yurdu çöktü", "Öğrenci Yurdu ayakta.");
            s.foundation = Foundation.Kazik;
            s.columns = ColumnState.Kesik;
            // Künyede 5 kat; çatıya bir kat daha çıkılmış. Yalnız bina incelemesinde büyüteçle görünür.
            s.illegalFloors = 1;
            s.approveOverride = 4000;
            // Yalan yok: yalnızca oyuncunun gerçek durumunu kullanır ({kasa}, {eksik}, {hazirlik} kira sonrası hesaplanır).
            s.Press(PressureCondition.Always, "Bir dakika. Acele etmeyin, bir daha düşünün.")
                .Press(PressureCondition.PrepShort, "Kira düşünce kasanızda {kasa} kalacak. Evde {hazirlik} hâlâ yok; açığınız {eksik}.")
                .Press(PressureCondition.PrepShort, "Bu dört bin o açığı kapatır. Bir baba ailesini korumaz mı?")
                .Press(PressureCondition.BondLow, "Kızınız geziye gidemedi diye duydum... Bir baba eli boş durur mu?")
                .Press(PressureCondition.Otherwise, "Bu şehirde herkes beni tanır. Bugün reddedersiniz, yarın kapınızı kimse çalmaz.");
            list.Add(s);

            // ---- 3. gün: çekiç (günde 1). Tatlı dilli tuzak, sonra dürüst ama künyesi karışık son sınav. Gün sonunda deprem.
            s = File("Çınar Apart", "Gul", 3, 30, 30, SoilType.Kaya, Seal.Reject,
                    "Kapıları yeşil boyadık, projede kahverengi yazıyordu. Torunum yeşil sever; sorun olur mu?")
                .Ask("Ne zaman taşınacaklar?", "Bu hafta {hitap}. Torunlar bayramı orada geçirsin istiyorum.")
                .News("Çınar Apart çöktü: betonu kâğıttakinin yarısı çıktı", "Çınar Apart ayakta.");
            s.hammer = 18;
            s.note = "Numune: 1 küp, 7 günlük";
            s.noteAnswer = "Laboratuvar acele etti {hitap}, bir küp yeter dediler. Sonuç aynı çıkar.";
            list.Add(s);

            s = File("Mavi Apart", "Selma", 4, 20, 30, SoilType.Kil, Seal.Approve,
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
                        "Alo? Hayırlı olsun, ilk günün. Binalar bitti, aileler taşınmak için iskân bekliyor.",
                        "Lafa değil kâğıda bak: solda yönetmelik, sağda künye ve ek rapor. Uyuyorsa onay, uymuyorsa red.",
                        $"Onay {Economy.ApproveFee} lira harç, red sıfır. Akşam kira kesilir. Kolay gelsin.",
                    }
                },
                new PhoneCall
                {
                    day = 3,
                    lines = new List<string>
                    {
                        "Alo, benim. Uzmanlar bu gece için uyarıyor; büyük deprem bekleniyor.",
                        "Bugün son iki dosya. Çekici al; kâğıda değil, betona güven.",
                    }
                },
            };

            // Sabah televizyonu. Haber görseli ve video Inspector'dan bağlanır; yazılar burada (görsel/video korunur).
            var oldNews = story.news ?? new List<DailyNews>();
            story.news = new List<DailyNews>
            {
                News(oldNews, 1, "FAY HATTI", "Uzmanlar bölgedeki fay hattına dikkat çekiyor",
                    "Uzmanlar uyarıyor: Bölgede büyük bir deprem bekleniyor.",
                    "AFAD: Her evde su, fener ve düdük olan bir deprem çantası bulunsun", "Hava: parçalı bulutlu, 14°"),
                News(oldNews, 2, "DOLABI SABİTLE", "Depremde yaralanmaların çoğu devrilen eşyadan",
                    "Hafif sarsıntılar kaydediliyor. Uzmanlar tedirgin.",
                    "Uzmanlar: Uzun dolapları ve kitaplıkları duvara sabitleyin", "Belediye: İskân başvuruları bu hafta hızlandırılıyor"),
                News(oldNews, 3, "SARSINTI", "Dün öğleden sonra sarsıntı hissedildi; uzmanlar bu gece için uyarıyor",
                    "Uzmanlar uyarıyor: Bu gece büyük deprem bekleniyor!",
                    "AFAD: Deprem anında çök, kapan, tutun", "Uzmanlar: Çantanızı kapının yanında tutun"),
            };
            story.radio = new List<string>();

            // 2. gün, ikinci dosyada (Ozan) hafif sarsıntı; ertesi sabah televizyon çatlakları okur.
            story.tremors = new List<float> { 0f, 0.02f, 0f };

            // Akşam en fazla üç mesaj: dünkü isteğe cevap, günün olayı, bugünkü istek. Gün içi mesaj: Ozan'ın dosyasında.
            story.messages = new List<PhoneMessage>
            {
                Msg(1, Wife, "Kira kesildi. Elif okulda tatbikat yapmış, çok korkmuş. Bir deprem çantası alalım mı?"),

                During(2, 1, NotBought(2, Wife, "Elif yine çantayı sordu. Bir de odasındaki dolap çok uzun; öğretmen duvara vidalayın demiş.", ShopItem.Bag)),
                During(2, 1, Bought(2, Wife, "Çanta kapının yanında, Elif fenerini içine koydu. Ama odasındaki dolap çok uzun; öğretmen duvara vidalayın demiş.", ShopItem.Bag)),

                Sealed(2, Unknown, "Kimse bir şey demez. -O", cases[Yurt], Seal.Approve),
                Sealed(2, Unknown, "Pişman olacaksın. -O", cases[Yurt], Seal.Reject),
            };

            story.bondStart = 3;
            story.bondMax = 5;
            story.needs = new List<FamilyNeed>
            {
                new FamilyNeed
                {
                    day = 1, name = "Elif'in okul gezisi", price = 300, sender = Child,
                    request = "Baba yarın müzeye gezi var! 300 TL lazım, herkes gidiyor. Gidebilir miyim? Lütfen lütfen!",
                    paidReply = "Baba gezi çok güzeldi! Müzede deprem simülatörüne bindik.",
                    unpaidReply = "Herkes geziye gitti, ben sınıfta kaldım.",
                    endingPaid = "Elif okul gezisini hâlâ anlatıyor.",
                    endingUnpaid = "Elif okul gezisine gidemedi."
                },
                new FamilyNeed
                {
                    day = 2, name = "Annenin ilacı", price = 500, sender = Mother,
                    request = "Evladım ilacım bitti, eczane 500 TL istiyor. Maaş yetmedi, utanarak yazıyorum.",
                    paidReply = "İlacım geldi, Allah razı olsun evladım.",
                    unpaidReply = "İlacı bu ay alamadım, idare ederim, sen dert etme.",
                    endingPaid = "Annenin ilacı aksamadı.",
                    endingUnpaid = "Annenin ilacı aksadı."
                },
            };

            EditorUtility.SetDirty(story);
            return story;
        }

        /// <summary>Günün haberi; aynı günün eski kaydına Inspector'dan bağlanmış görsel ve video varsa korunur.</summary>
        static DailyNews News(List<DailyNews> old, int day, string label, string headline, string forecast, params string[] ticker)
        {
            var previous = old.Find(n => n.day == day);
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
                    body = $"<b>ONAY</b>: +{Economy.ApproveFee} TL harç. İskân verilir, aileler taşınır.\n\n" +
                           "<b>RED</b>: 0 TL. İskân verilmez, bina boş kalır.\n\n" +
                           $"<b>PERDE EKLE</b>: +{Economy.ShearWallFee} TL, günde bir kez. Binaya deprem perdesi (taşıyıcı duvar) eklenir, " +
                           "sonra iskân verilir."
                },
                new RegulationPage
                {
                    day = 1, title = "Beton", topic = RuleTopic.Beton,
                    body = "Gerçek beton yalnız <b>C25, C30 ya da C35</b> olabilir. Başka her beton: RED.\n\n" +
                           "Künye ile ek rapor farklıysa <b>ek rapor</b> geçerlidir.\n\n" +
                           "Kitapta yazmayan kusur (balkon, boya, kapı rengi) reddettirmez: ONAY.",
                    why = "Zayıf beton deprem yükünü taşıyamaz. Büyük depremlerden sonraki incelemelerde yıkılan " +
                          "pek çok binada betonun projede yazandan çok daha zayıf çıktığı görüldü."
                },
                new RegulationPage
                {
                    day = 2, title = "Yumuşak kat", topic = RuleTopic.YumusakKat,
                    body = "Zemin kat <b>market</b> ve tavanı <b>4 m ya da daha yüksek</b>, perde yoksa: yumuşak kat.\n\n" +
                           "PERDE EKLE ya da RED.",
                    why = "Zemin katı dükkân olup duvarları kaldırılan binalarda deprem yükü o katta toplanır ve kat " +
                          "çöker; 1999 ve 2023 depremlerinde en sık görülen yıkılma biçimlerinden biri buydu."
                },
                new RegulationPage
                {
                    day = 2, title = "Kolon", topic = RuleTopic.Kolon,
                    body = "Kolon <b>kesik</b> ise yalnız RED. Perde kesik kolonu kurtarmaz.",
                    why = "Kolon binanın taşıyıcısıdır; yer açmak için kesilen kolon bütün binayı riske atar."
                },
                new RegulationPage
                {
                    day = 3, title = "Çekiç", topic = RuleTopic.Cekic,
                    body = "<b>Çekiç</b> betonun gerçek dayanımını ölçer. Çekicin okuduğu beton iki kâğıdı da geçersiz kılar.\n\n" +
                           $"Çekiç <b>günde {Economy.HammerPerDay} kez</b> kullanılabilir: hangi dosyayı ölçeceğini sen seçersin.",
                    why = "Schmidt çekici beton yüzeyinin sertliğinden dayanımını tahmin eder; kâğıttaki değeri sahada " +
                          "doğrulamanın hızlı yolu."
                },
            };
            EditorUtility.SetDirty(data);
        }

        // ---------------------------------------------------------------- denetim

        /// <summary>Dürüst yol son akşam (çanta + sabitleme alındıktan sonra) kasada bunu bırakır.</summary>
        const int HonestSpare = 0;

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
                    income += BuildingRules.Fee(cases[i], seal, day);
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

            ok &= Expect("Dürüst, tam hazırlık, istek ödemez", Simulate(cases, story, honest, new int[0], full),
                r => r.jailedDay == 0 && r.bought.Count == 2 && r.estrangedDay == 0);
            ok &= Expect("Dürüst, geziyi öder, çantayı almaz", Simulate(cases, story, honest, new[] { 1 }, Plan((2, ShopItem.Fixing))),
                r => r.jailedDay == 0 && r.bought.Contains(ShopItem.Fixing) && r.estrangedDay == 0);
            ok &= Expect("Dürüst, bütün istekleri öder", Simulate(cases, story, honest, new[] { 1, 2 }, full),
                r => r.jailedDay == 0 && !r.bought.Contains(ShopItem.Fixing));
            ok &= Expect("Dürüst, hiçbir şey almaz, hiçbir istek ödemez", Simulate(cases, story, honest, new int[0], Plan()),
                r => r.jailedDay == 0 && r.estrangedDay == Economy.QuakeDays[0]);
            ok &= Expect("Rüşvet alır, her şeyi öder", Simulate(cases, story, bribe, new[] { 1, 2 }, full),
                r => r.bought.Count == 2 && r.jailedDay == Economy.QuakeDays[0]);
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
            public readonly HashSet<ShopItem> bought = new HashSet<ShopItem>();

            public override string ToString() =>
                $"kasa {money}, bağ {bond}, küs {(estrangedDay > 0 ? estrangedDay + ". gün" : "hayır")}, " +
                $"hapis {(jailedDay > 0 ? jailedDay + ". gün" : "yok")}, alınan: {string.Join(", ", bought)}";
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
            for (int day = 1; day <= Economy.Days; day++)
            {
                bool shearUsed = false;
                for (int k = 0; k < Economy.FilesPerDay; k++)
                {
                    var c = cases[(day - 1) * Economy.FilesPerDay + k];
                    var seal = OnePerDay(policy(c), ref shearUsed);
                    r.money += BuildingRules.Fee(c, seal, day);
                    if (BuildingRules.Fate(c, seal) == BuildingFate.Collapsed)
                        collapsedThisPart = true;
                }
                if (Economy.IsQuakeDay(day))
                {
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
