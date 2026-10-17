using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Dropy.Gameplay;

namespace Dropy.EditorTools
{
    /// <summary>
    /// Tasarımın 18 dosyasını, gün gün açılan yönetmeliği ve masanın hikâyesini
    /// (telefon, radyo, mesajlar, sarsıntılar) asset olarak yazar, açık sahnedeki DeskManager'a bağlar.
    /// Tekrar çalıştırmak aynı asset'leri günceller.
    /// </summary>
    public static class BuildCaseFiles
    {
        const string Folder = "Assets/_Dropy/ScriptableObjects/Desk/Files";
        const string RegulationsPath = "Assets/_Dropy/ScriptableObjects/Desk/Regulations.asset";
        public const string StoryPath = "Assets/_Dropy/ScriptableObjects/Desk/Story.asset";

        /// <summary>
        /// Oyun sırası: 6 gün × 3 dosya. 16–18 numaralı dosyalar 2. bölümün ilk günü (ilk depremden sonra) geldiği
        /// için sırada 9'dan sonra durur; eski dosyaların adı değişmesin diye numaraları korunmuştur.
        /// </summary>
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
            Folder + "/16_KavakApartmani.asset",
            Folder + "/17_YildizSitesi.asset",
            Folder + "/18_FirinUstuKonut.asset",
            Folder + "/10_CinarApart.asset",
            Folder + "/11_IstiklalIlkokulu.asset",
            Folder + "/12_OgrenciYurdu.asset",
            Folder + "/13_LimanKonut.asset",
            Folder + "/14_DenizRezidans.asset",
            Folder + "/15_CarsiUstuKonut.asset",
        };

        // Dosya sırası (0'dan): hikâye bağlantıları bunlara göre kurulur.
        const int Ada = 0, Sahil = 7, Yurt = 14;

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
            public string building, contractor, note, noteAnswer;
            public int floors, claimed, lab, hammer, approveOverride;
            public GroundUse use;
            public float height = 2.8f;
            public Foundation foundation;
            public ColumnState columns;
            public SoilType soil;
            public DamageLevel damage;
            public string[] intro;
            public string approved, rejected, shearWall;
            public string headline, standing;
            public int bribe;
            public string bribeOffer, bribeAccepted, bribeRefused;
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

