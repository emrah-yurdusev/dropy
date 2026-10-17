using System.Collections.Generic;
using UnityEngine;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Müteahhidin bir belge satırı sorulunca verdiği cevabın kalıbı. Alan anahtarı <see cref="FieldKey"/>'den;
    /// savunma: o satırda kusur varken (çürük dosyanın sahibi kendini savunurken) kullanılır.
    /// </summary>
    [System.Serializable]
    public class FieldLine
    {
        [Tooltip("FieldKey: claimed, lab, hammer, use, height, columns, shearWall, soil, foundation, floors, name, note, damage")]
        public string field;
        [Tooltip("Satırda kusur varken mi (savunma) yoksa temizken mi (sakin) söylenir")]
        public bool defensive;
        [TextArea(1, 3)] public string text;
    }

    /// <summary>
    /// Bir konuşma tarzının cümleleri. Her liste boş bırakılabilir: boşsa bir üst katmana bakılır
    /// (dosyanın kendi cümlesi → karakter → kişilik → koddaki varsayılan). Listede birden çok cümle varsa biri seçilir.
    /// Yer tutucular: {isim} {hitap} {bina} {kat} {kunye} {rapor} {cekic} {tavan} {not} {kasa} {eksik} {hazirlik}.
    /// </summary>
    [System.Serializable]
    public class VoiceLines
    {
        [Tooltip("Oyuncuya nasıl hitap eder ({hitap}): hocam, evladım, beyefendi...")]
        public string address;
        [Tooltip("Yazı hızı çarpanı (1 = normal, 0 = üst katmandan al). Heyecanlı hızlı, çekingen yavaş konuşur")]
        [Range(0f, 2f)] public float typingSpeed;

        [Header("Giriş")]
        [Tooltip("İlk cümlenin başına eklenir (ayrı tıklama istemez)")]
        public List<string> greetings = new List<string>();

        [Header("Cevap kartlarına tepki")]
        [Tooltip("Oyuncu kural/kitap kartını seçti, dosya temiz")]
        public List<string> firmHonest = new List<string>();
        [Tooltip("Oyuncu kural/kitap kartını seçti, dosya çürük")]
        public List<string> firmDishonest = new List<string>();
        [Tooltip("Oyuncu yumuşak kartı seçti, dosya temiz")]
        public List<string> softHonest = new List<string>();
        [Tooltip("Oyuncu yumuşak kartı seçti, dosya çürük")]
        public List<string> softDishonest = new List<string>();

        [Header("Belge satırı sorulunca")]
        public List<FieldLine> fields = new List<FieldLine>();
        [Tooltip("Bazen satır cevabının sonuna eklenen kendine has laf")]
        public List<string> tics = new List<string>();

        [Header("Mühürden sonra")]
        public List<string> approved = new List<string>();
        public List<string> rejected = new List<string>();
        public List<string> shearWall = new List<string>();
        [Tooltip("Temiz dosyası haksız yere reddedilince")]
        public List<string> unjustReject = new List<string>();
    }

    /// <summary>
    /// Kişilik tipi (samimi, çekingen, manipülatif...): o tipteki bütün müteahhitlerin ortak konuşma tarzı.
    /// Burayı düzenlemek o tipi kullanan herkesi değiştirir; tek bir kişiyi değiştirmek için
    /// <see cref="ContractorProfile"/>'deki üzerine yazma alanlarını kullan.
    /// </summary>
    [CreateAssetMenu(menuName = "Dropy/Masa/Kişilik", fileName = "Personality")]
    public class Personality : ScriptableObject
    {
        public string typeName = "Kişilik";
        [TextArea(2, 4)] public string description;
        public VoiceLines lines = new VoiceLines();
    }
}
