using System.Collections.Generic;
using UnityEngine;

namespace Dropy.Gameplay
{
    public enum MessageCondition
    {
        Always,
        Bought,
        NotBought,
        SealWas
    }

    /// <summary>Akşam telefona düşen mesaj. Koşul tutmazsa gelmez.</summary>
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
    }

    /// <summary>Gün başında, ilk müteahhitten önce gelen telefon.</summary>
    [System.Serializable]
    public class PhoneCall
    {
        [Min(1)] public int day = 1;
        public string caller = "Müdür (telefon)";
        [TextArea(2, 4)] public List<string> lines = new List<string>();
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
        [Tooltip("Gün sırasıyla sabah radyosu (0 = 1. gün)")]
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
        [Min(1)] public int bondMax = 5;

        public string Radio(int day) => day >= 1 && day <= radio.Count ? radio[day - 1] : null;

        public float Tremor(int day) => day >= 1 && day <= tremors.Count ? tremors[day - 1] : 0f;
    }
}
