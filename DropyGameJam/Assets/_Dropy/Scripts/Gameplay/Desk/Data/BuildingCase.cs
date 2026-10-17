using System.Collections.Generic;
using UnityEngine;

namespace Dropy.Gameplay
{
    public enum GroundUse { Konut, Market }
    public enum Foundation { Radye, Kazik }
    public enum ColumnState { Tam, Kesik }
    public enum SoilType { Kaya, Kil, Kum }

    [System.Serializable]
    public class CaseQuestion
    {
        [Tooltip("Oyuncunun cevap balonunda görünen soru")]
        public string question;
        [TextArea(2, 4)] public string answer;
    }

    public enum PressureCondition
    {
        [Tooltip("Her zaman, en başta")] Always,
        [Tooltip("Kira düşünce kasa eksik hazırlığa (çanta, sabitleme) yetmiyorsa")] PrepShort,
        [Tooltip("Aile bağı 2 ya da altındaysa")] BondLow,
        [Tooltip("Oyuncu daha önce bir dosyada rüşvet aldıysa")] TookBribe,
        [Tooltip("Yukarıdakilerin hiçbiri tutmadıysa")] Otherwise
    }

    /// <summary>
    /// Rüşvet dosyasında RED basılınca müteahhidin araya girmesi. {kasa} ve {eksik} kira sonrası
    /// kasa ve eksik hazırlığa (çanta, sabitleme) kalan açıkla değişir. Yalan söylemez: yalnızca oyuncunun gerçek durumunu kullanır.
    /// </summary>
    [System.Serializable]
    public class PressureLine
    {
        public PressureCondition condition;
        [TextArea(2, 4)] public string text;
    }

    /// <summary>Önceki bir dosyaya hangi mühür basıldıysa müteahhidin ona değinmesi.</summary>
    [System.Serializable]
    public class CaseCallback
    {
        public BuildingCase other;
        public Seal seal;
        [TextArea(2, 4)] public string line;
    }

    /// <summary>
    /// Masaya gelen bir dosya: müteahhit, proje künyesi ve ek rapor.
    /// Doğru mühür buradaki değerlerden <see cref="BuildingRules"/> ile türetilir.
    /// </summary>
    [CreateAssetMenu(menuName = "Dropy/Masa/Vaka", fileName = "Case")]
    public class BuildingCase : ScriptableObject
    {
        public string contractorName = "Müteahhit";
        [Tooltip("Müteahhidin kendisi: konuşma tarzı (kişilik), modeli ve ona özel cümleler. Boşsa yalnız dosyadaki cümleler kullanılır")]
        public ContractorProfile contractor;
        [Tooltip("Boşsa müteahhidin modeli, o da yoksa DeskManager'daki varsayılan karakterlerden biri gelir")]
        public GameObject contractorPrefab;

        /// <summary>İsimdeki hitaptan: "Hanım" kadın, gerisi (Bey, Usta) erkek.</summary>
        public Gender ContractorGender => contractorName.TrimEnd().EndsWith("Hanım") ? Gender.Kadin : Gender.Erkek;

        [Header("Proje künyesi")]
        public string buildingName = "Bina";
        [Min(1)] public int floors = 3;
        [Tooltip("Künyede iddia edilen beton sınıfı (C30 için 30)")]
        public int claimedConcrete = 30;
        public GroundUse groundUse = GroundUse.Konut;
        [Tooltip("Zemin kat tavan yüksekliği (m)")]
        public float groundFloorHeight = 2.8f;
        public Foundation foundation = Foundation.Radye;
        public ColumnState columns = ColumnState.Tam;
        public bool hasShearWall;

        [Header("Ek rapor")]
        [Tooltip("Laboratuvar betonu. Künye ile çelişirse bu geçerlidir")]
        public int labConcrete = 30;
        public SoilType soil = SoilType.Kaya;
        [Tooltip("Ek raporun tek satırlık notu")]
        public string note;
        [Tooltip("Oyuncu not satırını sorarsa müteahhidin cevabı. Boşsa kusur notu gibi genel bir cevap üretilir")]
        public string noteAnswer;
        [Tooltip("İlk depremden sonra gelen dosyalarda hasar tespiti. Ağır hasarlı bina onarılmaz (4. gün kuralı)")]
        public DamageLevel damage;

        [Header("Yerinde (künyede görünmez)")]
        [Tooltip("Ruhsattan fazla çıkılmış kat. Künyede yazmaz; bina incelemesinde büyüteçle kırmızı görünür. Kuralı değiştirmez")]
        [Min(0)] public int illegalFloors;

        [Header("Çekiç (3. günden itibaren)")]
        [Tooltip("Çekicin okuduğu beton. 0 ise laboratuvar değeriyle aynı okunur")]
        public int hammerConcrete;

        [Header("Ekonomi")]
        [Tooltip("0 değilse Onay mührünün üstünde bu tutar yazar (rüşvet)")]
        public int approveFeeOverride;

        [Header("Gri rüşvet")]
        [Tooltip("0 değilse müteahhit konuşmada bu tutarı teklif eder; oyuncu kabul edip onay basarsa harca eklenir. Bina sağlam olsa da rüşvettir")]
        public int bribe;
        [TextArea(2, 4)] public string bribeOffer;
        [TextArea(2, 4)] public string bribeAcceptedLine;
        [TextArea(2, 4)] public string bribeRefusedLine;

        [Header("Konuşma")]
        [Tooltip("Varsa dallanan diyalog; yoksa aşağıdaki satırlar sırayla söylenir")]
        public DialogueData dialogue;
        [Tooltip("Daha önceki bir dosyanın sonucuna göre girişten önce söylenen satırlar")]
        public List<CaseCallback> callbacks = new List<CaseCallback>();
        [TextArea(2, 4)] public List<string> introLines = new List<string>();
        [Tooltip("Oyuncunun sorabileceği sorular. Cevaplar karakteri açar, kuralı değiştirmez")]
        public List<CaseQuestion> questions = new List<CaseQuestion>();

        [Header("Karar sonrası tepkiler")]
        [TextArea] public string approvedLine = "Sağ olun, hayırlı işler.";
        [TextArea] public string shearWallLine = "Perde mi? Masraf ama... peki, ekletiriz.";
        [TextArea] public string rejectedLine = "Bu iş burada bitmez!";
        [Tooltip("Boş değilse RED teslim edilince bir kez araya girer, oyuncu kararını gözden geçirebilir")]
        public List<PressureLine> rejectPressure = new List<PressureLine>();

        [Header("Deprem sonrası gazete")]
        [Tooltip("Bina çökerse manşet. Boşsa '<bina> çöktü'")]
        public string collapseHeadline;
        [Tooltip("Bina ayakta kalırsa kısa haber. Boşsa yalnızca adı listelenir")]
        public string standingLine;

        /// <summary>Betonun gerçek dayanımı: çekiç varsa çekiç, yoksa ek rapor.</summary>
        public int TrueConcrete => hammerConcrete > 0 ? hammerConcrete : labConcrete;

        public static string Concrete(int value) => "C" + value;
    }
}
