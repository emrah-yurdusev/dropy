using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace Dropy.Gameplay
{
    public enum MessageCondition
    {
        Always,
        Bought,
        NotBought,
        SealWas,
        [Tooltip("Kasa eşik değerin altındaysa")] MoneyBelow,
        [Tooltip("Aile bağı eşik değerin altındaysa")] BondBelow,
        [Tooltip("Kusursuz kayıt primi yattıysa (son akşam, bütün dosyalar doğru, rüşvet yok)")] PerfectRecord
    }

    public enum MessageTiming
    {
        [Tooltip("Akşam telefonunda")] Evening,
        [Tooltip("Gün içinde, belli bir dosya masadayken telefon titrer")] DuringFile
    }

    /// <summary>Telefona düşen mesaj: akşam ya da gün içinde bir dosya masadayken. Koşul tutmazsa gelmez. Metinde {kasa} gibi yer tutucular olabilir.</summary>
    [System.Serializable]
    public class PhoneMessage
    {
        [Min(1)] public int day = 1;
        public string sender;
        [Tooltip("Aileden mi (eşin, Elif, annen)? Aile küserse bu mesajlar gelmez")]
        public bool family;
        [TextArea(2, 4)] public string text;
        public MessageCondition condition;
        [Tooltip("Bought / NotBought için")]
        public ShopItem item;
        [Tooltip("SealWas için: bu dosyaya bu mühür basıldıysa")]
        public BuildingCase other;
        public Seal seal;
        [Tooltip("MoneyBelow / BondBelow için eşik")]
        public int threshold;
        public MessageTiming timing;
        [Tooltip("DuringFile için: günün kaçıncı dosyası (0 = ilk)")]
        public int fileOfDay;
    }

    /// <summary>Gün başında, ilk müteahhitten önce gelen telefon.</summary>
    [System.Serializable]
    public class PhoneCall
    {
        [Min(1)] public int day = 1;
        public string caller = "Müdür";
        [TextArea(2, 4)] public List<string> lines = new List<string>();
    }

    /// <summary>
    /// Bir sabahın televizyon haberi: alt banttaki manşet ve kayan yazı. Görsel ve video inspector'dan bağlanır;
    /// dosya üreticisi yazıları yeniden yazarken bağlanan görseli ve videoyu korur.
    /// </summary>
    [System.Serializable]
    public class DailyNews
    {
        [Min(1)] public int day = 1;
        [Tooltip("Alt bantta, SON DAKİKA'nın yanında")]
        [TextArea(1, 3)] public string headline;
        [Tooltip("Ekranın altında kayan satırlar")]
        public List<string> ticker = new List<string>();
        [Tooltip("Sağ üstteki gün göstergesinde o günün üstüne gelince görünen uyarı (deprem bekleniyor...)")]
        [TextArea(1, 2)] public string forecast;
        [Tooltip("Ekranın sağındaki görselin üstüne yazılan kısa etiket (görsel yoksa yer tutucuda görünür)")]
        public string graphicLabel;
        [Tooltip("Ekranın sağındaki haber görseli (harita, fotoğraf...)")]
        public Sprite graphic;
        [Tooltip("Varsa spiker yerine bu video oynar")]
        public VideoClip clip;
    }

    /// <summary>
    /// Akşam aileden biri bir şey ister; mesajın içinde "Öde" düğmesi olur. Ödenirse aile bağı artar,
    /// ödenmeden sabah olursa azalır; ertesi akşam bir cevap, depremden sonra gazetede bir satır gelir.
    /// </summary>
    [System.Serializable]
    public class FamilyNeed
    {
        [Min(1)] public int day = 1;
        public string name;
        [Min(0)] public int price;
        [Tooltip("İsteyen ve ertesi akşam cevap yazan kişi")]
        public string sender;
        [Tooltip("İsteğin mesajı; altında Öde düğmesi çıkar")]
        [TextArea(2, 4)] public string request;
        [TextArea(2, 4)] public string paidReply;
        [TextArea(2, 4)] public string unpaidReply;
        [TextArea(1, 3)] public string endingPaid;
        [TextArea(1, 3)] public string endingUnpaid;
    }

    /// <summary>
    /// Masanın etrafındaki hikâye: gün başı telefonları, sabah radyosu, akşam mesajları,
    /// aile masrafları ve depremden önceki sarsıntılar. Kurallara dokunmaz.
    /// </summary>
    [CreateAssetMenu(menuName = "Dropy/Masa/Hikâye", fileName = "Story")]
    public class StoryData : ScriptableObject
    {
        [Header("Gün başı")]
        public List<PhoneCall> calls = new List<PhoneCall>();
        [Tooltip("Sabah televizyon haberleri (gün başına bir tane)")]
        public List<DailyNews> news = new List<DailyNews>();
        [Tooltip("Eski sabah radyosu: o günün haberi yoksa manşet olarak kullanılır (0 = 1. gün)")]
        [TextArea(1, 3)] public List<string> radio = new List<string>();

        [Header("Sarsıntılar")]
        [Tooltip("Gün sırasıyla gündüz sarsıntısının şiddeti (0 = yok). Günün ikinci dosyasında hissedilir")]
        public List<float> tremors = new List<float>();

        [Header("Akşam")]
        public List<PhoneMessage> messages = new List<PhoneMessage>();
        public List<FamilyNeed> needs = new List<FamilyNeed>();

        [Header("Aile bağı")]
        [Tooltip("Başlangıç. Hiçbir isteği ödemeyen oyuncu 5. akşamın sonunda sıfıra düşer; birini ödemek yeter")]
        [Min(1)] public int bondStart = 4;

        [Header("Kusursuz kayıt primi")]
        [Tooltip("Son akşam, bütün dosyalar doğru ve rüşvet yoksa kasaya girer; PerfectRecord koşullu mesajlar bunu haber verir ({prim})")]
        [Min(0)] public int perfectBonus = 4000;
        [Min(1)] public int bondMax = 5;

        public string Radio(int day) => day >= 1 && day <= radio.Count ? radio[day - 1] : null;

        public DailyNews News(int day) => news.Find(n => n.day == day);

        public float Tremor(int day) => day >= 1 && day <= tremors.Count ? tremors[day - 1] : 0f;
    }
}
