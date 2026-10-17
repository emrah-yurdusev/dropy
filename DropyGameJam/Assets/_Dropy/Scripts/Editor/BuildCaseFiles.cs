using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Dropy.Gameplay;

namespace Dropy.EditorTools
{
    /// <summary>
    /// Kilitli tasarımın 15 dosyasını, gün gün açılan yönetmeliği ve masanın hikâyesini
    /// (telefon, radyo, mesajlar, sarsıntılar) asset olarak yazar, açık sahnedeki DeskManager'a bağlar.
    /// Tekrar çalıştırmak aynı asset'leri günceller.
    /// </summary>
    public static class BuildCaseFiles
    {
        const string Folder = "Assets/_Dropy/ScriptableObjects/Desk/Files";
        const string RegulationsPath = "Assets/_Dropy/ScriptableObjects/Desk/Regulations.asset";
        public const string StoryPath = "Assets/_Dropy/ScriptableObjects/Desk/Story.asset";

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

        // Dosya sırası (0'dan): hikâye bağlantıları bunlara göre kurulur.
        const int Ada = 0, Sahil = 7, Yurt = 11;

        [MenuItem("Dropy/Dosyaları Üret")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            var cases = new List<BuildingCase>();
            var specs = Specs();
            for (int i = 0; i < specs.Count; i++)
                cases.Add(Write(Paths[i], specs[i]));
            for (int i = 0; i < specs.Count; i++)
                WriteCallbacks(cases[i], specs[i], cases);

            WriteRegulations();
            var story = WriteStory(cases);
            AssetDatabase.SaveAssets();

            Verify(cases);
            VerifyFamilyPaths(cases, story);
            int family = 0;
            foreach (var need in story.needs)
                family += need.price;
            Debug.Log($"Aile masrafları toplam {family} TL. Dürüst yolda hepsi ödenirse güvenlik alışverişinden {family} TL eksik kalır.");
            AssignToScene(cases, story);
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
            public string headline, standing;
            public Seal expected;
            public readonly List<CaseQuestion> questions = new List<CaseQuestion>();
            public readonly List<(int other, Seal seal, string line)> callbacks = new List<(int, Seal, string)>();
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

            public Spec React(string onApprove, string onReject, string onShearWall = null)
            {
                approved = onApprove;
                rejected = onReject;
                shearWall = onShearWall;
                return this;
            }

            public Spec News(string onCollapse, string onStanding = null)
            {
                headline = onCollapse;
                standing = onStanding;
                return this;
            }

            public Spec Recall(int otherFile, Seal seal, string line)
            {
                callbacks.Add((otherFile, seal, line));
                return this;
            }
        }

        static Spec File(string building, string contractor, int floors, int claimed, int lab, SoilType soil, Seal expected, params string[] intro) =>
            new Spec { building = building, contractor = contractor, floors = floors, claimed = claimed, lab = lab, soil = soil, expected = expected, intro = intro };

