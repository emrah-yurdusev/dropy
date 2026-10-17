using UnityEngine;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Bir müteahhit: adı, modeli, kişilik tipi ve yalnızca ona özel cümleler. Dosyalar (<see cref="BuildingCase"/>)
    /// buna bağlanır. Üzerine yazma alanındaki dolu her liste kişilik tipindekinin yerine geçer, boşlar tipten gelir.
    /// </summary>
    [CreateAssetMenu(menuName = "Dropy/Masa/Müteahhit", fileName = "Contractor")]
    public class ContractorProfile : ScriptableObject
    {
        [Tooltip("Ekranda görünen ad (\"Hanım\" ile biterse kadın modeli seçilir)")]
        public string displayName = "Müteahhit";
        [Tooltip("Boşsa DeskManager'daki varsayılan karakterlerden cinsiyete uygun biri gelir")]
        public GameObject prefab;
        public Personality personality;
        [Tooltip("Yalnız bu kişiye özel cümleler; boş listeler kişilik tipinden gelir")]
        public VoiceLines overrides = new VoiceLines();
    }
}
