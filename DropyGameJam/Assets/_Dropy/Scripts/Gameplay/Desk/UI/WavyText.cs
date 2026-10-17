using TMPro;
using UnityEngine;

namespace Dropy.Gameplay
{
    /// <summary>
    /// TextMeshPro yazısını kötü baskı gibi dalgalandırır: satır boyunca dalga, harf köşelerinde pürüz.
    /// Gazetedeki okunmayan dolgu yazılar ve mürekkebi dağılmış manşet için. Yazı değişince kendini yeniler.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class WavyText : MonoBehaviour
    {
        [Tooltip("Satır boyunca dalga yüksekliği (piksel)")]
        public float amplitude = 3f;
        [Tooltip("Dalga sıklığı")]
        public float frequency = 0.045f;
        [Tooltip("Harf köşelerinin ayrı ayrı oynaması (mürekkep dağılması)")]
        public float roughness = 0.6f;

        TMP_Text text;
        float seed;
        bool dirty = true;
        bool applying;

        void Awake()
        {
            text = GetComponent<TMP_Text>();
            seed = Random.Range(0f, 100f);
        }

        void OnEnable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            dirty = true;
        }

        void OnDisable() => TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

        void OnTextChanged(Object changed)
        {
            if (!applying && changed == text)
                dirty = true;
        }

        void LateUpdate()
        {
            if (!dirty)
                return;
            dirty = false;
            Apply();
        }

        void Apply()
        {
            applying = true;
            text.ForceMeshUpdate();
            var info = text.textInfo;
            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                var vertices = info.meshInfo[m].vertices;
                if (vertices == null)
                    continue;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var v = vertices[i];
                    float wave = Mathf.Sin(v.x * frequency + seed) + 0.5f * Mathf.Sin(v.x * frequency * 2.3f + v.y * 0.05f + seed * 1.7f);
                    float jitter = (Mathf.PerlinNoise(v.x * 0.35f + seed, v.y * 0.35f) - 0.5f) * 2f * roughness;
                    vertices[i] = new Vector3(v.x + jitter, v.y + wave * amplitude + jitter, v.z);
                }
            }
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            applying = false;
        }
    }
}
