using System.Collections.Generic;
using UnityEngine;

namespace Dropy.Gameplay
{
    public enum DocumentKind
    {
        BasvuruFormu,
        ProjePaftasi,
        ZeminEtudu,
        BetonRaporu,
        Diger
    }

    [System.Serializable]
    public class DocumentField
    {
        [Tooltip("Satırın ne olduğu (künye/rapor alanı); müteahhide sorulunca cevap buna göre üretilir")]
        public string key;
        public string label;
        public string value;
        [Tooltip("Deprem güvenliğini etkileyen ölümcül hata mı? Kırmızı kalemle işaretlenirse revizyonla düzeltilir")]
        public bool isFatalFlaw;
    }

    /// <summary>Müteahhidin masaya bıraktığı tek bir belge (pafta, zemin etüdü, rapor...).</summary>
    [CreateAssetMenu(menuName = "Dropy/Masa/Belge", fileName = "Document")]
    public class DocumentData : ScriptableObject
    {
        public string title = "Proje Paftası";
        public DocumentKind kind = DocumentKind.ProjePaftasi;
        [TextArea] public string subtitle;
        public List<DocumentField> fields = new List<DocumentField>();
        [Tooltip("Belgenin altına küçük not (ör. imza, tarih)")]
        public string footer;
    }
}
