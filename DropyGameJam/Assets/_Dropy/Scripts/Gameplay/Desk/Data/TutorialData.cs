using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Dropy.Gameplay
{
    public enum PenShape
    {
        [Tooltip("Hedefin etrafına elle çizilmiş daire")] Circle,
        [Tooltip("Hedefin altına dalgalı çizgi")] Underline,
        [Tooltip("Hedeften ikinci hedefe ok")] Arrow,
        [Tooltip("İki hedef arasında çift yönlü ok")] DoubleArrow
    }

    public enum TutorialTargetKind
    {
        None,
        [Tooltip("Belgede bir satır (alan anahtarıyla: claimed, lab, use, height, columns...)")] Field,
        [Tooltip("Yönetmelikte bir konunun sekmesi")] RuleTab,
        [Tooltip("Mühür tepsisindeki bir mühür")] Seal,
        [Tooltip("Ek rapordaki çekiç düğmesi")] Hammer,
        [Tooltip("Bina incelemesi kartı (döndürülebilir maket, büyüteç)")] Building
    }

    [System.Serializable]
    public class TutorialTarget
    {
        public TutorialTargetKind kind;
        [Tooltip("Field için: FieldKey (claimed, lab, use, height, columns, hammer...)")]
        public string field;
        [Tooltip("RuleTab için")]
        public RuleTopic topic;
        [Tooltip("Seal için")]
        public Seal seal;
    }

    /// <summary>Kalemle çizilen bir hamle: şekil, hedef(ler) ve yanına el yazısıyla not.</summary>
    [System.Serializable]
    public class TutorialStep
    {
        [Min(1)] public int day = 1;
        [Tooltip("Günün kaçıncı dosyasında (0 = ilk)")]
        public int fileOfDay;
        public PenShape shape;
        public TutorialTarget from = new TutorialTarget();
        [Tooltip("Ok için ikinci hedef")]
        public TutorialTarget to = new TutorialTarget();
        [Tooltip("El yazısıyla yanına yazılan not")]
        public string note;
    }

    /// <summary>
    /// Fragman tarzı öğretici: belgeler açılınca ekrana kırmızı kalemle daire, ok ve el yazısı notlar çizilir.
    /// Yalnız 1. gün: 1. dosya beton, ek raporun önceliği ve çekiç; 2. dosya yumuşak kat (perde) ve kesik kolon.
    /// Oyuncuyu durdurmaz (tıklamalar geçer), mühür basılınca silinir; her hamle oyunda bir kez gösterilir.
    /// </summary>
    [CreateAssetMenu(menuName = "Dropy/Masa/Kalem Öğretici", fileName = "Tutorial")]
    public class TutorialData : ScriptableObject
    {
        [Tooltip("El yazısı font (boşsa varsayılan)")]
        public TMP_FontAsset font;
        public Color ink = new Color(0.82f, 0.12f, 0.1f);
        [Tooltip("Kalemin hızı (piksel/saniye, 1920 genişlikte)")]
        public float penSpeed = 1500f;
        [Tooltip("Bir hamle bitince sonrakine geçmeden önceki bekleme (saniye)")]
        public float pause = 0.35f;
        public List<TutorialStep> steps = new List<TutorialStep>();
    }
}
