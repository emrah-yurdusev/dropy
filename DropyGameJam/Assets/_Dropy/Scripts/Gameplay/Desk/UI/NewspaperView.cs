using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Oyun sonu gazetesi: siyah beyaz, hafif eğik sayfa; künye, manşet, noktalı baskı fotoğraf, üç sütunda okunur
    /// kutular ve aralarda okunmayan dalgalı dolgu haberler. Görünüş ve sabit yazılar <see cref="NewsStyle"/>'dan,
    /// içerik <see cref="NewspaperPage"/>'den gelir (<see cref="NewspaperWriter"/> oyunun sonucundan yazar).
    /// </summary>
    public class NewspaperView : MonoBehaviour
    {
        static readonly CultureInfo Tr = new CultureInfo("tr-TR");

        NewsStyle style;
        RectTransform root;
        RectTransform sheet;

        public event System.Action ReportClicked;
        public event System.Action RestartClicked;

        public void Build(RectTransform parent, NewsStyle newsStyle)
        {
            style = newsStyle;
            root = Ui.Image(parent, "Newspaper", new Color(0.05f, 0.04f, 0.04f, 0.88f)).rectTransform.Stretch();

            var report = Ui.Button(root, "Report", "DOSYA KARNESİ", new Color(0.29f, 0.36f, 0.48f), Color.white, 24f);
            ((RectTransform)report.transform).Place(new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 22f), new Vector2(300f, 60f));
            report.onClick.AddListener(() => ReportClicked?.Invoke());
            var again = Ui.Button(root, "Restart", "YENİDEN OYNA", new Color(0.85f, 0.6f, 0.2f), Color.white, 24f);
            ((RectTransform)again.transform).Place(new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(12f, 22f), new Vector2(300f, 60f));
            again.onClick.AddListener(() => RestartClicked?.Invoke());

            root.gameObject.SetActive(false);
        }

        public void Hide() => root.gameObject.SetActive(false);

        public void Show(NewspaperPage page)
        {
            if (sheet != null)
                Destroy(sheet.gameObject);
            BuildSheet(page);
            Show();
        }

        /// <summary>Son yazılan sayfayı yeniden açar (karneden dönerken).</summary>
        public void Show()
        {
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
        }

        void BuildSheet(NewspaperPage page)
        {
            // Sayfa ve gölgesi tek bir eğik tutucuda: yeni sonuçta tutucu silinip baştan kurulur.
            sheet = Ui.Rect("Page", root);
            sheet.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 52f), new Vector2(1180f, 966f));
            sheet.localRotation = Quaternion.Euler(0f, 0f, style.tilt);
            var shadow = Ui.Image(sheet, "Shadow", new Color(0f, 0f, 0f, 0.45f), Ui.Shadow, raycast: false);
            shadow.rectTransform.Stretch(-12f, -26f, -24f, -10f);
            var paper = Ui.Image(sheet, "Sheet", style.paperTexture != null ? Color.white : style.paper);
            if (style.paperTexture != null)
                paper.sprite = style.paperTexture;
            paper.rectTransform.Stretch();
            var layout = paper.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(46, 46, 26, 24);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var page0 = paper.rectTransform;
            Row(page0, style.issue, page.edition, style.price, 18f, 24f);
            Rule(page0, 1.5f);
            var masthead = Text(page0, style.masthead, 88f, style.mastheadFont, TextAlignmentOptions.Center, FontStyles.Bold);
            masthead.characterSpacing = 4f;
            Rough(masthead, 0f, style.inkRoughness * 1.4f);
            DoubleRule(page0);
            var special = Text(page0, style.specialEdition, 22f, style.headlineFont, TextAlignmentOptions.Center, FontStyles.Bold);
            special.characterSpacing = 16f;
            Rule(page0, 1f);
            var headline = Text(page0, page.headline.ToUpper(Tr), 54f, style.headlineFont, TextAlignmentOptions.Left, FontStyles.Bold);
            headline.lineSpacing = -8f;
            Rough(headline, 0f, style.inkRoughness);
            if (!string.IsNullOrEmpty(page.subhead))
                Text(page0, page.subhead, 28f, style.bodyFont, TextAlignmentOptions.Left, FontStyles.Italic);
            Rule(page0, 1f);

            // Sütunlar başlıktan artan yeri alır; kendi boylarında kalır, taşan kısım kesilir (gazete sayfası gibi).
            var columnsRow = Ui.Rect("Columns", page0);
            var rowSize = columnsRow.gameObject.AddComponent<LayoutElement>();
            rowSize.minHeight = 0f;
            rowSize.preferredHeight = 0f;
            rowSize.flexibleHeight = 1f;
            columnsRow.gameObject.AddComponent<RectMask2D>();
            var row = columnsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 28f;
            row.padding = new RectOffset(0, 0, 8, 0);
            row.childAlignment = TextAnchor.UpperLeft;
            row.childControlWidth = true;
            row.childControlHeight = false;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = false;
            var columns = new RectTransform[3];
            for (int i = 0; i < columns.Length; i++)
                columns[i] = Column(columnsRow, i == 0 ? 1.1f : 1f);

            int filler = 0;
            Photo(columns[0], page);
            if (!string.IsNullOrEmpty(page.story))
                Text(columns[1], page.story, 26f, style.headlineFont, TextAlignmentOptions.Left, FontStyles.Bold);
            filler = Filler(columns[1], filler);
            foreach (var box in page.boxes)
                Box(columns[Mathf.Clamp(box.column, 0, 2)], box);
            filler = Filler(columns[1], filler);
            filler = Filler(columns[2], filler);
            Ad(columns[2]);
            filler = Filler(columns[2], filler);
            Filler(columns[0], filler);

            if (!string.IsNullOrEmpty(page.verdict))
                Verdict(page0, page.verdict);
            DoubleRule(page0);
            Row(page0, page.footerLeft, "", page.footerRight, 20f, 28f);
        }

        RectTransform Column(RectTransform parent, float weight)
        {
            var column = Ui.Rect("Column", parent);
            column.pivot = new Vector2(0.5f, 1f);
            var size = column.gameObject.AddComponent<LayoutElement>();
            size.minWidth = 0f;
            size.preferredWidth = 0f;
            size.flexibleWidth = weight;
            column.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return column;
        }

        void Photo(RectTransform column, NewspaperPage page)
        {
            var frame = Ui.Image(column, "Photo", style.ink, raycast: false).rectTransform;
            frame.gameObject.AddComponent<LayoutElement>().preferredHeight = 170f;
            var back = Ui.Image(frame, "Paper", style.paper, raycast: false);
            back.rectTransform.Stretch(2f, 2f, 2f, 2f);
            var custom = page.mugshot ? style.mugshotPhoto : style.playerPhoto;
            var photo = Ui.Image(frame, "Image", custom != null ? Color.white : style.ink, raycast: false);
            photo.sprite = custom != null ? custom : PrintArt.Halftone(page.mugshot);
            photo.preserveAspect = custom != null;
            photo.rectTransform.Stretch(2f, 2f, 2f, 2f);
            if (!string.IsNullOrEmpty(page.photoCaption))
                Text(column, page.photoCaption, 19f, style.bodyFont, TextAlignmentOptions.Left, FontStyles.Italic);
        }

        void Box(RectTransform column, NewsBox box)
        {
            var border = Ui.Image(column, "Box", style.ink, raycast: false);
            var outer = border.gameObject.AddComponent<VerticalLayoutGroup>();
            outer.padding = new RectOffset(2, 2, 2, 2);
            outer.childControlWidth = outer.childControlHeight = true;
            outer.childForceExpandWidth = true;
            outer.childForceExpandHeight = false;

            var fg = box.inverted ? style.paper : style.ink;
            var inner = Ui.Image(border.transform, "Inner", box.inverted ? style.ink : style.paper, raycast: false);
            var layout = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 10);
            layout.spacing = 5f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = Text((RectTransform)inner.transform, box.title, 21f, style.headlineFont, TextAlignmentOptions.Left, FontStyles.Bold);
            title.characterSpacing = 6f;
            title.color = fg;
            var rule = Ui.Image(inner.transform, "Rule", fg, raycast: false);
            rule.gameObject.AddComponent<LayoutElement>().preferredHeight = 1.5f;
            if (box.lines.Count > 0)
            {
                var lines = Text((RectTransform)inner.transform, string.Join("\n", box.lines), 19f, style.bodyFont, TextAlignmentOptions.Left, FontStyles.Normal);
                lines.color = fg;
                lines.lineSpacing = 6f;
            }
            if (!string.IsNullOrEmpty(box.verdict))
            {
                var verdict = Text((RectTransform)inner.transform, box.verdict, 23f, style.headlineFont, TextAlignmentOptions.Left, FontStyles.Bold);
                verdict.color = fg;
            }
        }

        /// <summary>Ailenin sonu: sütunların altında tam genişlikte siyah şerit.</summary>
        void Verdict(RectTransform parent, string text)
        {
            var strip = Ui.Image(parent, "Verdict", style.ink, raycast: false);
            var layout = strip.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 8, 8);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var label = Text((RectTransform)strip.transform, text, 24f, style.headlineFont, TextAlignmentOptions.Center, FontStyles.Bold);
            label.color = style.paper;
        }

        /// <summary>Sıradaki dolgu haberi okunmaz, dalgalı yazar; dolgu bittiyse bir şey eklemez.</summary>
        int Filler(RectTransform column, int index)
        {
            if (index >= style.fillers.Count)
                return index;
            var text = Text(column, style.fillers[index], 17f, style.bodyFont, TextAlignmentOptions.Justified, FontStyles.Normal);
            text.color = style.fillerInk;
            text.lineSpacing = -4f;
            Rough(text, style.fillerWave, 0.9f);
            return index + 1;
        }

        void Ad(RectTransform column)
        {
            if (string.IsNullOrEmpty(style.adTitle))
                return;
            var box = new NewsBox { title = style.adTitle };
            box.lines.Add(style.adLine);
            Box(column, box);
            var ad = column.GetChild(column.childCount - 1);
            foreach (var t in ad.GetComponentsInChildren<TextMeshProUGUI>())
            {
                t.alignment = TextAlignmentOptions.Center;
                Rough(t, style.fillerWave * 0.6f, 0.6f);
            }
        }

        TextMeshProUGUI Text(RectTransform parent, string value, float size, TMP_FontAsset font, TextAlignmentOptions align, FontStyles fontStyle)
        {
            var text = Ui.Text(parent, "Text", value, size, style.ink, align, fontStyle);
            text.richText = true;
            if (font != null)
                text.font = font;
            return text;
        }

        static void Rough(TextMeshProUGUI text, float wave, float roughness)
        {
            if (wave <= 0f && roughness <= 0f)
                return;
            var wavy = text.gameObject.AddComponent<WavyText>();
            wavy.amplitude = wave;
            wavy.roughness = roughness;
        }

        void Row(RectTransform parent, string left, string center, string right, float size, float height)
        {
            var row = Ui.Rect("Row", parent);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            Place(Ui.Text(row, "Left", left ?? "", size, style.ink, TextAlignmentOptions.Left), row);
            Place(Ui.Text(row, "Center", center ?? "", size, style.ink, TextAlignmentOptions.Center), row);
            Place(Ui.Text(row, "Right", right ?? "", size, style.ink, TextAlignmentOptions.Right), row);
        }

        void Place(TextMeshProUGUI text, RectTransform row)
        {
            text.rectTransform.Stretch();
            text.textWrappingMode = TextWrappingModes.NoWrap;
            if (style.bodyFont != null)
                text.font = style.bodyFont;
        }

        void Rule(RectTransform parent, float thickness)
        {
            var rule = Ui.Image(parent, "Rule", style.ink, raycast: false);
            rule.gameObject.AddComponent<LayoutElement>().preferredHeight = thickness;
        }

        void DoubleRule(RectTransform parent)
        {
            var holder = Ui.Rect("DoubleRule", parent);
            holder.gameObject.AddComponent<LayoutElement>().preferredHeight = 7f;
            var top = Ui.Image(holder, "Top", style.ink, raycast: false).rectTransform;
            top.anchorMin = new Vector2(0f, 1f);
            top.anchorMax = Vector2.one;
            top.pivot = new Vector2(0.5f, 1f);
            top.sizeDelta = new Vector2(0f, 3f);
            var bottom = Ui.Image(holder, "Bottom", style.ink, raycast: false).rectTransform;
            bottom.anchorMin = Vector2.zero;
            bottom.anchorMax = new Vector2(1f, 0f);
            bottom.pivot = new Vector2(0.5f, 0f);
            bottom.sizeDelta = new Vector2(0f, 1.5f);
        }
    }
}
