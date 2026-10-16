using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    public enum StampKind
    {
        None,
        Approve,
        Reject
    }

    /// <summary>
    /// Ekranda sürüklenebilen kağıt belge. Tıklayınca öne gelir; seçili araca göre
    /// damga basılır veya satırlar kırmızı kalemle işaretlenir.
    /// </summary>
    public class DocumentCard : MonoBehaviour, IPointerDownHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        static readonly Color Paper = new Color(0.98f, 0.96f, 0.9f);
        static readonly Color Ink = new Color(0.16f, 0.15f, 0.14f);
        static readonly Color Faded = new Color(0.4f, 0.38f, 0.35f);

        RectTransform rect;
        RectTransform dragArea;
        Image shadow;
        Vector2 dragOffset;
        DeskTools tools;
        RectTransform stampLayer;
        readonly List<Row> rows = new List<Row>();

        public DocumentData Data { get; private set; }
        public bool IsMain { get; private set; }
        public StampKind Stamp { get; private set; }
        public RectTransform RectTransform => rect;

        public event System.Action<DocumentCard> Dropped;

        public IEnumerable<DocumentField> MarkedFields
        {
            get
            {
                foreach (var row in rows)
                    if (row.Marked)
                        yield return row.Field;
            }
        }

        public static DocumentCard Create(DocumentData data, bool isMain, RectTransform parent, DeskTools tools)
        {
            var image = Ui.Image(parent, data.title, Paper, Ui.Rounded);
            var card = image.gameObject.AddComponent<DocumentCard>();
            card.Build(data, isMain, parent, tools);
            return card;
        }

        void Build(DocumentData data, bool isMain, RectTransform area, DeskTools deskTools)
        {
            Data = data;
            IsMain = isMain;
            dragArea = area;
            tools = deskTools;
            rect = (RectTransform)transform;
            rect.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 520f));
            shadow = Ui.DropShadow(rect, 16f, new Vector2(4f, -6f), 0.35f);

            var header = Ui.Image(rect, "Header", HeaderColor(data.kind), Ui.Rounded, raycast: false).rectTransform;
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.offsetMin = new Vector2(10f, -70f);
            header.offsetMax = new Vector2(-10f, -10f);
            var title = Ui.Text(header, "Title", data.title.ToUpperInvariant(), 24f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            title.rectTransform.Stretch(16f, 0f, 12f, 0f);

            var content = Ui.Rect("Content", rect).Stretch(22f, isMain ? 124f : 56f, 22f, 82f);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            if (!string.IsNullOrEmpty(data.subtitle))
            {
                var sub = Ui.Text(content, "Subtitle", data.subtitle, 17f, Faded, style: FontStyles.Italic);
                sub.gameObject.AddComponent<LayoutElement>().minHeight = 24f;
            }
            foreach (var field in data.fields)
                rows.Add(new Row(this, content, field));

            if (!string.IsNullOrEmpty(data.footer))
            {
                var footer = Ui.Text(rect, "Footer", data.footer, 15f, Faded, TextAlignmentOptions.BottomLeft, FontStyles.Italic);
                var footerRect = footer.rectTransform;
                footerRect.anchorMin = Vector2.zero;
                footerRect.anchorMax = new Vector2(isMain ? 0.52f : 1f, 0f);
                footerRect.pivot = new Vector2(0.5f, 0f);
                footerRect.offsetMin = new Vector2(22f, 16f);
                footerRect.offsetMax = new Vector2(-12f, 56f);
            }

            if (isMain)
            {
                var box = Ui.Image(rect, "StampBox", new Color(0f, 0f, 0f, 0.6f), Ui.Frame, raycast: false).rectTransform;
                box.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 18f), new Vector2(170f, 90f));
                var label = Ui.Text(box, "Label", "VİZE KARARI", 14f, Faded, TextAlignmentOptions.Top, FontStyles.Bold);
                label.rectTransform.Stretch(0f, 0f, 0f, 6f);
            }

            stampLayer = Ui.Rect("Stamps", rect).Stretch();
        }

        static Color HeaderColor(DocumentKind kind)
        {
            switch (kind)
            {
                case DocumentKind.BasvuruFormu: return new Color(0.24f, 0.32f, 0.45f);
                case DocumentKind.ProjePaftasi: return new Color(0.2f, 0.45f, 0.55f);
                case DocumentKind.ZeminEtudu: return new Color(0.5f, 0.38f, 0.22f);
                case DocumentKind.BetonRaporu: return new Color(0.42f, 0.42f, 0.42f);
                default: return new Color(0.35f, 0.3f, 0.45f);
            }
        }

        /// <summary>Verilen ekran noktasına damga basar. Ana belgede son damga kararı belirler.</summary>
        public void ApplyStamp(StampKind kind, Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(stampLayer, screenPoint, null, out var local);
            var stamp = CreateStampGraphic(stampLayer, kind);
            stamp.anchoredPosition = local;
            stamp.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-14f, 14f));
            stamp.gameObject.AddComponent<StampThump>();
            if (IsMain)
                Stamp = kind;
        }

        public static RectTransform CreateStampGraphic(Transform parent, StampKind kind)
        {
            bool approve = kind == StampKind.Approve;
            var color = approve ? new Color(0.12f, 0.55f, 0.25f, 0.85f) : new Color(0.8f, 0.12f, 0.12f, 0.85f);
            var frame = Ui.Image(parent, "Stamp", color, Ui.Frame, raycast: false).rectTransform;
            frame.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 70f));
            var text = Ui.Text(frame, "Text", approve ? "ONAYLANDI" : "REDDEDİLDİ", 30f, color, TextAlignmentOptions.Center, FontStyles.Bold);
            text.rectTransform.Stretch();
            text.characterSpacing = 4f;
            return frame;
        }

        public void OnPointerDown(PointerEventData eventData) => rect.SetAsLastSibling();

        public void OnPointerClick(PointerEventData eventData) => HandleClick(eventData, null);

        void HandleClick(PointerEventData eventData, Row row)
        {
            if (eventData.dragging || tools == null)
                return;
            switch (tools.Active)
            {
                case DeskTool.ApproveStamp:
                    ApplyStamp(StampKind.Approve, eventData.position);
                    break;
                case DeskTool.RejectStamp:
                    ApplyStamp(StampKind.Reject, eventData.position);
                    break;
                case DeskTool.RedPen:
                    row?.ToggleMark();
                    break;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(dragArea, eventData.position, null, out var local);
            dragOffset = rect.anchoredPosition - local;
            rect.localScale = Vector3.one * 1.04f;
            shadow.color = new Color(0f, 0f, 0f, 0.45f);
        }

        public void OnDrag(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(dragArea, eventData.position, null, out var local);
            var half = dragArea.rect.size * 0.5f;
            var target = local + dragOffset;
            target.x = Mathf.Clamp(target.x, -half.x, half.x);
            target.y = Mathf.Clamp(target.y, -half.y, half.y);
            rect.anchoredPosition = target;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            rect.localScale = Vector3.one;
            shadow.color = new Color(0f, 0f, 0f, 0.35f);
            Dropped?.Invoke(this);
        }

        /// <summary>Kırmızı kalemle işaretlenebilen bir belge satırı.</summary>
        class Row
        {
            readonly Image mark;
            public DocumentField Field { get; }
            public bool Marked => mark.gameObject.activeSelf;

            public Row(DocumentCard card, RectTransform parent, DocumentField field)
            {
                Field = field;
                var bg = Ui.Image(parent, field.label, new Color(0f, 0f, 0f, 0f));
                var element = bg.gameObject.AddComponent<LayoutElement>();
                element.minHeight = 34f;

                var label = Ui.Text(bg.transform, "Label", field.label, 17f, Faded);
                label.rectTransform.Stretch(0f, 0f, 0f, 0f);
                label.rectTransform.anchorMax = new Vector2(0.48f, 1f);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                var value = Ui.Text(bg.transform, "Value", field.value, 19f, Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                value.rectTransform.Stretch();
                value.rectTransform.anchorMin = new Vector2(0.5f, 0f);
                element.preferredHeight = Mathf.Max(34f, value.GetPreferredValues(field.value, 175f, 0f).y + 10f);

                var line = Ui.Image(bg.transform, "Line", new Color(0f, 0f, 0f, 0.08f), raycast: false).rectTransform;
                line.anchorMin = Vector2.zero;
                line.anchorMax = new Vector2(1f, 0f);
                line.sizeDelta = new Vector2(0f, 1.5f);

                mark = Ui.Image(bg.transform, "RedMark", new Color(0.85f, 0.1f, 0.1f, 0.9f), raycast: false);
                mark.type = Image.Type.Simple;
                mark.sprite = Ui.Ellipse;
                mark.rectTransform.Stretch(-8f, -4f, -8f, -4f);
                mark.rectTransform.anchorMin = new Vector2(0.47f, 0f);
                mark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-4f, 4f));
                mark.gameObject.SetActive(false);

                bg.gameObject.AddComponent<RowClick>().Init(card, this);
            }

            public void ToggleMark() => mark.gameObject.SetActive(!mark.gameObject.activeSelf);

            public void Click(DocumentCard card, PointerEventData e) => card.HandleClick(e, this);
        }

        class RowClick : MonoBehaviour, IPointerClickHandler
        {
            DocumentCard card;
            Row row;

            public void Init(DocumentCard owner, Row target)
            {
                card = owner;
                row = target;
            }

            public void OnPointerClick(PointerEventData eventData) => row.Click(card, eventData);
        }
    }

    /// <summary>Damga basılınca kısa bir "küt" efekti.</summary>
    public class StampThump : MonoBehaviour
    {
        float t;

        void Update()
        {
            t += Time.unscaledDeltaTime * 6f;
            transform.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, Mathf.SmoothStep(0f, 1f, t));
            if (t >= 1f)
                Destroy(this);
        }
    }
}
