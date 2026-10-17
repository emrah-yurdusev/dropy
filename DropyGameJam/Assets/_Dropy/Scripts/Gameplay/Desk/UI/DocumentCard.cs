using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Ekranda sürüklenebilen kağıt belge. Tıklayınca öne gelir; ana belgeye (künye)
    /// seçili mühür basılır.
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
        RectTransform content;
        RectTransform stampMark;
        System.Func<Seal, bool> stampFilter;

        public DocumentData Data { get; private set; }
        public bool IsMain { get; private set; }
        public Seal Stamp { get; private set; }
        public RectTransform RectTransform => rect;

        public event System.Action<DocumentCard> Dropped;

        /// <param name="stampFilter">Ana belgede mühür basılmadan önce sorulur; false dönerse basılmaz.</param>
        public static DocumentCard Create(DocumentData data, bool isMain, RectTransform parent, DeskTools tools,
            float height, System.Func<Seal, bool> stampFilter = null)
        {
            var image = Ui.Image(parent, data.title, Paper, Ui.Rounded);
            var card = image.gameObject.AddComponent<DocumentCard>();
            card.stampFilter = stampFilter;
            card.Build(data, isMain, parent, tools, height);
            return card;
        }

        void Build(DocumentData data, bool isMain, RectTransform area, DeskTools deskTools, float height)
        {
            Data = data;
            IsMain = isMain;
            dragArea = area;
            tools = deskTools;
            rect = (RectTransform)transform;
            rect.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, height));
            shadow = Ui.DropShadow(rect, 16f, new Vector2(4f, -6f), 0.35f);

            var header = Ui.Image(rect, "Header", HeaderColor(data.kind), Ui.Rounded, raycast: false).rectTransform;
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.offsetMin = new Vector2(10f, -70f);
            header.offsetMax = new Vector2(-10f, -10f);
            var title = Ui.Text(header, "Title", data.title.ToUpperInvariant(), 24f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            title.rectTransform.Stretch(16f, 0f, 12f, 0f);

            content = Ui.Rect("Content", rect).Stretch(22f, isMain ? 124f : 56f, 22f, 82f);
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
                AddField(field);

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
                var label = Ui.Text(box, "Label", "MÜHÜR", 14f, Faded, TextAlignmentOptions.Top, FontStyles.Bold);
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

        /// <summary>Belgeye sonradan satır ekler (ör. çekiç ölçümü).</summary>
        public void AddField(DocumentField field, bool highlight = false)
        {
            var row = Ui.Image(content, field.label, highlight ? new Color(1f, 0.85f, 0.3f, 0.35f) : new Color(0f, 0f, 0f, 0f), raycast: false);
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 34f;

            var label = Ui.Text(row.transform, "Label", field.label, 17f, Faded);
            label.rectTransform.Stretch();
            label.rectTransform.anchorMax = new Vector2(0.48f, 1f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            var value = Ui.Text(row.transform, "Value", field.value, 19f, Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            value.rectTransform.Stretch();
            value.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            element.preferredHeight = Mathf.Max(34f, value.GetPreferredValues(field.value, 175f, 0f).y + 10f);

            var line = Ui.Image(row.transform, "Line", new Color(0f, 0f, 0f, 0.08f), raycast: false).rectTransform;
            line.anchorMin = Vector2.zero;
            line.anchorMax = new Vector2(1f, 0f);
            line.sizeDelta = new Vector2(0f, 1.5f);
        }

        /// <summary>Ana belgeye mühür basar; son basılan mühür karardır.</summary>
        public void ApplyStamp(Seal seal, Vector2 screenPoint)
        {
            if (!IsMain || seal == Seal.None)
                return;
            if (stampFilter != null && !stampFilter(seal))
                return;
            if (stampMark != null)
                stampMark.GetComponent<CanvasGroup>().alpha = 0.25f;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(stampLayer, screenPoint, null, out var local);
            stampMark = CreateStampGraphic(stampLayer, seal);
            stampMark.gameObject.AddComponent<CanvasGroup>();
            stampMark.anchoredPosition = local;
            stampMark.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-14f, 14f));
            stampMark.gameObject.AddComponent<StampThump>();
            Stamp = seal;
        }

        public static RectTransform CreateStampGraphic(Transform parent, Seal seal)
        {
            Color color;
            string label;
            switch (seal)
            {
                case Seal.Approve:
                    color = new Color(0.12f, 0.55f, 0.25f, 0.85f);
                    label = "ONAYLANDI";
                    break;
                case Seal.ShearWall:
                    color = new Color(0.15f, 0.35f, 0.75f, 0.85f);
                    label = "PERDE EKLENSİN";
                    break;
                default:
                    color = new Color(0.8f, 0.12f, 0.12f, 0.85f);
                    label = "REDDEDİLDİ";
                    break;
            }
            var frame = Ui.Image(parent, "Stamp", color, Ui.Frame, raycast: false).rectTransform;
            frame.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(230f, 70f));
            var text = Ui.Text(frame, "Text", label, 28f, color, TextAlignmentOptions.Center, FontStyles.Bold);
            text.rectTransform.Stretch();
            text.characterSpacing = 3f;
            return frame;
        }

        public void OnPointerDown(PointerEventData eventData) => rect.SetAsLastSibling();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging || tools == null)
                return;
            ApplyStamp(tools.Active, eventData.position);
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
