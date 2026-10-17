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
        [Tooltip("Boşsa DeskManager'daki varsayılan karakterlerden biri gelir")]
        public GameObject contractorPrefab;

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

        [Header("Çekiç (3. günden itibaren)")]
        [Tooltip("Çekicin okuduğu beton. 0 ise laboratuvar değeriyle aynı okunur")]
        public int hammerConcrete;

        [Header("Ekonomi")]
        [Tooltip("0 değilse Onay mührünün üstünde bu tutar yazar (rüşvet)")]
        public int approveFeeOverride;

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
