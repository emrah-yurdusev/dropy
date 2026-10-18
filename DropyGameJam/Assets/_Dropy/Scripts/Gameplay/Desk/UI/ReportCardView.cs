using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Oyun sonu dosya karnesi: sarı bir dosya klasörünün içinde tek sayfalık büro raporu. Sade tutulur:
    /// kâğıt, mürekkep ve bir tablo. Her dosyada basılan mühür; yanlışların altında doğrusu ve kuralın kısa açıklaması.
    /// Gazete ne olduğunu söyler, karne nedenini öğretir.
    /// </summary>
    public class ReportCardView : MonoBehaviour
    {
        static readonly Color Paper = new Color(0.98f, 0.96f, 0.9f);
        static readonly Color Ink = new Color(0.16f, 0.15f, 0.14f);
        static readonly Color Faded = new Color(0.45f, 0.42f, 0.38f);
        static readonly Color Rule = new Color(0f, 0f, 0f, 0.12f);
        static readonly Color Folder = new Color(0.84f, 0.69f, 0.45f);
        static readonly Color FolderBack = new Color(0.76f, 0.6f, 0.38f);
        static readonly Color FolderInk = new Color(0.36f, 0.25f, 0.12f);
        static readonly Color MarkerGreen = new Color(0.1f, 0.55f, 0.25f);
        static readonly Color MarkerRed = new Color(0.82f, 0.12f, 0.1f);
        static readonly Color MarkerBlue = new Color(0.15f, 0.38f, 0.8f);

        const float FolderWidth = 1180f, FolderHeight = 920f;
        // Tablo sütunları: sol kenardan uzaklık ve genişlik (kâğıdın iç genişliği ~1030). Doğru mühür yalnız yanlışların altında yazar.
        const float DayX = 0f, DayW = 90f;
        const float NameX = 96f, NameW = 600f;
        const float PlayedX = 760f, SealW = 260f;
        const float RowHeight = 42f;

        RectTransform root;
        RectTransform folder;
        TMP_FontAsset hand;

        public event System.Action NewspaperClicked;
        public event System.Action RestartClicked;

        /// <param name="handFont">Keçeli kalem yazısının fontu (öğretici kalemle aynı); boşsa kalın yazı.</param>
        public void Build(RectTransform parent, TMP_FontAsset handFont = null)
        {
            hand = handFont;
            root = Ui.Image(parent, "ReportCard", new Color(0.05f, 0.04f, 0.04f, 0.88f)).rectTransform.Stretch();

            var paper = Ui.Button(root, "Newspaper", "GAZETE", new Color(0.29f, 0.36f, 0.48f), Color.white, 24f);
            ((RectTransform)paper.transform).Place(new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 22f), new Vector2(300f, 60f));
            paper.onClick.AddListener(() => NewspaperClicked?.Invoke());
            var again = Ui.Button(root, "Restart", "YENİDEN OYNA", new Color(0.85f, 0.6f, 0.2f), Color.white, 24f);
            ((RectTransform)again.transform).Place(new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(12f, 22f), new Vector2(300f, 60f));
            again.onClick.AddListener(() => RestartClicked?.Invoke());

            root.gameObject.SetActive(false);
        }

        /// <summary>Sonuçlardan klasörü baştan kurar (açmaz).</summary>
        public void Set(IList<DeskManager.CaseResult> results)
        {
            if (folder != null)
                Destroy(folder.gameObject);
            BuildFolder(results);
        }

        public void Show()
        {
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            Sfx.PaperSlide();
        }

        public void Hide() => root.gameObject.SetActive(false);

        void BuildFolder(IList<DeskManager.CaseResult> results)
        {
            // Klasör hafif eğik; içindeki kâğıt düz durur ki tablo net okunsun.
            folder = Ui.Rect("Folder", root);
            folder.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(FolderWidth, FolderHeight));
            folder.localRotation = Quaternion.Euler(0f, 0f, -0.6f);
            folder.SetSiblingIndex(0);
            Ui.DropShadow(folder, 24f, new Vector2(8f, -12f), 0.5f);

            // Sekme klasörün üstünden taşar; gövde altını örter.
            var tab = Ui.Image(folder, "Tab", FolderBack, Ui.Rounded, raycast: false).rectTransform;
            tab.Place(new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(46f, -18f), new Vector2(360f, 62f));
            var tabLabel = Ui.Text(tab, "Label", "DOSYA KARNESİ", 22f, FolderInk, TextAlignmentOptions.Center, FontStyles.Bold);
            tabLabel.characterSpacing = 4f;
            tabLabel.rectTransform.Stretch(0f, 14f, 0f, 0f);
            Ui.Image(folder, "Body", Folder, Ui.Rounded, raycast: false).rectTransform.Stretch();

            var sheet = Ui.Image(folder, "Sheet", Paper, Ui.Rounded).rectTransform.Stretch(30f, 28f, 30f, 30f);
            sheet.localRotation = Quaternion.Euler(0f, 0f, 0.6f);
            Ui.DropShadow(sheet, 8f, new Vector2(2f, -3f), 0.2f);
            BuildSheet(sheet, results);
        }

        void BuildSheet(RectTransform sheet, IList<DeskManager.CaseResult> results)
        {
            int total = results.Count;
            int correct = DeskHud.CountCorrect(results);
            bool bribed = results.Any(r => r.bribed);

            // Başlık: büro adı, altında ince mürekkep çizgisi.
            Line(sheet, "Heading", "FAYKÖY İSKÂN BÜROSU", 30f, Ink, -26f, 44f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold).characterSpacing = 2f;
            Line(sheet, "Tag", "DOSYA KARNESİ", 18f, Faded, -26f, 44f, TextAlignmentOptions.MidlineRight, FontStyles.Bold).characterSpacing = 4f;
            HLine(sheet, -76f, 2f, Ink);
            Line(sheet, "Info", "T.C. FAYKÖY BELEDİYESİ  ·  İMAR MÜDÜRLÜĞÜ  ·  DEPREM HAFTASI", 15f, Faded, -86f, 26f,
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold).characterSpacing = 2f;
            Line(sheet, "Count", $"{Economy.Days} GÜN  ·  {total} DOSYA", 15f, Faded, -86f, 26f,
                TextAlignmentOptions.MidlineRight, FontStyles.Bold).characterSpacing = 2f;

            // Sonuç tek satır: büyük doğru sayısı ve yanında kısa not.
            var score = Line(sheet, "Score", $"<b>{correct} / {total}</b>  <size=40%><color=#{ColorUtility.ToHtmlStringRGB(Faded)}>dosyada doğru mühür</color></size>",
                54f, Ink, -124f, 70f, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            score.richText = true;
            // Rüşvet alındıysa sağda kırmızı kalemle, hafif eğik: "Rüşvet kaydı var!"
            if (bribed)
            {
                var stamp = Ui.Rect("BribeNote", sheet);
                stamp.Place(new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-50f, -158f), new Vector2(420f, 70f));
                stamp.localRotation = Quaternion.Euler(0f, 0f, -4f);
                var note = Marker(stamp, "Text", "Rüşvet kaydı var!", 48f, MarkerRed, 0f, 420f);
                note.alignment = TextAlignmentOptions.MidlineRight;
            }
            HLine(sheet, -206f, 1.5f, Rule);

            var columns = Ui.Rect("Columns", sheet);
            columns.anchorMin = new Vector2(0f, 1f);
            columns.anchorMax = Vector2.one;
            columns.pivot = new Vector2(0.5f, 1f);
            columns.offsetMin = new Vector2(40f, -246f);
            columns.offsetMax = new Vector2(-40f, -218f);
            ColumnHead(columns, "GÜN", DayX, DayW);
            ColumnHead(columns, "BİNA", NameX, NameW);
            ColumnHead(columns, "BASTIĞIN", PlayedX, SealW);

            var list = ScrollList(sheet);
            int day = 0;
            foreach (var r in results)
            {
                bool first = r.day != day;
                day = r.day;
                if (first && list.childCount > 0)
                    Divider(list);
                Row(list, r, DeskHud.RightSeal(r, results), first);
            }
        }

        static TextMeshProUGUI Line(RectTransform sheet, string name, string text, float size, Color color, float y, float height,
            TextAlignmentOptions align, FontStyles style)
        {
            var t = Ui.Text(sheet, name, text, size, color, align, style);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(40f, y - height);
            rt.offsetMax = new Vector2(-40f, y);
            return t;
        }

        static void HLine(RectTransform sheet, float y, float thickness, Color color)
        {
            var line = Ui.Image(sheet, "Rule", color, raycast: false).rectTransform;
            line.anchorMin = new Vector2(0f, 1f);
            line.anchorMax = Vector2.one;
            line.pivot = new Vector2(0.5f, 1f);
            line.offsetMin = new Vector2(40f, y - thickness);
            line.offsetMax = new Vector2(-40f, y);
        }

        static void ColumnHead(RectTransform parent, string text, float x, float width)
        {
            var t = Cell(parent, text, text, 14f, Faded, x, width, FontStyles.Bold);
            t.characterSpacing = 2f;
        }

        /// <summary>Tablonun kaydırılabilen gövdesi; sığmazsa sağda ince kaydırma çubuğu çıkar.</summary>
        static RectTransform ScrollList(RectTransform sheet)
        {
            var area = Ui.Rect("List", sheet);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(40f, 26f);
            area.offsetMax = new Vector2(-28f, -252f);

            var viewport = Ui.Image(area, "Viewport", new Color(0f, 0f, 0f, 0f)).rectTransform.Stretch(0f, 0f, 12f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Ui.Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var track = Ui.Image(area, "Scrollbar", new Color(0f, 0f, 0f, 0.05f), Ui.Rounded).rectTransform;
            track.anchorMin = new Vector2(1f, 0f);
            track.anchorMax = Vector2.one;
            track.pivot = new Vector2(1f, 0.5f);
            track.sizeDelta = new Vector2(6f, 0f);
            var handleArea = Ui.Rect("Sliding", track).Stretch();
            var handle = Ui.Image(handleArea, "Handle", new Color(0f, 0f, 0f, 0.25f), Ui.Rounded);
            handle.rectTransform.Stretch();
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;

            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        static void Divider(RectTransform list)
        {
            var holder = Ui.Rect("Divider", list);
            holder.gameObject.AddComponent<LayoutElement>().preferredHeight = 9f;
            var line = Ui.Image(holder, "Line", Rule, raycast: false).rectTransform;
            line.anchorMin = new Vector2(0f, 0.5f);
            line.anchorMax = new Vector2(1f, 0.5f);
            line.sizeDelta = new Vector2(0f, 1.5f);
        }

        /// <summary>Bir dosyanın satırı; yanlışsa altında kuralın açıklaması.</summary>
        void Row(RectTransform list, DeskManager.CaseResult r, Seal right, bool firstOfDay)
        {
            bool ok = r.seal == right;
            var row = Ui.Rect(r.buildingCase.buildingName, list);
            var rowLayout = row.gameObject.AddComponent<VerticalLayoutGroup>();
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowLayout.padding = new RectOffset(0, 0, 0, ok ? 0 : 8);

            var line = Ui.Rect("Line", row);
            line.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;
            if (firstOfDay)
                Cell(line, "Day", $"{r.day}. gün", 17f, Faded, DayX, DayW, FontStyles.Normal);
            // Bina adı ve (rüşvet alındıysa) hemen yanında kırmızı kalemle not: yan yana dizilir.
            var nameCell = Ui.Rect("NameCell", line);
            nameCell.anchorMin = Vector2.zero;
            nameCell.anchorMax = new Vector2(0f, 1f);
            nameCell.pivot = new Vector2(0f, 0.5f);
            nameCell.offsetMin = new Vector2(NameX, 0f);
            nameCell.offsetMax = new Vector2(NameX + NameW, 0f);
            var nameLayout = nameCell.gameObject.AddComponent<HorizontalLayoutGroup>();
            nameLayout.spacing = 14f;
            nameLayout.childAlignment = TextAnchor.MiddleLeft;
            nameLayout.childControlWidth = true;
            nameLayout.childControlHeight = true;
            nameLayout.childForceExpandWidth = false;
            nameLayout.childForceExpandHeight = true;
            var nameText = Ui.Text(nameCell, "Name", r.buildingCase.buildingName, 20f, Ink, TextAlignmentOptions.MidlineLeft);
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            if (r.bribed)
                Marker(nameCell, "Bribe", "rüşvet!", 26f, MarkerRed, 0f, 0f);
            // Basılan mühür renkli markerla: onay yeşil, red kırmızı, perde mavi.
            Marker(line, "Played", DeskManager.SealName(r.seal), 28f, SealInk(r.seal), PlayedX, SealW);

            if (!ok)
            {
                var why = Ui.Rect("Why", row);
                var whyLayout = why.gameObject.AddComponent<HorizontalLayoutGroup>();
                whyLayout.padding = new RectOffset((int)NameX, 12, 0, 0);
                whyLayout.childControlWidth = true;
                whyLayout.childControlHeight = true;
                whyLayout.childForceExpandWidth = true;
                whyLayout.childForceExpandHeight = false;
                string rightSeal = $"<color=#{ColorUtility.ToHtmlStringRGB(SealInk(right))}>{DeskManager.SealName(right)}</color>";
                var text = Ui.Text(why, "Text", $"<b>Doğrusu: {rightSeal}.</b> {BuildingRules.Explain(r.buildingCase, r.seal)}", 17f, Faded,
                    TextAlignmentOptions.TopLeft, FontStyles.Italic);
                text.richText = true;
            }
        }

        static Color SealInk(Seal seal)
        {
            switch (seal)
            {
                case Seal.Approve: return MarkerGreen;
                case Seal.ShearWall: return MarkerBlue;
                default: return MarkerRed;
            }
        }

        /// <summary>Renkli keçeli kalemle el yazısı (öğretici kalemin fontu); font yoksa kalın yazı.</summary>
        TextMeshProUGUI Marker(RectTransform line, string name, string text, float size, Color color, float x, float width)
        {
            var t = Cell(line, name, text, size, color, x, width, FontStyles.Normal);
            if (hand == null)
            {
                t.fontStyle = FontStyles.Bold;
                t.fontSize = size * 0.75f;
                return t;
            }
            t.font = hand;
            // İnce Caveat harfleri aynı mürekkeple kalınlaşır: keçeli kalem gibi dolgun okunur.
            var material = t.fontMaterial;
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetColor(ShaderUtilities.ID_OutlineColor, color);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.12f);
            t.fontMaterial = material;
            return t;
        }

        static TextMeshProUGUI Cell(RectTransform line, string name, string text, float size, Color color, float x, float width, FontStyles style)
        {
            var t = Ui.Text(line, name, text, size, color, TextAlignmentOptions.MidlineLeft, style);
            t.rectTransform.anchorMin = new Vector2(0f, 0f);
            t.rectTransform.anchorMax = new Vector2(0f, 1f);
            t.rectTransform.pivot = new Vector2(0f, 0.5f);
            t.rectTransform.offsetMin = new Vector2(x, 0f);
            t.rectTransform.offsetMax = new Vector2(x + width, 0f);
            return t;
        }
    }
}
