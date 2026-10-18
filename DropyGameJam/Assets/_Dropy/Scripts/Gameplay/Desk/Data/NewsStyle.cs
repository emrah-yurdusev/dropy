using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Video;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Sabah televizyonu ve final gazetesinin görünüşü ve sabit yazıları. Inspector'dan değiştirilir;
    /// dosya üreticisi bu asset'e dokunmaz. Boş bırakılan görsel alanlar yerine koddan çizilen yer tutucular gelir.
    /// Gazete şablonlarındaki {0}, {1} yer tutucuları oyunun sonucuyla dolar.
    /// </summary>
    [CreateAssetMenu(menuName = "Dropy/Masa/Haber Görünümü", fileName = "NewsStyle")]
    public class NewsStyle : ScriptableObject
    {
        [Header("Televizyon · yazılar")]
        public string channelName = "K6 HABER";
        public string tvBrand = "TELESİS";
        public string liveLabel = "CANLI";
        public string breakingLabel = "SON DAKİKA";
        [Tooltip("{0} = gün")]
        public string clockFormat = "GÜN {0} · 08:30";
        [Tooltip("{0} = gün")]
        public string dayTitleFormat = "GÜN {0}";
        public string continueHint = "Devam etmek için tıkla";

        [Header("Televizyon · görünüm")]
        [Tooltip("Televizyon kasası (9-slice). Boşsa koyu yuvarlak kasa çizilir")]
        public Sprite tvFrame;
        [Tooltip("Stüdyo arka planı. Boşsa düz renk")]
        public Sprite studioBackground;
        [Tooltip("Spiker görseli. Boşsa siluet çizilir. Günün videosu varsa video oynar")]
        public Sprite anchor;
        [Tooltip("Günün kendi videosu yoksa spiker yerine bu oynar (döngü)")]
        public VideoClip defaultAnchorClip;
        public Color backdrop = new Color(0.16f, 0.13f, 0.11f, 0.97f);
        public Color studio = new Color(0.07f, 0.23f, 0.39f);
        public Color breakingColor = new Color(0.75f, 0.13f, 0.11f);
        public Color tickerColor = new Color(0.95f, 0.76f, 0.19f);
        [Tooltip("Kayan yazı hızı (piksel/saniye, 1920 genişlikte)")]
        public float tickerSpeed = 110f;
        [Range(0f, 0.6f)] public float scanlines = 0.25f;
        public TMP_FontAsset tvFont;

        [Header("Gazete · yazılar")]
        public string masthead = "FAYKÖY GAZETESİ";
        public string issue = "Sayı 1842";
        public string price = "Fiyatı 10 TL";
        public string specialEdition = "DEPREM ÖZEL BASKISI";
        [Tooltip("{0} = gün")]
        public string editionFormat = "{0}. gün · Akşam baskısı";
        [Tooltip("Okunmayan dalgalı dolgu haberler (sırayla boş sütunlara dağılır)")]
        [TextArea(2, 5)]
        public List<string> fillers = new List<string>
        {
            "Fayköy'de hissedilen deprem eski çarşıda hasara yol açtı. Ekipler sabaha kadar çalıştı; toplanma alanlarında çorba dağıtıldı.",
            "Uzmanlar zemin etüdü yapılmadan verilen izinlerin risk taşıdığını hatırlattı. Belediye, Deprem Haftası boyunca bütün binaları yeniden denetleyecek.",
            "Liman yolu trafiğe kontrollü olarak açıldı. Okullar hafta boyunca tatil edildi. Gönüllüler toplanma alanlarında çocuklara oyun kurdu.",
            "Spor: Fayköyspor'un hafta sonu maçı ertelendi. Hava: parçalı bulutlu, gece serin.",
        };
        public string adTitle = "DEPREM ÇANTASI";
        public string adLine = "su · fener · düdük · ilk yardım";

        [Header("Gazete · görünüm")]
        [Tooltip("Kâğıt dokusu (9-slice ya da düz). Boşsa düz kâğıt rengi")]
        public Sprite paperTexture;
        [Tooltip("Oyuncunun gazete fotoğrafı. Boşsa noktalı baskı yer tutucu çizilir")]
        public Sprite playerPhoto;
        [Tooltip("Hapis sonundaki sabıka fotoğrafı. Boşsa noktalı baskı yer tutucu çizilir")]
        public Sprite mugshotPhoto;
        [Tooltip("Akşam ekranındaki aile fotoğrafı (\"Biz, geçen bayram\"). Boşsa düz şekillerle çizilmiş yer tutucu")]
        public Sprite familyPhoto;
        public Color paper = new Color(0.92f, 0.9f, 0.84f);
        public Color ink = new Color(0.1f, 0.09f, 0.07f);
        [Tooltip("Dalgalı dolgu yazının rengi")]
        public Color fillerInk = new Color(0.42f, 0.4f, 0.35f);
        [Tooltip("Sayfanın eğikliği (derece)")]
        public float tilt = -0.6f;
        [Tooltip("Dolgu yazının dalga yüksekliği")]
        public float fillerWave = 3f;
        [Tooltip("Manşetin mürekkep pürüzü (0 = düz)")]
        public float inkRoughness = 0.8f;
        public TMP_FontAsset mastheadFont;
        public TMP_FontAsset headlineFont;
        public TMP_FontAsset bodyFont;

        [Header("Gazete · şablonlar")]
        public NewspaperTexts texts = new NewspaperTexts();

        static NewsStyle fallback;

        /// <summary>Sahnede asset bağlı değilse varsayılan değerlerle bir kopya.</summary>
        public static NewsStyle Default
        {
            get
            {
                if (fallback == null)
                    fallback = CreateInstance<NewsStyle>();
                return fallback;
            }
        }
    }

    /// <summary>Gazetenin oyun sonucuna göre seçilen cümleleri. {0}, {1} yer tutucuları kodda dolar.</summary>
    [System.Serializable]
    public class NewspaperTexts
    {
        [Header("Final manşetleri")]
        public string headlineAllStanding = "İskân verilen binaların hepsi ayakta";
        [Tooltip("{0} = boş çöken bina sayısı")]
        public string headlineEmptyFallen = "{0} bina çöktü, hiçbirinde kimse yoktu";
        public string subheadAllStanding = "Büronun mühür bastığı her bina depremi ayakta karşıladı. Onaylanan her bina, bir ailenin yuvası.";
        public string subheadEmptyFallen = "Çöken binaların hiçbirine iskân verilmemişti; içlerinde kimse yoktu.";
        public string photoCaption = "İskân memuru, büroda: \"Kitaba baktım, o kadar.\"";
        public string photoCaptionBribed = "Teftiş Kurulu'nun incelemesi sonrası görevden uzaklaştırılan iskân memuru.";

        [Header("Soruşturma manşetleri")]
        public string jailHeadline = "İskân memuru gözaltına alındı";
        [Tooltip("{0} = bina adı")]
        public string jailSubheadOne = "Çöken {0} binasının iskânında onun imzası vardı; hakkında soruşturma başlatıldı.";
        [Tooltip("{0} = bina sayısı")]
        public string jailSubheadMany = "Çöken {0} binanın iskânında onun imzası vardı; hakkında soruşturma başlatıldı.";
        [Tooltip("{0} = deprem günü")]
        public string jailEdition = "{0}. gün depreminden sonra";
        public string jailCaption = "Gözaltına alınan iskân memuru ifade vermeye götürüldü.";
        [TextArea(2, 4)]
        public string courtText = "Yönetmelikteki kuralı çiğneyen bina depremde çöktü. Aileler tatbikat sayesinde son anda dışarı çıktı ama yuvalarını kaybetti. Savcılık, müteahhitle birlikte iskân memuru hakkında da soruşturma başlattı.";
        public string courtBribe = "Dosyada rüşvet kaydı da var; Teftiş Kurulu ayrıca inceliyor.";
        public string jailFooterLeft = "OYUN BİTTİ";
        public string jailFooterRight = "NEYİ KAÇIRDIĞINI KARNEDE GÖR";

        [Header("Kutu başlıkları")]
        public string boxHome = "EVDE";
        public string boxFamily = "AİLEDEN";
        public string boxFellEmpty = "BOŞ ÇÖKENLER";
        public string boxStanding = "AYAKTA KALANLAR";
        public string boxEmptySound = "SAĞLAM AMA BOŞ KALANLAR";
        public string boxCollapsed = "ÇÖKENLER";
        public string boxInvestigation = "SORUŞTURMA";
        public string boxReward = "ÖDÜL";
        [Tooltip("{0} = prim tutarı")]
        public string rewardLine = "Kusursuz sicil: İskân memuru rüşvet teklifini reddetti, bütün dosyalarda doğru kararı verdi. Fayköy İmar Müdürlüğü ona {0} prim verdi. Dürüst kalan kazandı.";
        public string boxCourt = "SORUŞTURMA";
        [Tooltip("Final gazetesinde uzmanların kutusu (soruşturmasız sonda)")]
        public string boxExperts = "UZMANLAR DİYOR Kİ";
        [TextArea(2, 4)]
        public List<string> expertLines = new List<string>
        {
            "Mühendisler: Zemin etüdü, doğru beton, sağlam kolon. Deprem güvenliği kâğıtta başlar.",
            "Müteahhitler: Fayköy'ün ustalarının çoğu işini kurala göre yapar; denetim dürüst işi korur.",
        };
        public string emptySoundNote = "İskân verilmedi; aileleri hâlâ kirada.";
        public string boxUnjust = "HAKSIZ RED";
        public string unjustNote = "Kurala uyan ya da perdeyle kurtulabilecek binalar boş kaldı; aileler hâlâ kirada. Vatandaşın hakkı da korunmalı.";
        [Tooltip("{0} = müteahhit adları")]
        public string investigation = "Teftiş Kurulu, {0} dosyasındaki rüşvet kaydını inceledi: paraya el konuldu. Görevden uzaklaştırıldın, soruşturma sürüyor.";
        [Tooltip("{0} = müteahhit, {1} = onay günü sözü")]
        public string contractorQuote = "Müteahhit {0}, iskân günü: \"{1}\"";

        [Header("Evde")]
        public string bagReady = "Çanta hazırdı: fener, su, düdük.";
        public string bagNone = "Karanlıkta ne su vardı ne fener.";
        public string fixingDone = "Dolaplar sabitti; depremde kimse yaralanmadı.";
        public string fixingMissing = "Sabitlenmemiş dolap devrildi; Elif son anda kenara kaçtı, çok korktu.";

        [Header("Ailenin sonu")]
        public string verdictSafe = "AİLEN GÜVENDE";
        public string verdictNoBag = "Herkes iyi; ama gece karanlıkta ve susuz geçti.";
        public string verdictNoFixing = "Devrilen dolap Elif'i çok korkuttu; çantadaki fener ve düdük sayesinde yardım çabuk geldi.";
        public string verdictNone = "Dolap devrildi, ev karanlıktı; Elif çok korktu, yardım ancak sabaha karşı geldi.";
        public string estrangedHouse = "Ailen sana biraz küs.";

        [Header("Sarsıntıda çatlayan ev")]
        public string repairDone = "Sarsıntıda çatlayan ev güçlendirildi; depremde ayakta kaldı.";
        public string repairNone = "Sarsıntıda çatlayan ev güçlendirilmedi; depremde yıkıldı.";
        public string verdictHomeLostSafe = "EVİN YIKILDI, AİLEN GÜVENDE";
        public string verdictHomeLostNoBag = "Ev yıkıldı ama aile dışarı çıktı. Dolaplar sabitti; yalnız fener yoktu, gece karanlıkta geçti.";
        public string verdictHomeLostNoFixing = "Ev yıkıldı ama aile çantayı kapıp çıktı. Devrilen dolap Elif'i çok korkuttu.";
        public string verdictHomeLostNone = "Ev yıkıldı. Neyse ki evde kimse yoktu; ancak hiçbir hazırlık olmadığı için bütün eşyalar yıkıntıda kaldı.";
        public string homeStorySurvived = "Yıkılan evden hemen çıktılar: çanta kapının yanındaydı, aile toplanma alanında buluştu.";
        public string homeStoryInjured = "İskân memurunun evi yıkıldı; aile dışarı çıktı ama gece zor geçti.";
        public string headlineFamilyLost = "İskân memurunun evi yıkıldı; neyse ki evde kimse yoktu";
        public string subheadFamilyLost = "Sarsıntıda çatlayan ev güçlendirilmemişti; evde ne sabitlenmiş dolap vardı ne deprem çantası.";
        public string homeStoryLost = "İskân memurunun ailesi o gece akrabadaydı. Ev yıkıldı; hazırlık olmadığı için hiçbir eşya kurtarılamadı.";

        [Header("Alt şerit")]
        [Tooltip("{0} = doğru, {1} = toplam dosya")]
        public string footerReport = "KARNE: {1} DOSYADAN {0} DOĞRU";
        [Tooltip("{0} = kasa")]
        public string footerCash = "KASA: {0}";
        [Tooltip("{0} = borç")]
        public string footerDebt = "BORÇ: {0}";
    }

    /// <summary>Bir sabahın televizyon yayını: manşet, kayan yazı, ekrandaki görsel ve televizyonun altındaki notlar.</summary>
    public class TvBroadcast
    {
        public int day;
        public string headline;
        public readonly List<string> ticker = new List<string>();
        /// <summary>Televizyonun altında sabit duran önemli satırlar (yönetmeliğe yeni madde, harç yok...).</summary>
        public readonly List<string> notes = new List<string>();
        public Sprite graphic;
        public string graphicLabel;
        public VideoClip clip;
    }

    /// <summary>Gazetenin bir sayfası: başlık bilgileri, manşet, fotoğraf ve sütunlara dağılan okunur kutular.</summary>
    public class NewspaperPage
    {
        public string edition;
        public string headline;
        public string subhead;
        public bool mugshot;
        public string photoCaption;
        /// <summary>İkinci sütunun başındaki küçük haber başlığı.</summary>
        public string story;
        /// <summary>Sütunların altında tam genişlikte siyah şeritteki sonuç (ailenin sonu). Boşsa şerit yok.</summary>
        public string verdict;
        public readonly List<NewsBox> boxes = new List<NewsBox>();
        public string footerLeft;
        public string footerRight;
    }

    public class NewsBox
    {
        public string title;
        public readonly List<string> lines = new List<string>();
        /// <summary>Kutunun altında kalın yazılan sonuç cümlesi.</summary>
        public string verdict;
        /// <summary>Siyah zemin, açık yazı (soruşturma gibi).</summary>
        public bool inverted;
        /// <summary>0, 1 ya da 2: hangi sütunda durduğu.</summary>
        public int column;
    }
}