        /// <summary>
        /// Diyalog ilkesi: müteahhit cevabı söylemez, kendi derdini anlatır. İpucu kâğıttadır;
        /// lafı oyuncuyu ikna etmeye ya da yanıltmaya çalışır. Bazı dürüst müteahhitler bilerek şüpheli konuşur.
        /// </summary>
        static List<Spec> Specs()
        {
            var list = new List<Spec>();

            // ---- 1. gün: beton ----
            var s = File("Ada Konutları", "Kemal Bey", 3, 30, 30, SoilType.Kaya, Seal.Approve,
                "Günaydın! Yeni gelen siz misiniz? Hayırlı olsun.",
                "Ada Konutları, üç kat. Otuz yıldır bu işi yaparım, tek bir şikâyet gelmedi.")
                .Ask("Balkon neden taşmış?", "Hanım biraz geniş olsun dedi. On beş santim, görüntü meselesi.")
                .Ask("Betonu kim test etti?", "Şehrin laboratuvarı. Rapor ekte, açın bakın.")
                .React("Eksik olmayın. Sizinle iyi çalışırız.", "Balkon için mi? Otuz yılda ilk kez başıma geliyor.");
            s.note = "Balkon 15 cm taşıyor";
            list.Add(s);

            list.Add(File("Kıyı Evleri", "Nuri Usta", 2, 25, 25, SoilType.Kil, Seal.Approve,
                    "Ş-şey... İlk kez dosya getiriyorum, kusura bakmayın.",
                    "İki katlı, küçük bir ev. Annemle babam oturacak.")
                .Ask("Neden bu kadar gerginsiniz?", "Geçen ay başka bir büroda bir saat bekletip geri çevirdiler. Kâğıtlarım tam ama.")
                .Ask("Beton sınıfı ne?", "Raporda ne yazıyorsa o. Fazlasına param yetmedi açıkçası.")
                .React("Gerçekten mi? Allah razı olsun!", "Ama... her şey yazdığı gibiydi."));

            s = File("Mavi Apart", "Selma Hanım", 4, 20, 30, SoilType.Kil, Seal.Approve,
                    "Kâğıtlarda bir tutarsızlık göreceksiniz, baştan söyleyeyim.",
                    "Künyeyi ofisteki çocuk doldurdu. Ben laboratuvara güvenirim.")
                .Ask("Künyede neden farklı beton yazıyor?", "Eski bir projeden kopyalamış. Döktüğümüz beton raporda ne yazıyorsa o.")
                .Ask("Kaç daire var?", "Sekiz. Hepsi satıldı bile, aileler taşınmayı bekliyor.")
                .React("Doğru okudunuz. Teşekkürler.", "Künyedeki bir yazım hatası yüzünden mi? Ek raporu okumadınız mı?");
            s.note = "Numune: 3 küp, 28 günlük";
            list.Add(s);

            // ---- 2. gün: yumuşak kat, kesik kolon ----
            s = File("Sarı Site", "Rıza Bey", 3, 30, 30, SoilType.Kaya, Seal.Approve,
                    "Belediye başkanı selam söyledi. Bu dosyaya özellikle bakacakmışsınız.",
                    "Cephe boyası biraz soldu, onu da söyleyeyim, sonra laf olmasın.")
                .Ask("Başkanı nereden tanıyorsunuz?", "Askerlik arkadaşıyız. Selamı yeter, değil mi?")
                .Ask("Boyayı neden yenilemediniz?", "Bütçe. Boya kozmetik, bina sapasağlam.")
                .React("Başkana iletirim, memnun kalacak.", "Başkan bunu duyunca hiç hoşlanmayacak.");
            s.note = "Cephe boyası solmuş";
            list.Add(s);

            s = File("Pazar Market", "Cengiz Bey", 4, 30, 30, SoilType.Kil, Seal.ShearWall,
                    "Zemine zincir market giriyor, kira garantili.",
                    "Tavanı yüksek tuttuk, raflar sığsın diye. Kolonlar kalın, dengeler.")
                .Ask("Zemin katta duvar var mı?", "Market bu, duvar olur mu? Açık alan, baştan başa cam vitrin.")
                .Ask("Kolonlar ne kadar kalın?", "Elli elli. Mühendisimiz yeter dedi.")
                .React("Market açılışına beklerim!", "Bir market için bu kadar zorluk mu?", "Perde mi? Vitrin küçülür ama... peki, ekletiriz.")
                .News("Pazar Market'in zemin katı çöktü", "Pazar Market ayakta: perde zemin katı tuttu.");
            s.use = GroundUse.Market;
            s.height = 4.5f;
            list.Add(s);

            list.Add(File("Zeytin Apart", "Ayten Hanım", 3, 35, 35, SoilType.Kaya, Seal.Approve,
                    "Vinç kapıda bekliyor, saatine para veriyorum.",
                    "C35 döktük, en iyisinden. Hızlıca bakarsanız sevinirim.")
                .Ask("Neden bu kadar acele?", "Kış gelmeden kaba inşaatı bitirmemiz lazım.")
                .Ask("C35 pahalı değil mi?", "Pahalı. Ama oğlum da bu binada oturacak.")
                .React("Vinç kurtuldu, sağ olun!", "Vinç parasını siz mi ödeyeceksiniz?"));

            // ---- 3. gün: kum zemin, çekiç ----
            s = File("Tepe Evleri", "Hüseyin Usta", 3, 30, 30, SoilType.Kaya, Seal.Approve,
                    "Duydum, yeni bir çekiç gelmiş. Vurun bakalım, korkmuyoruz.",
                    "Dün gece sallandı mı sizce de? Bizde bardaklar şıngırdadı.")
                .Ask("Çekiçten neden korkmuyorsunuz?", "Betonu kendim karıştırdım, ne koyduğumu bilirim.")
                .Ask("Zemin nasıl?", "Tepeye yaptık, adı üstünde. Raporu okuyun.")
                .React("Çekiç de doğruladı ya, bitti bu iş.", "Çekiç bile sağlam dedi, daha ne istiyorsunuz?");
            s.hammer = 30;
            list.Add(s);

            s = File("Sahil Rezidans", "Tarık Bey", 6, 30, 30, SoilType.Kum, Seal.Reject,
                    "Sahile sıfır, altı kat. Manzarayı görseniz siz de bir daire istersiniz.",
                    "Size özel bir indirim de konuşuruz, aramızda.")
                .Ask("Temeli kim hesapladı?", "Radye döktük, standart. Bu sahilde herkes öyle yapıyor.")
                .Ask("Zemin etüdü ne diyor?", "Sahil sonuçta. Hollanda'da da insanlar kumun üstünde yaşıyor.")
                .React("Size bir daire ayırıyorum, haberiniz olsun.", "Bu işi başka büroda hallederim, merak etmeyin.")
                .News("Sahil Rezidans'ta enkaz: müteahhit firari");
            s.note = "Deniz kumu";
            list.Add(s);

            s = File("Palmiye Konut", "Fikret Bey", 4, 25, 25, SoilType.Kil, Seal.Approve,
                    "Korkuluk biraz alçak kaldı, onu baştan söyleyeyim.",
                    "Bu arada Ozan Bey sizi sordu. Yarın bir dosyası varmış, yardımcı olursanız unutmazmış.")
                .Ask("Ozan Bey kim?", "Yurt işleri yapar. Eli açıktır, anlarsınız ya.")
                .Ask("Korkuluğu ne zaman düzelteceksiniz?", "İskele sökülmeden hallederiz, söz.")
                .React("Ozan Bey'e iyi şeyler söylerim.", "Ozan Bey bunu duyunca şaşıracak.");
            s.hammer = 25;
            s.note = "Korkuluk alçak";
            list.Add(s);

            // ---- 4. gün: rüşvet ----
            s = File("Çınar Apart", "Gül Hanım", 3, 30, 30, SoilType.Kaya, Seal.Approve,
                    "Kapıları yeşil boyadık, projede kahverengi yazıyordu.",
                    "Torunum yeşil sever. Sorun olur mu bilmiyorum.")
                .Ask("Neden projeden saptınız?", "Sadece renk. Başka hiçbir şeye dokunmadık, yemin ederim.")
                .Ask("Bina ne zaman bitecek?", "Bahara. Torunlar bayramı orada geçirsin istiyorum.")
                .React("Torunlara müjdeyi vereyim!", "Kapı rengi yüzünden mi? Gerçekten mi?");
            s.hammer = 30;
            s.note = "Kapı rengi projeden farklı";
            list.Add(s);

            s = File("İstiklal İlkokulu", "Erdal Bey", 4, 30, 30, SoilType.Kil, Seal.ShearWall,
                    "Mahallenin okulu bu. Altına market koyduk, kirası okulun masraflarına gidecek.",
                    "Çocuklar için yapıyoruz, takılmazsınız umarım.")
                .Ask("Zemin kat neden market?", "Belediye bütçe vermedi. Market kirası olmadan okul açılamıyor.")
                .Ask("Markette duvar var mı?", "Geniş olsun dedik, sadece kolon var. Yüksek tavan, ferah.")
                .React("Çocuklar adına teşekkürler.", "Bu mahallenin çocukları nerede okuyacak şimdi?", "Perde mi? Tamam, çocuklar için ne gerekiyorsa.")
                .News("İstiklal İlkokulu'nun zemin katı çöktü", "İstiklal İlkokulu ayakta: çocuklar bahçede toplandı.");
            s.hammer = 30;
            s.use = GroundUse.Market;
            s.height = 4.5f;
            list.Add(s);

            s = File("Öğrenci Yurdu", "Ozan Bey", 5, 30, 30, SoilType.Kaya, Seal.Reject,
                    "Fikret size bahsetmiştir. Ozan.",
                    "Yurt acil, dönem başlıyor. Çocuklar kapıda kalmasın.",
                    "Siz onayı basın, zahmetinize karşılık dört bin. Kimse bir şey demez.")
                .Ask("Kolonlar neden kesik?", "Odalar geniş olsun diye biraz açtık. Öğrenciler kalabalık.")
                .Ask("Dört bin ne demek?", "Teşekkür. Kira, fatura... Sizin de bir aileniz var, değil mi?")
                .React("Akıllı adamsınız. Kimse bir şey demez.", "Pişman olacaksınız. Bu şehirde herkes beni tanır.")
                .News("Öğrenci yurdu çöktü");
            s.hammer = 30;
            s.foundation = Foundation.Kazik;
            s.columns = ColumnState.Kesik;
            s.approveOverride = 4000;
            // Yalan yok: yalnızca oyuncunun gerçek durumunu kullanır ({kasa}, {eksik} kira sonrası hesaplanır).
            s.Press(PressureCondition.Always, "Bir dakika. Acele etmeyin, bir daha düşünün.")
                .Press(PressureCondition.RetrofitShort, "Kira düşünce kasanızda {kasa} kalacak. Apartman güçlendirmesi 4.000 TL. Eksiğiniz {eksik}.")
                .Press(PressureCondition.RetrofitShort, "Bu dört bin o açığı kapatır. Aileniz o binada oturuyor, değil mi?")
                .Press(PressureCondition.BondLow, "Kızınızın doğum günü yaklaşıyormuş... Fikret söyledi. Bir baba eli boş gitmez.")
                .Press(PressureCondition.Otherwise, "Bu şehirde herkes beni tanır. Bugün reddedersiniz, yarın kapınızı kimse çalmaz.");
            list.Add(s);

            // ---- 5. gün: para yok ----
            s = File("Liman Konut", "Kemal Bey", 4, 25, 25, SoilType.Kil, Seal.Approve,
                    "Bu sabah yer sallandı mı sizce de? Hanım korktu, bütün gece uyumadı.",
                    "Dört kat, kil zemin. Hızlıca bakarsanız eve döneceğim.")
                .Recall(Ada, Seal.Approve, "Yine ben! Geçen sefer balkona takılmadınız, sağ olun.")
                .Recall(Ada, Seal.Reject, "Yine ben. Geçen sefer balkon yüzünden geri çevirdiniz, bu sefer her şey milimetrik.")
                .Ask("Bu sefer ne getirdiniz?", "Ne varsa kâğıtta. İsterseniz çekici de vurun.")
                .Ask("Siz depreme hazır mısınız?", "Çantamız kapının yanında. Siz de hazırlıklı olun, hocam.")
                .React("Kolay gelsin, kendinize dikkat edin.", "Ne eksik ki? Neyse, siz bilirsiniz.");
            s.hammer = 25;
            list.Add(s);

            s = File("Deniz Rezidans", "Levent Bey", 5, 30, 30, SoilType.Kaya, Seal.Reject,
                    "İki laboratuvar da aynı sonucu verdi.",
                    "İkinci kez test yaptırmak hakaret olur bize. Vaktiniz kıymetli.")
                .Recall(Sahil, Seal.Reject, "Tarık'ın işini yatırmışsınız, adam perişan. Ben ondan akıllıyım.")
                .Recall(Sahil, Seal.Approve, "Tarık sizi çok övdü. Ben de onun ortağıyım.")
                .Ask("Hangi laboratuvar?", "Kayınbiraderimin. İşini bilir, merak etmeyin.")
                .Ask("Çekiçle ölçsem?", "Ölçün ama o alet nem görünce şaşırır. Biz rapora güveniriz.")
                .React("Gördünüz mü? Boşuna uğraşmadınız.", "Bir aletin dediğine mi inanıyorsunuz şimdi?")
                .News("Deniz Rezidans'ın betonu un ufak oldu");
            s.hammer = 16;
            s.foundation = Foundation.Kazik;
            list.Add(s);

            s = File("Çarşı Üstü Konut", "Sevgi Hanım", 3, 35, 35, SoilType.Kaya, Seal.ShearWall,
                    "Alt kat çarşı, beş metre tavan. Esnaf çok sevdi.",
                    "Bugün son dosya mı? Herkes tedirgin, radyo da hep deprem diyor.")
                .Ask("Çarşı katında perde var mı?", "Dükkânlar geniş olsun dedik. Perde... gerekir mi?")
                .Ask("Üst katlarda kim oturacak?", "Esnafın aileleri. Dükkânın üstü ev, eski usul.")
                .React("Esnafa haber vereyim.", "Esnaf ne yapacak şimdi?", "Peki, perdeyi ekletiriz. İçim rahatladı açıkçası.")
                .News("Çarşı Üstü Konut'ta zemin kat göçtü", "Çarşı Üstü Konut ayakta: esnaf dükkânların önünde toplandı.");
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
            var defaults = ScriptableObject.CreateInstance<BuildingCase>();
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
            c.questions = new List<CaseQuestion>(spec.questions);
            c.approvedLine = spec.approved ?? defaults.approvedLine;
            c.rejectedLine = spec.rejected ?? defaults.rejectedLine;
            c.shearWallLine = spec.shearWall ?? defaults.shearWallLine;
            c.collapseHeadline = spec.headline;
            c.rejectPressure = new List<PressureLine>(spec.pressure);
            c.standingLine = spec.standing;
            Object.DestroyImmediate(defaults);
            EditorUtility.SetDirty(c);
            return c;
        }

