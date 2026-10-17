using System.Collections.Generic;
using UnityEngine;

namespace Dropy.Gameplay
{
    [System.Serializable]
    public class RegulationPage
    {
        public string title;
        [Tooltip("Bu sayfanın kitaba girdiği gün")]
        [Min(1)] public int day = 1;
        [TextArea(4, 12)] public string body;
    }

    /// <summary>Sol paneldeki mevzuat kitapçığı.</summary>
    [CreateAssetMenu(menuName = "Dropy/Masa/Mevzuat", fileName = "Regulations")]
    public class RegulationData : ScriptableObject
    {
        public string bookTitle = "Deprem Yönetmeliği (Özet)";
        public List<RegulationPage> pages = new List<RegulationPage>();
    }
}
