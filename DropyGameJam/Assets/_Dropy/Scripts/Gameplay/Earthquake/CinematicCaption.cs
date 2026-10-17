using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Ara sahnelerin (deprem, hapishane) kendi arayüzü: siyaha geçiş ve alt alta beliren yazı satırları.
    /// Kendi Canvas'ını kurar; sahne tek başına açılınca boş ve görünmez kalır.
    /// </summary>
    public class CinematicCaption : MonoBehaviour
    {
        [Tooltip("Satırlar arası süre (saniye)")]
        [SerializeField, Min(0f)] float lineInterval = 1.4f;

        CanvasGroup fade;
        TextMeshProUGUI text;
        CanvasGroup textGroup;

        void Awake()
        {
            var canvasGo = new GameObject("DepremCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;

            var band = Ui.Image(root, "Band", new Color(0f, 0f, 0f, 0.55f), raycast: false);
            band.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1500f, 170f));
            textGroup = band.gameObject.AddComponent<CanvasGroup>();
            textGroup.alpha = 0f;
            text = Ui.Text(band.transform, "Text", "", 40f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            text.rectTransform.Stretch(30f, 16f, 30f, 16f);

            var black = Ui.Image(root, "Fade", Color.black, raycast: false);
            black.rectTransform.Stretch();
            fade = black.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 0f;
        }

        /// <summary>Ekranı anında siyah yapar (sahne yüklenir yüklenmez, ilk kare görünmeden).</summary>
        public void SetBlack() => fade.alpha = 1f;

        public IEnumerator FadeFromBlack(float seconds) => FadeTo(fade, 0f, seconds);

        public IEnumerator FadeToBlack(float seconds) => FadeTo(fade, 1f, seconds);

        /// <summary>Satırları belli aralıklarla alt alta ekler; sahne kapanana kadar ekranda kalırlar.</summary>
        public IEnumerator ShowLines(IList<string> lines, float delay)
        {
            if (lines == null || lines.Count == 0)
                yield break;
            yield return new WaitForSeconds(delay);
            text.text = "";
            StartCoroutine(FadeTo(textGroup, 1f, 0.4f));
            for (int i = 0; i < lines.Count; i++)
            {
                text.text += (i > 0 ? "\n" : "") + lines[i];
                if (i < lines.Count - 1)
                    yield return new WaitForSeconds(lineInterval);
            }
        }

        static IEnumerator FadeTo(CanvasGroup group, float target, float seconds)
        {
            float start = group.alpha;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                group.alpha = Mathf.Lerp(start, target, t / seconds);
                yield return null;
            }
            group.alpha = target;
        }
    }
}