        static void WriteCallbacks(BuildingCase c, Spec spec, List<BuildingCase> cases)
        {
            c.callbacks = new List<CaseCallback>();
            foreach (var (other, seal, line) in spec.callbacks)
                c.callbacks.Add(new CaseCallback { other = cases[other], seal = seal, line = line });
            EditorUtility.SetDirty(c);
        }

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
                        "Alo? Hayırlı olsun, ilk günün. Kısa anlatayım, müteahhitler kapıda.",
                        "Gelen oturur, derdini anlatır. Lafa değil kâğıda bak: solda yönetmelik, sağda künye ve ek rapor.",
                        "Künyeye bir mühür basarsın. Onay bin lira, red sıfır. Perde ekle beş yüz, günde bir kere.",
                        "Kitapta yazmayan kusura takılma. Akşam kira kesilir. Kolay gelsin.",
                    }
                },
                // 5. gün mühür para vermez (kilitli tasarım); oyuncu bozuk sanmasın diye müdür söyler.
                new PhoneCall
                {
                    day = 5,
                    lines = new List<string>
                    {
                        "Alo, benim. Belediye kasası bugün kapalı, harç alınmıyor.",
                        "Yani bugün mühürler sana para getirmez. Ama binalar yine yapılacak, kararın yine geçerli.",
                        "Radyo da hep deprem diyor. Dikkatli bak.",
                    }
                },
            };

            story.radio = new List<string>
            {
                "Radyo: Uzmanlar bölgedeki fay hattına dikkat çekiyor.",
                "Radyo: AFAD, deprem çantasında su, fener ve ilk yardım malzemesi bulunmasını hatırlattı.",
                "Radyo: Dün gece 3.8 büyüklüğünde bir sarsıntı hissedildi.",
                "Radyo: Uzmanlar, dolapların duvara sabitlenmesinin can kurtardığını vurguluyor.",
                "Radyo: Son iki günde onlarca küçük sarsıntı kaydedildi. Vatandaşlar tedirgin.",
            };

            // 1–2. gün sakin, sonra gittikçe sertleşen sarsıntılar.
            story.tremors = new List<float> { 0f, 0f, 0.012f, 0.02f, 0.03f };

            // Her akşam aileden bir kişi bir şey ister (mesajın içinde Öde düğmesi), diğer mesajlar onu kalabalıklaştırmaz.
            story.messages = new List<PhoneMessage>
            {
                Msg(1, Wife, "Kira bu akşam kesiliyor, unutma. Bir de deprem çantası işini konuşalım; Elif okulda tatbikat yapmış, çok korkmuş."),

                Bought(2, Wife, "Çanta geldi, kapının yanına koydum. Bir de Elif'in odasındaki dolap çok uzun, öğretmen duvara vidalayın demiş.", ShopItem.Bag),
                NotBought(2, Wife, "Çanta işini konuşmadık, Elif yine sordu. Bir de odasındaki dolap çok uzun, öğretmen duvara vidalayın demiş.", ShopItem.Bag),
                Msg(2, Unknown, "Yakında bir dosya gelecek. Yardımcı olursanız unutmam. -O"),

                Bought(3, Wife, "Usta geldi, dolapları duvara sabitledi. Elif rahat uyudu.", ShopItem.Fixing),
                NotBought(3, Wife, "Gece Elif'in odasındaki dolap sallandı, çok korktuk.", ShopItem.Fixing),
                Msg(3, Landlord, "Bina eski, komşular güçlendirme için para topluyor. Sizin payınız 4.000 TL. Telefondaki dükkândan ödenebiliyor, yarın akşam son gün."),

                Msg(4, Wife, "Güçlendirme parası yetiyor mu? Ev sahibi bu akşam son gün dedi."),
                Sealed(4, Unknown, "Teşekkürler. Kimse bir şey demez. -O", cases[Yurt], Seal.Approve),
                Sealed(4, Unknown, "Pişman olacaksın. -O", cases[Yurt], Seal.Reject),
            };

            // İsteğe bağlı aile masrafları. Dürüst yol üç güvenlik alışverişine tam yeter (kasa 0'da biter),
            // yani her ödenen istek ya bir güvenlik önleminden ya da rüşvetten çıkar. İkilem burada.
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
                    endingUnpaid = "Annenin ilacı bir hafta aksadı."
                },
                new FamilyNeed
                {
                    day = 3, name = "Doğalgaz faturası", price = 400, sender = Wife,
                    request = "Doğalgaz faturası geldi, 400 TL. Yarın son gün, yatmazsa kesecekler.",
                    paidReply = "Fatura yatmış, ev sıcacık.",
                    unpaidReply = "Doğalgaz kesildi. Elif'le battaniyeye sarındık.",
                    endingPaid = "Ev hafta boyunca sıcak kaldı.",
                    endingUnpaid = "Ev iki gece soğuk kaldı."
                },
                new FamilyNeed
                {
                    // Deprem 5. gün oluyor: Elif'in doğum günü.
                    day = 4, name = "Elif'in doğum günü pastası", price = 300, sender = Child,
                    request = "Baba yarın doğum günüm! Pasta olacak mı? Arkadaşlarımı çağırdım.",
                    endingPaid = "Elif doğum gününde mumlarını üfledi.",
                    endingUnpaid = "Elif'in doğum günü pastasız geçti."
                },
            };

            EditorUtility.SetDirty(story);
            return story;
        }

        const string Wife = "Eşin", Child = "Elif", Mother = "Annen", Landlord = "Ev sahibi", Unknown = "Bilinmeyen numara";

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
                           "sonra onaylanır.\n\n" +
                           "Kitapta yazan bir kuralı çiğneyen onay <b>müfettiş tutanağı</b> yer: ilki uyarı, " +
                           "sonrakiler akşam 500 TL ceza."
                },
                new RegulationPage
                {
                    day = 1, title = "Beton",
                    body = "Gerçek beton yalnız <b>C25, C30 ya da C35</b> olabilir. Başka her beton: RED.\n\n" +
                           "Künye ile ek rapor farklıysa <b>ek rapor</b> geçerlidir.\n\n" +
                           "Kitapta yazmayan kusur (balkon, boya, kapı rengi, korkuluk) reddettirmez: ONAY.",
                    why = "Zayıf beton deprem yükünü taşıyamaz. Büyük depremlerden sonraki incelemelerde yıkılan " +
                          "pek çok binada betonun projede yazandan çok daha zayıf çıktığı görüldü."
                },
                new RegulationPage
                {
                    day = 2, title = "Yumuşak kat",
                    body = "Zemin kat <b>market</b> ve tavanı <b>4 m ya da daha yüksek</b>, perde yoksa: yumuşak kat. " +
                           "PERDE EKLE ya da RED.\n\n" +
                           "Kolon <b>kesik</b> ise yalnız RED. Perde kesik kolonu kurtarmaz.",
                    why = "Zemin katı dükkân olup duvarları kaldırılan binalarda deprem yükü o katta toplanır ve kat " +
                          "çöker; 1999 ve 2023 depremlerinde en sık görülen yıkılma biçimlerinden biri buydu. Kolon " +
                          "binanın taşıyıcısıdır; yer açmak için kesilen kolon bütün binayı riske atar."
                },
                new RegulationPage
                {
                    day = 3, title = "Zemin ve çekiç",
                    body = "<b>Kum</b> zemin ve <b>5 ya da daha fazla kat</b>: kazık temel şart. Radye temel: RED.\n\n" +
                           "<b>Çekiç</b> betonun gerçek dayanımını ölçer. Çekicin okuduğu beton iki kâğıdı da geçersiz kılar. " +
                           "Çekiç <b>günde 2 kez</b> kullanılabilir: hangi dosyayı ölçeceğini sen seçersin.",
                    why = "Suya doygun kum depremde sıvılaşıp taşıma gücünü yitirebilir; kazık temel yükü alttaki " +
                          "sağlam katmana indirir. Schmidt çekici beton yüzeyinin sertliğinden dayanımını tahmin eder; " +
                          "kâğıttaki değeri sahada doğrulamanın hızlı yolu."
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

        /// <summary>
        /// Aile bağı ve kasanın dört örnek yolda tasarımdaki gibi çıktığını denetler. DeskManager'ın akşam
        /// kurallarını taklit eder: önce kira, sonra istek ve alışveriş, sabah ödenmeyen istek −1 bağ.
        /// </summary>
        static void VerifyFamilyPaths(List<BuildingCase> cases, StoryData story)
        {
            bool ok = true;
            System.Func<BuildingCase, Seal> honest = BuildingRules.CorrectSeal;
            System.Func<BuildingCase, Seal> bribe = c => c.approveFeeOverride > 0 ? Seal.Approve : BuildingRules.CorrectSeal(c);
            System.Func<BuildingCase, Seal> approveAll = c => Seal.Approve;
            ok &= Expect("Dürüst, hiçbir isteği ödemez", Simulate(cases, story, honest, new int[0],
                    Plan((1, ShopItem.Bag), (2, ShopItem.Fixing), (4, ShopItem.Retrofit))),
                r => r.estrangedDay == 3 && r.bought.Count == 3);
            ok &= Expect("Dürüst, çantayı almaz, geziyi öder", Simulate(cases, story, honest, new[] { 1 },
                    Plan((2, ShopItem.Fixing), (4, ShopItem.Retrofit))),
                r => r.bond == 1 && r.estrangedDay == 0 && r.bought.Contains(ShopItem.Retrofit));
            ok &= Expect("Dürüst, bütün istekleri öder", Simulate(cases, story, honest, new[] { 1, 2, 3, 4 },
                    Plan((1, ShopItem.Bag), (2, ShopItem.Fixing), (3, ShopItem.Retrofit), (4, ShopItem.Retrofit))),
                r => r.bond == story.bondMax && !r.bought.Contains(ShopItem.Retrofit));
            ok &= Expect("Rüşvet alır, her şeyi öder", Simulate(cases, story, bribe, new[] { 1, 2, 3, 4 },
                    Plan((1, ShopItem.Bag), (2, ShopItem.Fixing), (4, ShopItem.Retrofit))),
                r => r.bond == story.bondMax && r.bought.Count == 3 && r.citations == 1 && r.fines == 0);
            // Her şeyi onaylayan: tutanaklar sömürüyü keser (ilki bedava, 5. günün cezası kesilmez).
            ok &= Expect("Her şeyi onaylar, hiçbir şey almaz", Simulate(cases, story, approveAll, new int[0],
                    Plan()),
                r => r.citations == 6 && r.fines == 3 * Economy.CitationFine);
            if (ok)
                Debug.Log("Aile bağı ve tutanak: bütün yollar tasarımdaki gibi.");
        }

        class PathResult
        {
            public int money, bond, estrangedDay, citations, fines;
            public readonly HashSet<ShopItem> bought = new HashSet<ShopItem>();

            public override string ToString() =>
                $"kasa {money}, bağ {bond}, küs {(estrangedDay > 0 ? estrangedDay + ". akşam" : "hayır")}, tutanak {citations} (ceza {fines}), alınan: {string.Join(", ", bought)}";
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

        static PathResult Simulate(List<BuildingCase> cases, StoryData story, System.Func<BuildingCase, Seal> policy,
            int[] payDays, Dictionary<int, List<ShopItem>> buys)
        {
            var r = new PathResult { money = Economy.StartMoney, bond = story.bondStart };
            for (int day = 1; day <= Economy.Days; day++)
            {
                int fine = 0;
                for (int k = 0; k < Economy.FilesPerDay; k++)
                {
                    var c = cases[(day - 1) * Economy.FilesPerDay + k];
                    var seal = policy(c);
                    r.money += BuildingRules.Fee(c, seal, day);
                    if (seal == Seal.Approve && BuildingRules.KnownViolation(c, seal, day) != null)
                    {
                        r.citations++;
                        if (r.citations > Economy.FreeCitations && day <= Economy.LastPaidDay)
                            fine += Economy.CitationFine;
                    }
                }
                if (day > Economy.LastPaidDay)
                    break;
                fine = Mathf.Min(r.money, fine);
                r.money -= fine;
                r.fines += fine;
                r.money -= Mathf.Min(r.money, Economy.Rent);

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
                    {
                        r.bond = Mathf.Max(0, r.bond - 1);
                        if (r.bond == 0 && r.estrangedDay == 0)
                            r.estrangedDay = day;
                    }
                }
                if (buys.TryGetValue(day, out var items))
                    foreach (var item in items)
                        if (!r.bought.Contains(item) && r.money >= Shop.Price(item))
                        {
                            r.money -= Shop.Price(item);
                            r.bought.Add(item);
                        }
            }
            return r;
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

        static void AssignToScene(List<BuildingCase> cases, StoryData story)
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
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(desk.gameObject.scene);
                EditorSceneManager.SaveScene(desk.gameObject.scene);
                Debug.Log($"{desk.gameObject.scene.name} sahnesindeki DeskManager 15 dosyaya ve hikâyeye bağlandı.");
            }
        }
    }
}