            /// <summary>Gri rüşvet: bina sağlam, müteahhit yine de harç dışında para teklif eder.</summary>
            public Spec Bribe(int amount, string offer, string onAccept, string onRefuse)
            {
                bribe = amount;
                bribeOffer = offer;
                bribeAccepted = onAccept;
                bribeRefused = onRefuse;
                return this;
            }
        }

        static Spec File(string building, string contractor, int floors, int claimed, int lab, SoilType soil, Seal expected, params string[] intro) =>
            new Spec { building = building, contractor = contractor, floors = floors, claimed = claimed, lab = lab, soil = soil, expected = expected, intro = intro };

        /// <summary>
        /// Diyalog ilkesi: müteahhit cevabı söylemez, kendi derdini anlatır. İpucu kâğıttadır;
        /// lafı oyuncuyu ikna etmeye ya da yanıltmaya çalışır. Bazı dürüst müteahhitler bilerek şüpheli konuşur,
        /// bazı tatlı dilliler de çürük dosya getirir. Mühür iskândır: bina bitmiş, aileler taşınmayı bekliyor.
        /// </summary>
        static List<Spec> Specs()
        {
            var list = new List<Spec>();

            // ---- 1. gün: beton ----
            var s = File("Ada Konutları", "Veysel Bey", 3, 30, 30, SoilType.Kaya, Seal.Approve,
                "Günaydın! Yeni gelen siz misiniz? Hayırlı olsun.",
                "Ada Konutları, üç kat. Otuz yıldır bu işi yaparım, tek bir şikâyet gelmedi.")
                .Ask("Balkon neden taşmış?", "Hanım biraz geniş olsun dedi. On beş santim, görüntü meselesi.")
                .Ask("Betonu kim test etti?", "Şehrin laboratuvarı. Rapor ekte, açın bakın.")
                .React("Eksik olmayın. Sizinle iyi çalışırız.", "Balkon için mi? Otuz yılda ilk kez başıma geliyor.");
            s.note = "Balkon 15 cm taşıyor";
            list.Add(s);

            // Künye temiz, ek rapor çürük: Mavi Apart'ın tersi. Nuri Usta kötü niyetli değil, betoncu onu kandırmış.
            list.Add(File("Kıyı Evleri", "Nuri Usta", 2, 25, 20, SoilType.Kil, Seal.Reject,
                    "Ş-şey... İlk kez dosya getiriyorum, kusura bakmayın.",
                    "İki katlı, küçük bir ev. Annemle babam oturacak.")
                .Ask("Neden bu kadar gerginsiniz?", "Geçen ay başka bir büroda bir saat bekletip geri çevirdiler. Kâğıtlarım tam ama.")
                .Ask("Beton sınıfı ne?", "Künyede yazıyor, C25. Fazlasına param yetmedi açıkçası.")
                .React("Gerçekten mi? Allah razı olsun!", "C20 mi? Betoncu bana C25 diye sattı... Annemlere ne diyeceğim?")
                .News("Kıyı Evleri çöktü: betonu C20 çıktı"));

            s = File("Mavi Apart", "Selma Hanım", 4, 20, 30, SoilType.Kil, Seal.Approve,
                    "Kâğıtlarda bir tutarsızlık göreceksiniz, baştan söyleyeyim.",
                    "Künyeyi ofisteki çocuk doldurdu. Ben laboratuvara güvenirim.")
                .Ask("Künyede neden farklı beton yazıyor?", "Eski bir projeden kopyalamış. Döktüğümüz beton raporda ne yazıyorsa o.")
                .Ask("Kaç daire var?", "Sekiz. Hepsi satıldı bile, aileler taşınmayı bekliyor.")
                .React("Doğru okudunuz. Teşekkürler.", "Künyedeki bir yazım hatası yüzünden mi? Ek raporu okumadınız mı?");
            s.note = "Numune: 3 küp, 28 günlük";
            s.noteAnswer = "Laboratuvar üç numune almış, yirmi sekiz gün bekletmiş. Usulüne uygun.";
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
                    "Kiracıların kamyonu kapıda bekliyor, saatine para veriyorum.",
                    "C35 döktük, en iyisinden. Hızlıca bakarsanız sevinirim.")
                .Ask("Neden bu kadar acele?", "Sözleşmeler imzalandı, aileler bu hafta taşınacak.")
                .Ask("C35 pahalı değil mi?", "Pahalı. Ama oğlum da bu binada oturacak.")
                .React("Kamyon boşalsın, sağ olun!", "Kamyon parasını siz mi ödeyeceksiniz?"));

            // ---- 3. gün: kum zemin, çekiç; gün sonunda ilk deprem ----
            s = File("Tepe Evleri", "Hüseyin Usta", 3, 30, 30, SoilType.Kaya, Seal.Approve,
                    "Duydum, yeni bir çekiç gelmiş. Vurun bakalım, korkmuyoruz.",
                    "Dün öğleden sonra sallandı, değil mi? Bizim evde bardaklar şıngırdadı.")
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
            s.noteAnswer = "Deniz kumu tabii, sahil burası. Kumun üstüne radye, herkes öyle yapıyor.";
            list.Add(s);

            s = File("Palmiye Konut", "Fikret Bey", 4, 25, 25, SoilType.Kil, Seal.Approve,
                    "Korkuluk biraz alçak kaldı, onu baştan söyleyeyim.",
                    "Bu arada Ozan Bey sizi sordu. Yakında bir dosyası varmış, yardımcı olursanız unutmazmış.")
                .Ask("Ozan Bey kim?", "Yurt işleri yapar. Eli açıktır, anlarsınız ya.")
                .Ask("Korkuluğu ne zaman düzelteceksiniz?", "Aileler taşınmadan hallederiz, söz.")
                .React("Ozan Bey'e iyi şeyler söylerim.", "Ozan Bey bunu duyunca şaşıracak.")
                // Korkuluk kitapta red sebebi değil: para, zaten basılacak doğru mühür için verilir. Bina sağlam, rüşvet yine rüşvet.
                .Bribe(1500, "Bir de... korkuluk için tutanak tutmazsanız, bin beş yüz de benden. Teşekkür olarak.",
                    "Akıllı adamsınız. Ozan Bey'e de söylerim, sizi unutmaz.",
                    "Peki peki. Ben teşekkür etmek istemiştim, o kadar.");
            s.hammer = 25;
            s.note = "Korkuluk alçak";
            list.Add(s);

            // ---- 4. gün: ilk depremden sonra, hasar tespiti ----
            // 4. günün ikilemi: Kavak ve Fırın Üstü ikisi de yumuşak kat, perde günde bir kez. Biri perde, öteki red.
            s = File("Kavak Apartmanı", "Murat Usta", 4, 30, 30, SoilType.Kil, Seal.ShearWall,
                    "Depremde sıvalar çatladı, onarım izni için geldim.",
                    "On iki aile çadırda bekliyor. Altta market var, rafları yeniden dizdik bile.")
                .Ask("Market katında duvar var mı?", "Yok, baştan başa raf. Ama kolonlar sağlam, raporda yazıyor.")
                .Ask("Raporu kim hazırladı?", "Belediyenin hasar ekibi, dün geldiler. İmzalı.")
                .React("Çadırdakiler eve döner, sağ olun.", "Ama hasar hafif! On iki aile çadırda kalacak.",
                    "Perde mi? Marketi daraltırız, yeter ki aileler dönsün.")
                .News("Kavak Apartmanı'nın market katı çöktü", "Kavak Apartmanı ayakta: on iki aile evine döndü.");
            s.hammer = 30;
            s.use = GroundUse.Market;
            s.height = 4.2f;
            s.damage = DamageLevel.Light;
            s.note = "Belediye hasar ekibi raporu ekte";
            s.noteAnswer = "Hasar ekibi her daireye girdi, kolonlara baktı. Rapor bu.";
            list.Add(s);

            s = File("Yıldız Sitesi", "Hakan Bey", 5, 30, 30, SoilType.Kaya, Seal.Reject,
                    "Biraz çatlak var, deprem sonrası hepsinde var.",
                    "Sıvayı atar, boyarız. Kiracılar yarın dönsün istiyorum.")
                .Ask("Kolondaki çatlak ne kadar derin?", "Görünen kısmı ince. İçini kim görecek?")
                .Ask("Mühendis gördü mü?", "Gördü, 'olur' dedi. Yazılı değil ama.")
                .React("Kiracılara müjde, sağ olun!", "Bina sağlam diyorum size! Bu iş burada bitmez.")
                .News("Yıldız Sitesi ikinci depremde tamamen çöktü");
            s.hammer = 30;
            s.foundation = Foundation.Kazik;
            s.damage = DamageLevel.Heavy;
            s.note = "Kolonlarda çapraz çatlaklar";
            s.noteAnswer = "Çapraz, düz, ne fark eder? Sıva tutar, boyayınca görünmez.";
            list.Add(s);

            s = File("Fırın Üstü Konut", "Nermin Hanım", 3, 30, 30, SoilType.Kil, Seal.ShearWall,
                    "Altta fırınımız var, depremde vitrin camları kırıldı.",
                    "Hafif hasar dediler. Fırını açalım, mahalle ekmeksiz kaldı.")
                .Ask("Fırının tavanı neden bu kadar yüksek?", "Fırın için yüksek tuttuk, sıcak birikmesin diye.")
                .Ask("Üstte kimler oturuyor?", "Ben, oğlum, gelinim, iki de torun.")
                .React("Fırın yarın sabah açılır, sağ olun!", "Fırını kapatırsak mahalle ne yiyecek?", "Perde mi? Tamam, fırına bir duvar ekleriz.")
                .News("Fırın Üstü Konut'un zemin katı çöktü", "Fırın Üstü Konut ayakta: fırın ertesi sabah ekmek dağıttı.");
            s.hammer = 30;
            s.use = GroundUse.Market;
            s.height = 4.5f;
            s.damage = DamageLevel.Light;
            list.Add(s);

            // ---- 5. gün: rüşvet (ev onarımı için son akşam) ----
            // Tatlı dilli tuzak: dikkat kapı rengine çekilir, asıl kusur betonda. Mavi Apart'ın "3 küp, 28 günlük"
            // notunu hatırlayan oyuncu çekici bu dosyaya harcar.
            s = File("Çınar Apart", "Gül Hanım", 3, 30, 30, SoilType.Kaya, Seal.Reject,
                    "Kapıları yeşil boyadık, projede kahverengi yazıyordu.",
                    "Torunum yeşil sever. Sorun olur mu bilmiyorum.")
                .Ask("Neden projeden saptınız?", "Sadece renk. Başka hiçbir şeye dokunmadık, yemin ederim.")
                .Ask("Ne zaman taşınacaklar?", "Bu hafta. Torunlar bayramı orada geçirsin istiyorum.")
                .React("Torunlara müjdeyi vereyim!", "Beton mu? Betoncu bana C30 diye yemin etmişti... Torunlara ne diyeceğim?")
                .News("Çınar Apart çöktü: betonu kâğıttakinin yarısı çıktı");
            s.hammer = 18;
            s.note = "Numune: 1 küp, 7 günlük";
            s.noteAnswer = "Laboratuvar acele etti, bir küp yeter dediler. Sonuç aynı çıkar.";
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
                .Press(PressureCondition.RetrofitShort, "Kira düşünce kasanızda {kasa} kalacak. Evinizin onarımı 4.500 TL. Eksiğiniz {eksik}.")
                .Press(PressureCondition.RetrofitShort, "Bu dört bin o açığı kapatır. Aileniz o hasarlı evde oturuyor, değil mi?")
                .Press(PressureCondition.BondLow, "Kızınızın doğum günü yaklaşıyormuş... Fikret söyledi. Bir baba eli boş gitmez.")
                .Press(PressureCondition.TookBribe, "Fikret'in parasını almışsınız ama. Şimdi mi namuslu oldunuz?")
                .Press(PressureCondition.Otherwise, "Bu şehirde herkes beni tanır. Bugün reddedersiniz, yarın kapınızı kimse çalmaz.");
            list.Add(s);

            // ---- 6. gün: harç yok; gün sonunda ikinci deprem ----
            s = File("Liman Konut", "Veysel Bey", 4, 25, 25, SoilType.Kil, Seal.Approve,
                    "Dün yine sallandı, değil mi? Büyük depremden beri hanım geceleri uyuyamıyor.",
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
            c.noteAnswer = spec.noteAnswer;
            c.damage = spec.damage;
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
            c.bribe = spec.bribe;
            c.bribeOffer = spec.bribeOffer;
            c.bribeAcceptedLine = spec.bribeAccepted;
            c.bribeRefusedLine = spec.bribeRefused;
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
                        "Binalar bitti, aileler taşınmak için iskân bekliyor. Lafa değil kâğıda bak: solda yönetmelik, sağda künye ve ek rapor.",
                        "Künyeye bir mühür basarsın. Onay bin beş yüz lira harç, aileler taşınır. Red sıfır, bina boş kalır. Perde ekle bin lira, günde bir kere.",
                        "Kasan boş başlıyor, kira her akşam kesilir. Kitapta yazmayan kusura takılma. Kolay gelsin.",
                    }
                },
                // İlk depremden sonraki sabah: yeni kural ve ikinci depremin habercisi.
                new PhoneCall
                {
                    day = 4,
                    lines = new List<string>
                    {
                        "Alo, benim. İyi misin, evdekiler iyi mi?",
                        "Şehir hasar tespitiyle uğraşıyor. Bakanlık kitaba yeni madde ekledi: hasarlı binalar.",
                        "Uzmanlar daha büyük bir deprem bekliyor. Onarım izinlerine dikkatli bak.",
                    }
                },
                // 6. gün mühür para vermez; oyuncu bozuk sanmasın diye müdür söyler.
                new PhoneCall
                {
                    day = 6,
                    lines = new List<string>
                    {
                        "Alo, benim. Belediye kasası bugün kapalı, harç alınmıyor.",
                        "Yani bugün mühürler sana para getirmez. Ama aileler yine taşınacak, kararın yine geçerli.",
                        "Radyo da hep deprem diyor. Dikkatli bak.",
                    }
                },
            };

            // Sabah televizyonu. İpuçları alışverişten önce gelir: çanta 1., dolap sabitleme 2. gün
            // (sabitleme 2. akşamdan sonra satılmaz). Inspector'dan bağlanan görsel ve video korunur.
            var oldNews = story.news ?? new List<DailyNews>();
            story.news = new List<DailyNews>
            {
                News(oldNews, 1, "FAY HATTI", "Uzmanlar bölgedeki fay hattına dikkat çekiyor",
                    "AFAD: Her evde su, fener ve düdük olan bir deprem çantası bulunsun", "Hava: parçalı bulutlu, 14°"),
                News(oldNews, 2, "DOLABI SABİTLE", "Depremde yaralanmaların çoğu devrilen eşyadan",
                    "Uzmanlar: Uzun dolapları ve kitaplıkları duvara sabitleyin", "Belediye: İskân başvuruları bu hafta hızlandırılıyor"),
                News(oldNews, 3, "SARSINTI", "Dün öğleden sonra hafif bir sarsıntı hissedildi",
                    "Uzmanlar tedirgin: sarsıntılar sürebilir", "AFAD: Deprem anında çök, kapan, tutun"),
                News(oldNews, 4, "HASAR TESPİTİ", "Dünkü depremin ardından hasar tespit çalışmaları sürüyor",
                    "Uzmanlar artçı depremlere karşı uyardı", "Kızılay: Çadır kentte yardım noktaları açıldı"),
                News(oldNews, 5, "HASARLI BİNA", "Hasarlı binalara girilmemesi çağrısı yapıldı",
                    "Uzmanlar daha büyük bir deprem olasılığından söz ediyor", "Belediye: Onarım izni başvuruları sürüyor"),
                News(oldNews, 6, "ARTÇI", "Son iki günde onlarca sarsıntı kaydedildi",
                    "Vatandaşlar tedirgin", "AFAD: Deprem çantanızı kapının yanında tutun"),
            };
            story.radio = new List<string>();

            // Her depremden önceki gün küçük bir sarsıntı (2. ve 5. gün); ertesi sabah radyo çatlakları okur.
            story.tremors = new List<float> { 0f, 0.012f, 0f, 0f, 0.025f, 0f };

            // Her akşam aileden bir kişi bir şey ister (mesajın içinde Öde düğmesi), diğer mesajlar onu kalabalıklaştırmaz.
            story.messages = new List<PhoneMessage>
            {
                Msg(1, Wife, "Kira bu akşam kesiliyor, unutma. Bir de deprem çantası işini konuşalım; Elif okulda tatbikat yapmış, çok korkmuş."),

                Bought(2, Wife, "Çanta geldi, kapının yanına koydum. Bir de Elif'in odasındaki dolap çok uzun, öğretmen duvara vidalayın demiş.", ShopItem.Bag),
                NotBought(2, Wife, "Çanta işini konuşmadık, Elif yine sordu. Bir de odasındaki dolap çok uzun, öğretmen duvara vidalayın demiş.", ShopItem.Bag),

                // 3. akşam: ilk depremin hemen ardından.
                Bought(3, Wife, "Ne sarsıntıydı! Dolaplar sabit olduğu için kimseye bir şey olmadı. Ama duvarlarda çatlaklar var.", ShopItem.Fixing),
                NotBought(3, Wife, "Elif'in odasındaki dolap üstüne devrildi, kolu incindi. Hastaneden yeni döndük. Duvarlarda da çatlaklar var.", ShopItem.Fixing),
                Bought(3, Child, "Baba çantadaki fener çok işe yaradı, elektrikler gitti.", ShopItem.Bag),
                NotBought(3, Child, "Baba karanlıkta çok korktum, evde fener bile yoktu.", ShopItem.Bag),
                Msg(3, Landlord, "Bina depremde hasar aldı. Dairenizin onarımı 4.500 TL, telefondaki dükkândan ödenebiliyor. İkinci bir deprem olmadan yaptırın; dükkân 5. akşam son kez açık."),

                Msg(4, Wife, "Usta onarıma 4.500 dedi. Yarın akşama kadar yaptırmamız lazım."),
                Msg(4, Unknown, "Yakında bir dosya gelecek. Yardımcı olursanız unutmam. -O"),

                Msg(5, Wife, "Onarım parası yetiyor mu? Dükkân bu akşam son gün."),
                Sealed(5, Unknown, "Teşekkürler. Kimse bir şey demez. -O", cases[Yurt], Seal.Approve),
                Sealed(5, Unknown, "Pişman olacaksın. -O", cases[Yurt], Seal.Reject),
            };

            // İsteğe bağlı aile masrafları. Dürüst yol çanta + sabitleme + ev onarımından sonra 500 TL bırakır:
            // tek küçük istek ödenir, fazlası ya bir güvenlik önleminden ya da rüşvetten çıkar. İkilem iki bölüme yayılır.
            // Bağ 4'ten başlar: hiçbir isteği ödemeyen 5. akşam küser, birini ödeyen küsmez.
            story.bondStart = 4;
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
                    day = 4, name = "Kombi tamiri", price = 400, sender = Wife,
                    request = "Depremde kombi bozuldu, tamiri 400 TL. Bu soğukta Elif hasta olur.",
                    paidReply = "Kombi yapıldı, ev ısındı.",
                    unpaidReply = "Kombi hâlâ bozuk. Elif'le battaniyeye sarındık.",
                    endingPaid = "Ev hafta boyunca sıcak kaldı.",
                    endingUnpaid = "Ev iki gece soğuk kaldı."
                },
                new FamilyNeed
                {
                    // İkinci deprem 6. gün oluyor: Elif'in doğum günü.
                    day = 5, name = "Elif'in doğum günü pastası", price = 300, sender = Child,
                    request = "Baba yarın doğum günüm! Pasta olacak mı? Arkadaşlarımı çağırdım.",
                    endingPaid = "Elif doğum gününde mumlarını üfledi.",
                    endingUnpaid = "Elif'in doğum günü pastasız geçti."
                },
            };

            EditorUtility.SetDirty(story);
            return story;
        }

        const string Wife = "Eşin", Child = "Elif", Mother = "Annen", Landlord = "Ev sahibi", Unknown = "Bilinmeyen numara";

        /// <summary>Günün haberi; aynı günün eski kaydına inspector'dan bağlanmış görsel ve video varsa korunur.</summary>
        static DailyNews News(List<DailyNews> old, int day, string label, string headline, params string[] ticker)
        {
            var previous = old.Find(n => n.day == day);
            return new DailyNews
            {
                day = day,
                graphicLabel = label,
                headline = headline,
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
                    body = $"<b>ONAY</b>: +{Economy.ApproveFee} TL harç. İskân verilir, aileler taşınır.\n\n" +
                           "<b>RED</b>: 0 TL. İskân verilmez, bina boş kalır.\n\n" +
                           $"<b>PERDE EKLE</b>: +{Economy.ShearWallFee} TL, günde bir kez. Binaya deprem perdesi (taşıyıcı duvar) eklenir, " +
                           "sonra iskân verilir."
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
                new RegulationPage
                {
                    day = 4, title = "Hasar",
                    body = "Depremden sonra gelen dosyalarda ek raporda <b>hasar tespiti</b> yazar.\n\n" +
                           "<b>Hafif</b> hasar (sıva çatlağı): onarılabilir; diğer kurallar aynen geçerli.\n\n" +
                           "<b>Ağır</b> hasar (kolonda çatlak): onarım izni verilmez: RED.",
                    why = "Kolondaki çapraz çatlak, taşıyıcının yükü taşıyamadığını gösterir. Böyle bir bina ikinci " +
                          "depremde çökebilir; sıvayla kapatmak çatlağı yok etmez, yalnızca gizler."
                },
            };
            EditorUtility.SetDirty(data);
        }

        /// <summary>Dürüst yol 5. akşam ev onarımından sonra kasada bunu bırakır: tek küçük aile isteği.</summary>
        const int HonestSpare = 500;

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
                if (day <= Economy.LastPaidDay)
                    money -= Economy.Rent;
                // Dürüst tam yol: çanta 1., sabitleme 2. akşam (1. depremden önce), ev onarımı 5. akşam (2. depremden önce).
                if (day == 1) money -= Shop.Price(ShopItem.Bag);
                if (day == 2) money -= Shop.Price(ShopItem.Fixing);
                if (day == Economy.LastPaidDay) money -= Shop.Price(ShopItem.Retrofit);
                Debug.Log($"Dürüst yol, {day}. gün: mühür +{income}, akşam kasa {money}");
                if (money < 0)
                    ok = false;
            }
            if (money != HonestSpare)
                ok = false;
            if (ok)
                Debug.Log($"{cases.Count} dosya yazıldı. Mühürler tasarımla aynı, dürüst tam yol kasayı {HonestSpare} TL'de bitiriyor.");
            else
                Debug.LogError($"Dosyalar tasarımla tutmuyor. Dürüst yol sonu kasa: {money}");
        }

        /// <summary>
        /// Aile bağı, kasa ve hapsin örnek yollarda tasarımdaki gibi çıktığını denetler. DeskManager'ın kurallarını
        /// taklit eder: deprem günü o bölümde onaylanıp çöken bina varsa hapis; 1. depremde sabitleme yoksa bağ −2;
        /// akşam önce kira, sonra istek ve alışveriş, ödenmeyen istek −1 bağ. 6. gün akşam yok.
        /// </summary>
        static void VerifyFamilyPaths(List<BuildingCase> cases, StoryData story)
        {
            bool ok = true;
            System.Func<BuildingCase, Seal> honest = BuildingRules.CorrectSeal;
            System.Func<BuildingCase, Seal> bribe = c => c.approveFeeOverride > 0 ? Seal.Approve : BuildingRules.CorrectSeal(c);
            System.Func<BuildingCase, Seal> approveAll = c => Seal.Approve;
            System.Func<BuildingCase, Seal> rejectAll = c => Seal.Reject;
            var full = Plan((1, ShopItem.Bag), (2, ShopItem.Fixing), (5, ShopItem.Retrofit));

            ok &= Expect("Mükemmel: dürüst, her şeyi alır, geziyi öder", Simulate(cases, story, honest, new[] { 1 }, full),
                r => r.jailedDay == 0 && r.bought.Count == 3 && r.estrangedDay == 0 && r.money >= 0);
            ok &= Expect("Dürüst, hiçbir isteği ödemez", Simulate(cases, story, honest, new int[0], full),
                r => r.jailedDay == 0 && r.money == HonestSpare && r.bought.Count == 3 && r.estrangedDay == Economy.LastPaidDay);
            ok &= Expect("Dürüst, çantayı almaz, geziyi öder", Simulate(cases, story, honest, new[] { 1 },
                    Plan((2, ShopItem.Fixing), (5, ShopItem.Retrofit))),
                r => r.jailedDay == 0 && r.bought.Contains(ShopItem.Retrofit) && r.estrangedDay == 0);
            ok &= Expect("Dürüst, bütün istekleri öder", Simulate(cases, story, honest, new[] { 1, 2, 4, 5 }, full),
                r => r.jailedDay == 0 && r.bond == story.bondMax && !r.bought.Contains(ShopItem.Retrofit));
            ok &= Expect("Dürüst, sabitlemeyi almaz", Simulate(cases, story, honest, new int[0],
                    Plan((1, ShopItem.Bag), (5, ShopItem.Retrofit))),
                r => r.jailedDay == 0 && r.estrangedDay == Economy.QuakeDays[0]);
            ok &= Expect("Dürüst, gri rüşveti alır", Simulate(cases, story, honest, new int[0], full, greedy: true),
                r => r.jailedDay == 0 && r.money > HonestSpare);
            ok &= Expect("Rüşvet alır, her şeyi öder", Simulate(cases, story, bribe, new[] { 1, 2, 4, 5 }, full, greedy: true),
                r => r.bought.Count == 3 && r.jailedDay == Economy.QuakeDays[1]);
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

        /// <param name="greedy">Gri rüşvet teklifini kabul eder (onaylanan dosyada harca eklenir).</param>
        static PathResult Simulate(List<BuildingCase> cases, StoryData story, System.Func<BuildingCase, Seal> policy,
            int[] payDays, Dictionary<int, List<ShopItem>> buys, bool greedy = false)
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
                    if (greedy && seal == Seal.Approve)
                        r.money += c.bribe;
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
                    if (day == Economy.QuakeDays[0])
                    {
                        int loss = (r.bought.Contains(ShopItem.Fixing) ? 0 : 2) + (r.bought.Contains(ShopItem.Bag) ? 0 : 1);
                        if (loss > 0)
                            LoseBond(r, loss, day);
                    }
                }
                if (day > Economy.LastPaidDay)
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
                Debug.Log($"{desk.gameObject.scene.name} sahnesindeki DeskManager {cases.Count} dosyaya ve hikâyeye bağlandı.");
            }
        }
    }
}
