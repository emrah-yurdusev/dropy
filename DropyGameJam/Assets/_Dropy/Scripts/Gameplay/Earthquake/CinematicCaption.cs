using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Ara sahnelerin (deprem, hapishane) kendi arayüzü: siyaha geçiş ve sabah haberindeki gibi "SON DAKİKA" şeridi.
    /// Her satır lacivert bir haber şeridi olarak soldan açılır; şeritler alttan yukarı dizilir, altta kanal yazısı durur.
    /// Kendi Canvas'ını kurar; sahne tek başına açılınca boş ve görünmez kalır.
    /// </summary>
    public class CinematicCaption : MonoBehaviour
    {
        static readonly Color Navy = new Color(0.07f, 0.23f, 0.39f, 0.96f);
        static readonly Color Red = new Color(0.75f, 0.13f, 0.11f);
        const float RowHeight = 58f;
        const float LabelWidth = 230f;
        const float RowGap = 6f;
        const float RevealTime = 0.35f;

        [Tooltip("Satırlar arası süre (saniye)")]
        [SerializeField, Min(0f)] float lineInterval = 1.4f;
        [Tooltip("Sabah haberinin yazı tipi (NewsStyle → TV yazı tipi). Boşsa varsayılan")]
        [SerializeField] TMP_FontAsset font;
        [SerializeField] string breakingLabel = "SON DAKİKA";
        [SerializeField] string channel = "K6 HABER  ·  CANLI";

        CanvasGroup fade;
        CanvasGroup textGroup;
        TextMeshProUGUI continueHint;
        /// <summary>ShowLines bütün satırları ekrana koydu mu (ara sahne bunu bekleyip tıklama ister).</summary>
        bool linesDone = true;
        RectTransform stack;
        RectTransform footer;
        readonly List<GameObject> rows = new List<GameObject>();

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

            // Şerit yığını sol altta; satır eklendikçe yukarı doğru büyür.
            stack = Ui.Rect("Strip", root);
            stack.anchorMin = stack.anchorMax = Vector2.zero;
            stack.pivot = Vector2.zero;
            stack.anchoredPosition = new Vector2(70f, 64f);
            stack.sizeDelta = new Vector2(1780f, 0f);
            textGroup = stack.gameObject.AddComponent<CanvasGroup>();
            textGroup.alpha = 0f;
            var layout = stack.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = RowGap;
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            stack.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var channelText = Text(stack, "Channel", "<color=#e0453a>●</color>  " + channel, 20f, new Color(1f, 1f, 1f, 0.85f));
            channelText.characterSpacing = 3f;
            footer = channelText.rectTransform;
            footer.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;

            // Sağ altta "devam" ipucu: yazılar okunana kadar ara sahne kapanmaz, oyuncu tıklayınca geçer.
            continueHint = Ui.Text(root, "Continue", "Devam etmek için tıkla  ▶", 26f, Color.white, TextAlignmentOptions.BottomRight, FontStyles.Bold);
            continueHint.rectTransform.Stretch(0f, 22f, 70f, 0f);
            continueHint.outlineWidth = 0.25f;
            continueHint.outlineColor = new Color32(0, 0, 0, 200);
            if (font != null)
                continueHint.font = font;
            continueHint.gameObject.SetActive(false);

            var black = Ui.Image(root, "Fade", Color.black, raycast: false);
            black.rectTransform.Stretch();
            fade = black.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 0f;
        }

        /// <summary>Ekranı anında siyah yapar (sahne yüklenir yüklenmez, ilk kare görünmeden).</summary>
        public void SetBlack() => fade.alpha = 1f;

        public IEnumerator FadeFromBlack(float seconds) => FadeTo(fade, 0f, seconds);

        public IEnumerator FadeToBlack(float seconds) => FadeTo(fade, 1f, seconds);

        /// <summary>Satırları belli aralıklarla birer haber şeridi olarak ekler; sahne kapanana kadar ekranda kalırlar.</summary>
        public IEnumerator ShowLines(IList<string> lines, float delay)
        {
            if (lines == null || lines.Count == 0)
                yield break;
            linesDone = false;
            yield return new WaitForSeconds(delay);
            foreach (var row in rows)
                Destroy(row);
            rows.Clear();
            StartCoroutine(FadeTo(textGroup, 1f, 0.25f));
            for (int i = 0; i < lines.Count; i++)
            {
                StartCoroutine(Reveal(AddRow(lines[i], i == 0)));
                if (i < lines.Count - 1)
                    yield return new WaitForSeconds(lineInterval);
            }
            linesDone = true;
        }

        /// <summary>
        /// Yazıların hepsi çıkana kadar, sonra en az <paramref name="minHold"/> saniye bekler; ardından "Devam etmek için tıkla"
        /// belirir ve oyuncu tıklayana (ya da Space/Enter) kadar sahne kapanmaz. Böylece yazılar okunmadan geçilmez.
        /// </summary>
        public IEnumerator WaitForContinue(float minHold)
        {
            while (!linesDone)
                yield return null;
            yield return new WaitForSeconds(minHold);
            continueHint.gameObject.SetActive(true);
            yield return null;
            while (!ContinuePressed())
            {
                continueHint.alpha = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f);
                yield return null;
            }
            continueHint.gameObject.SetActive(false);
        }

        static bool ContinuePressed()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            return (mouse != null && mouse.leftButton.wasPressedThisFrame)
                || (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame));
        }

        /// <summary>
        /// Bir haber satırı: solda kırmızı "SON DAKİKA" etiketi (yalnız ilk satırda; diğerlerinde aynı genişlikte boşluk),
        /// sağda metin kadar uzayan lacivert şerit.
        /// </summary>
        RectTransform AddRow(string line, bool first)
        {
            var row = Ui.Rect("Row", stack);
            row.pivot = new Vector2(0f, 0.5f);
            row.SetSiblingIndex(footer.GetSiblingIndex());
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;
            row.gameObject.AddComponent<CanvasGroup>().alpha = 0f;

            if (first)
            {
                var label = Ui.Image(row, "Label", Red, raycast: false);
                label.gameObject.AddComponent<LayoutElement>().preferredWidth = LabelWidth;
                var labelText = Text(label.rectTransform, "Text", breakingLabel, 26f, Color.white, TextAlignmentOptions.Center);
                labelText.characterSpacing = 2f;
                labelText.rectTransform.Stretch();
            }
            else
                Ui.Rect("Spacer", row).gameObject.AddComponent<LayoutElement>().preferredWidth = LabelWidth;

            var bar = Ui.Image(row, "Bar", Navy, raycast: false);
            var barLayout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            barLayout.padding = new RectOffset(24, 30, 0, 0);
            barLayout.childAlignment = TextAnchor.MiddleLeft;
            barLayout.childControlWidth = true;
            barLayout.childControlHeight = true;
            barLayout.childForceExpandWidth = false;
            Text(bar.rectTransform, "Text", line, 32f, Color.white, TextAlignmentOptions.MidlineLeft);

            rows.Add(row.gameObject);
            return row;
        }

        TextMeshProUGUI Text(RectTransform parent, string name, string value, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var text = Ui.Text(parent, name, value, size, color, align, FontStyles.Bold);
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            if (font != null)
                text.font = font;
            return text;
        }

        /// <summary>Satır soldan açılır (TV şeridi gibi) ve belirir.</summary>
        static IEnumerator Reveal(RectTransform row)
        {
            var group = row.GetComponent<CanvasGroup>();
            for (float t = 0f; t < RevealTime; t += Time.deltaTime)
            {
                if (row == null)
                    yield break;
                float k = 1f - Mathf.Pow(1f - t / RevealTime, 3f);
                row.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, k), 1f, 1f);
                group.alpha = k;
                yield return null;
            }
            if (row == null)
                yield break;
            row.localScale = Vector3.one;
            group.alpha = 1f;
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
