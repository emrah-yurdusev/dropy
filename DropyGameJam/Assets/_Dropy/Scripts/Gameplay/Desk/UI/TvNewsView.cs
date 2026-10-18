using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Gün başında ekranı kaplayan eski televizyon: sabah haberi (spiker, sağda haber görseli, SON DAKİKA bandı,
    /// kayan yazı), altında "GÜN N" ve önemli notlar. Oyuncu tıklayana kadar kalır.
    /// Görünüş <see cref="NewsStyle"/>'dan, içerik <see cref="TvBroadcast"/>'tan gelir; görsel bağlı değilse
    /// koddan çizilen yer tutucu görünür.
    /// </summary>
    public class TvNewsView : MonoBehaviour
    {
        const float ReferenceWidth = 1920f;

        NewsStyle style;
        System.Func<bool> inputBlocked;
        RectTransform root;
        CanvasGroup group;
        Image studioBackground;
        GameObject silhouette;
        Image anchorImage;
        RawImage videoImage;
        VideoPlayer video;
        RenderTexture videoTexture;
        AspectRatioFitter videoFit;
        Image graphic;
        TextMeshProUGUI graphicLabel;
        GameObject live;
        TextMeshProUGUI clock;
        TextMeshProUGUI headline;
        RectTransform tickerContent;
        TextMeshProUGUI tickerText;
        float tickerLoop;
        TextMeshProUGUI dayTitle;
        TextMeshProUGUI notes;
        TextMeshProUGUI hint;
        bool showing;

        public void Build(RectTransform parent, NewsStyle newsStyle, System.Func<bool> blocked)
        {
            style = newsStyle;
            inputBlocked = blocked;

            var backdrop = Ui.Image(parent, "TvNews", style.backdrop);
            root = backdrop.rectTransform.Stretch();
            group = root.gameObject.AddComponent<CanvasGroup>();

            var shadow = Ui.Image(root, "Shadow", new Color(0f, 0f, 0f, 0.5f), Ui.Shadow, raycast: false).rectTransform;
            shadow.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 82f), new Vector2(1240f, 760f));
            var body = Ui.Image(root, "Body", style.tvFrame != null ? Color.white : new Color(0.11f, 0.09f, 0.08f),
                style.tvFrame != null ? style.tvFrame : Ui.Rounded).rectTransform;
            body.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 95f), new Vector2(1180f, 700f));

            var screenMask = Ui.Image(body, "Screen", style.studio, Ui.Rounded);
            screenMask.rectTransform.Stretch(44f, 92f, 44f, 40f);
            screenMask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var screen = screenMask.rectTransform;
            BuildStudio(screen);
            BuildGraphic(screen);
            BuildBands(screen);

            var lines = Ui.Rect("Scanlines", screen).Stretch();
            var scan = lines.gameObject.AddComponent<RawImage>();
            scan.texture = PrintArt.Scanlines;
            scan.color = new Color(1f, 1f, 1f, style.scanlines);
            scan.uvRect = new Rect(0f, 0f, 1f, 568f / 4f);
            scan.raycastTarget = false;

            var brand = Ui.Text(body, "Brand", style.tvBrand, 24f, new Color(0.45f, 0.4f, 0.36f), TextAlignmentOptions.Left, FontStyles.Bold);
            brand.characterSpacing = 18f;
            brand.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(60f, 46f), new Vector2(400f, 40f));
            Knob(body, -150f, 30f, new Color(0.23f, 0.19f, 0.17f));
            Knob(body, -105f, 30f, new Color(0.23f, 0.19f, 0.17f));
            Knob(body, -66f, 12f, new Color(0.48f, 0.82f, 0.42f));

            dayTitle = Ui.Text(root, "Day", "", 84f, new Color(0.95f, 0.91f, 0.85f), TextAlignmentOptions.Center, FontStyles.Bold);
            dayTitle.characterSpacing = 8f;
            dayTitle.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(1200f, 100f));
            notes = Ui.Text(root, "Notes", "", 28f, new Color(0.95f, 0.91f, 0.85f), TextAlignmentOptions.Top);
            notes.richText = true;
            notes.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 145f), new Vector2(1400f, 90f));
            hint = Ui.Text(root, "Hint", style.continueHint, 22f, new Color(0.79f, 0.73f, 0.65f), TextAlignmentOptions.Center, FontStyles.Italic);
            hint.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 36f), new Vector2(800f, 40f));

            ApplyFont(root);
            root.gameObject.SetActive(false);
        }

        void BuildStudio(RectTransform screen)
        {
            studioBackground = Ui.Image(screen, "Studio", Color.white, raycast: false);
            studioBackground.rectTransform.Stretch();
            studioBackground.sprite = style.studioBackground;
            studioBackground.gameObject.SetActive(style.studioBackground != null);
            if (style.studioBackground == null)
            {
                var shade = style.studio * 0.8f;
                shade.a = 1f;
                var panel = Ui.Image(screen, "StudioPanel", shade, raycast: false).rectTransform;
                panel.anchorMin = Vector2.zero;
                panel.anchorMax = new Vector2(0.58f, 1f);
                panel.offsetMin = panel.offsetMax = Vector2.zero;
            }

            // Spiker alanı: ekranın sol yarısı. Sırayla video, spiker görseli ya da siluet.
            var area = Ui.Rect("Anchor", screen);
            area.anchorMin = Vector2.zero;
            area.anchorMax = new Vector2(0.58f, 1f);
            area.offsetMin = area.offsetMax = Vector2.zero;

            silhouette = Ui.Rect("Silhouette", area).Stretch().gameObject;
            var dark = style.studio * 0.55f;
            dark.a = 1f;
            var shoulders = Ui.Image(silhouette.transform, "Shoulders", dark, raycast: false);
            shoulders.sprite = PrintArt.Disc;
            shoulders.rectTransform.Place(new Vector2(0.45f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(330f, 300f));
            var head = Ui.Image(silhouette.transform, "Head", dark, raycast: false);
            head.sprite = PrintArt.Disc;
            head.rectTransform.Place(new Vector2(0.45f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 290f), new Vector2(120f, 138f));

            anchorImage = Ui.Image(area, "AnchorImage", Color.white, raycast: false);
            anchorImage.rectTransform.Stretch();
            anchorImage.preserveAspect = true;

            videoImage = Ui.Rect("AnchorVideo", area).Stretch().gameObject.AddComponent<RawImage>();
            videoImage.raycastTarget = false;
            // Video hangi oranda olursa olsun spiker alanını ezmeden doldurur (taşan kısmı ekran maskesi keser).
            videoFit = videoImage.gameObject.AddComponent<AspectRatioFitter>();
            videoFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            video = videoImage.gameObject.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.isLooping = true;
            video.renderMode = VideoRenderMode.RenderTexture;
            video.audioOutputMode = VideoAudioOutputMode.None;
            video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        }

        void BuildGraphic(RectTransform screen)
        {
            var frame = Ui.Image(screen, "GraphicFrame", Color.white, raycast: false).rectTransform;
            frame.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -46f), new Vector2(390f, 290f));
            graphic = Ui.Image(frame, "Graphic", new Color(0.91f, 0.88f, 0.81f), raycast: false);
            graphic.rectTransform.Stretch(5f, 5f, 5f, 5f);
            graphic.preserveAspect = true;
            graphicLabel = Ui.Text(frame, "Label", "", 30f, new Color(0.35f, 0.08f, 0.06f), TextAlignmentOptions.Bottom, FontStyles.Bold);
            graphicLabel.rectTransform.Stretch(10f, 14f, 10f, 10f);

            live = Ui.Rect("Live", screen).gameObject;
            var liveRt = (RectTransform)live.transform;
            liveRt.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -356f), new Vector2(390f, 40f));
            var pill = Ui.Image(liveRt, "Pill", style.breakingColor, raycast: false).rectTransform;
            pill.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(110f, 36f));
            var liveText = Ui.Text(pill, "Text", style.liveLabel, 22f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            liveText.rectTransform.Stretch();
            clock = Ui.Text(liveRt, "Clock", "", 26f, Color.white, TextAlignmentOptions.Right);
            clock.rectTransform.Stretch(120f, 0f, 0f, 0f);

            var bug = Ui.Image(screen, "Channel", Color.white, raycast: false).rectTransform;
            bug.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -22f), new Vector2(170f, 40f));
            var bugText = Ui.Text(bug, "Text", style.channelName, 24f, style.studio, TextAlignmentOptions.Center, FontStyles.Bold);
            bugText.rectTransform.Stretch();
        }

        void BuildBands(RectTransform screen)
        {
            var lower = Ui.Rect("LowerThird", screen);
            lower.anchorMin = new Vector2(0f, 0f);
            lower.anchorMax = new Vector2(1f, 0f);
            lower.pivot = new Vector2(0.5f, 0f);
            lower.anchoredPosition = new Vector2(0f, 50f);
            lower.sizeDelta = new Vector2(0f, 62f);
            var breaking = Ui.Image(lower, "Breaking", style.breakingColor, raycast: false).rectTransform;
            breaking.anchorMin = Vector2.zero;
            breaking.anchorMax = new Vector2(0f, 1f);
            breaking.pivot = new Vector2(0f, 0.5f);
            breaking.sizeDelta = new Vector2(220f, 0f);
            var breakingText = Ui.Text(breaking, "Text", style.breakingLabel, 26f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            breakingText.rectTransform.Stretch();
            var bar = Ui.Image(lower, "Headline", new Color(0.96f, 0.95f, 0.92f), raycast: false).rectTransform;
            bar.Stretch(220f, 0f, 0f, 0f);
            headline = Ui.Text(bar, "Text", "", 32f, new Color(0.07f, 0.07f, 0.07f), TextAlignmentOptions.Left);
            headline.enableAutoSizing = true;
            headline.fontSizeMin = 20f;
            headline.fontSizeMax = 32f;
            headline.textWrappingMode = TextWrappingModes.NoWrap;
            headline.rectTransform.Stretch(18f, 0f, 18f, 0f);

            var ticker = Ui.Image(screen, "Ticker", style.tickerColor, raycast: false).rectTransform;
            ticker.anchorMin = new Vector2(0f, 0f);
            ticker.anchorMax = new Vector2(1f, 0f);
            ticker.pivot = new Vector2(0.5f, 0f);
            ticker.sizeDelta = new Vector2(0f, 50f);
            ticker.gameObject.AddComponent<RectMask2D>();
            tickerContent = Ui.Rect("Content", ticker);
            tickerContent.anchorMin = new Vector2(0f, 0f);
            tickerContent.anchorMax = new Vector2(0f, 1f);
            tickerContent.pivot = new Vector2(0f, 0.5f);
            tickerText = Ui.Text(tickerContent, "Text", "", 26f, new Color(0.1f, 0.09f, 0.08f), TextAlignmentOptions.Left);
            tickerText.textWrappingMode = TextWrappingModes.NoWrap;
            tickerText.rectTransform.Stretch();
        }

        static void Knob(RectTransform body, float x, float size, Color color)
        {
            var knob = Ui.Image(body, "Knob", color, raycast: false);
            knob.sprite = PrintArt.Disc;
            knob.rectTransform.Place(new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(x, 46f), new Vector2(size, size));
        }

        void ApplyFont(Transform parent)
        {
            if (style.tvFont == null)
                return;
            foreach (var text in parent.GetComponentsInChildren<TextMeshProUGUI>(true))
                text.font = style.tvFont;
        }

        /// <summary>Yayını gösterir, oyuncu tıklayınca (Space / Enter da olur) kapanır.</summary>
        public IEnumerator Show(TvBroadcast broadcast)
        {
            // Önce aç: kapalı nesnedeki VideoPlayer oynatılamaz.
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            Fill(broadcast);
            showing = true;
            Sfx.TvOn();
            yield return Fade(0f, 1f, 0.35f);
            Sfx.Breaking();
            // İlk yarım saniye tıklama sayılmaz ki önceki ekrandan kalan tık yanlışlıkla geçirmesin.
            for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
                yield return null;
            while (!ContinuePressed())
                yield return null;
            yield return Fade(1f, 0f, 0.5f);
            Hide();
        }

        /// <summary>Yarıda kesilen akışta ekranı kapatır.</summary>
        public void Hide()
        {
            showing = false;
            if (video.isPlaying)
                video.Stop();
            root.gameObject.SetActive(false);
        }

        void Fill(TvBroadcast broadcast)
        {
            headline.text = broadcast.headline;
            clock.text = string.Format(style.clockFormat, broadcast.day);
            dayTitle.text = string.Format(style.dayTitleFormat, broadcast.day);
            notes.text = string.Join("\n", broadcast.notes);

            graphic.sprite = broadcast.graphic;
            graphic.color = broadcast.graphic != null ? Color.white : new Color(0.91f, 0.88f, 0.81f);
            graphicLabel.text = broadcast.graphic != null ? "" : broadcast.graphicLabel;

            var clip = broadcast.clip != null ? broadcast.clip : style.defaultAnchorClip;
            videoImage.gameObject.SetActive(clip != null);
            anchorImage.gameObject.SetActive(clip == null && style.anchor != null);
            anchorImage.sprite = style.anchor;
            silhouette.SetActive(clip == null && style.anchor == null);
            if (clip != null)
            {
                int width = Mathf.Max(2, (int)clip.width), height = Mathf.Max(2, (int)clip.height);
                if (videoTexture == null || videoTexture.width != width || videoTexture.height != height)
                {
                    if (videoTexture != null)
                        videoTexture.Release();
                    videoTexture = new RenderTexture(width, height, 0);
                    video.targetTexture = videoTexture;
                    videoImage.texture = videoTexture;
                }
                videoFit.aspectRatio = width / (float)height;
                video.clip = clip;
                video.Play();
            }

            // Kayan yazı iki kez yan yana yazılır; ilk kopyanın genişliği kadar kayınca başa sarar, dikiş görünmez.
            string once = string.Join("     •     ", broadcast.ticker) + "     •     ";
            tickerText.text = once + once;
            tickerLoop = tickerText.GetPreferredValues(once).x;
            tickerContent.sizeDelta = new Vector2(tickerLoop * 2f + 40f, 0f);
            tickerContent.anchoredPosition = Vector2.zero;
        }

        void Update()
        {
            if (!showing)
                return;
            float speed = style.tickerSpeed * ((RectTransform)root.transform).rect.width / ReferenceWidth;
            var p = tickerContent.anchoredPosition;
            p.x -= speed * Time.unscaledDeltaTime;
            if (tickerLoop > 0f && p.x < -tickerLoop)
                p.x += tickerLoop;
            tickerContent.anchoredPosition = p;
            live.SetActive(Mathf.Repeat(Time.unscaledTime, 1.2f) < 0.85f);
            hint.alpha = 0.45f + 0.35f * Mathf.Sin(Time.unscaledTime * 3f);
        }

        IEnumerator Fade(float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            group.alpha = to;
        }

        bool ContinuePressed()
        {
            if (inputBlocked != null && inputBlocked())
                return false;
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            return (mouse != null && mouse.leftButton.wasPressedThisFrame)
                || (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame));
        }

        void OnDestroy()
        {
            if (videoTexture != null)
                videoTexture.Release();
        }
    }
}
