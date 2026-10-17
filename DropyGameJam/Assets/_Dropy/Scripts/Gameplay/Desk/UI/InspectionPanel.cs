using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>
    /// "Belgeleri İncele" modu: ekran %40 / %20 / %40 bölünür.
    /// Solda yönetmelik, sağda proje künyesi, ek rapor (3. günden itibaren altında çekiç) ve mühürler;
    /// ortada karakter görünmeye devam eder. Mühürlü künye ortaya sürüklenince ya da "Teslim Et"e basınca karar verilir.
    /// </summary>
    public class InspectionPanel : MonoBehaviour
    {
        const float SideWidth = 0.4f;
        const float MainCardHeight = 600f;
        const float ReportCardHeight = 400f;
        static readonly Color PanelColor = new Color(0.36f, 0.27f, 0.2f, 0.97f);
        static readonly Color PanelEdge = new Color(0.22f, 0.16f, 0.12f, 1f);

        [SerializeField, Min(0.05f)] float slideTime = 0.35f;
        [Tooltip("İnceleme açıkken kameranın görüş açısı (karakter ortadaki şeride sığsın diye)")]
        [SerializeField] float inspectFieldOfView = 30f;

        RectTransform leftPanel;
        RectTransform rightPanel;
        RectTransform documentLayer;
        CanvasGroup documentGroup;
        Button submitButton;
        TextMeshProUGUI hintText;
        DeskTools tools;
        RegulationBook book;
        Camera deskCamera;
        float normalFieldOfView;
        float openAmount;
        Coroutine animation;
        readonly List<DocumentCard> cards = new List<DocumentCard>();

        public bool IsOpen { get; private set; }
        public DocumentCard MainCard { get; private set; }
        public DeskTools Tools => tools;
        public bool HammerUsed { get; private set; }

        /// <summary>Künyeye mühür basılmadan önce sorulur; false dönerse mühür basılmaz.</summary>
        public System.Func<Seal, bool> StampFilter;

        public event System.Action SubmitRequested;

        public void Build(RectTransform root, RegulationData regulations, Camera camera)
        {
            deskCamera = camera;
            normalFieldOfView = camera.fieldOfView;

            leftPanel = SidePanel(root, "Mevzuat", 0f);
            book = gameObject.AddComponent<RegulationBook>();
            book.Build(leftPanel, regulations);

            rightPanel = SidePanel(root, "Belgeler", 1f - SideWidth);
            tools = gameObject.AddComponent<DeskTools>();

            var center = Ui.Rect("CenterStrip", root);
            center.anchorMin = new Vector2(SideWidth, 0f);
            center.anchorMax = new Vector2(1f - SideWidth, 1f);
            center.offsetMin = center.offsetMax = Vector2.zero;
            submitButton = Ui.Button(center, "Submit", "KARARI TESLİM ET", new Color(0.85f, 0.6f, 0.2f), Color.white, 22f);
            ((RectTransform)submitButton.transform).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(300f, 64f));
            submitButton.onClick.AddListener(() => SubmitRequested?.Invoke());
            hintText = Ui.Text(center, "Hint", "", 18f, Color.white, TextAlignmentOptions.Bottom, FontStyles.Italic);
            ((RectTransform)hintText.transform).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 100f), new Vector2(340f, 80f));
            hintText.fontSizeMin = 14f;
            hintText.enableAutoSizing = true;
            hintText.fontSizeMax = 18f;
            hintText.outlineWidth = 0.2f;
            hintText.outlineColor = new Color32(0, 0, 0, 200);

            documentLayer = Ui.Rect("Documents", root).Stretch();
            documentGroup = documentLayer.gameObject.AddComponent<CanvasGroup>();
            tools.Build(rightPanel, root);

            SetOpenAmount(0f);
        }

        RectTransform SidePanel(RectTransform root, string name, float anchorX)
        {
            var panel = Ui.Image(root, name, PanelColor).rectTransform;
            panel.anchorMin = new Vector2(anchorX, 0f);
            panel.anchorMax = new Vector2(anchorX + SideWidth, 1f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            var edge = Ui.Image(panel, "Edge", PanelEdge, raycast: false).rectTransform;
            edge.anchorMin = new Vector2(anchorX < 0.5f ? 1f : 0f, 0f);
            edge.anchorMax = new Vector2(anchorX < 0.5f ? 1f : 0f, 1f);
            edge.sizeDelta = new Vector2(8f, 0f);
            return panel;
        }

        /// <summary>Yönetmelik kitabını o güne kadar açılmış sayfalarla günceller.</summary>
        public void SetDay(int day) => book.SetDay(day);

        /// <summary>Dosyanın künyesini ve ek raporunu sağ panelin üstüne koyar.</summary>
        public void LoadCase(BuildingCase buildingCase, bool hammerAvailable)
        {
            ClearCase();
            float rightCenter = documentLayer.rect.width * (0.5f - SideWidth / 2f);

            var report = AddCard(CaseDocuments.Report(buildingCase), false, ReportCardHeight, new Vector2(rightCenter + 175f, 110f), -2f);
            if (hammerAvailable)
                AddHammer(report, buildingCase);
            MainCard = AddCard(CaseDocuments.Kunye(buildingCase), true, MainCardHeight, new Vector2(rightCenter - 165f, 40f), 1.5f);
            Sfx.Paper();
        }

        DocumentCard AddCard(DocumentData data, bool isMain, float height, Vector2 position, float angle)
        {
            var card = DocumentCard.Create(data, isMain, documentLayer, tools, height, isMain ? StampFilter : null);
            card.RectTransform.anchoredPosition = position;
            card.RectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            card.Dropped += OnCardDropped;
            cards.Add(card);
            return card;
        }

        void AddHammer(DocumentCard report, BuildingCase buildingCase)
        {
            var hammer = Ui.Button(report.RectTransform, "Hammer", "ÇEKİÇLE ÖLÇ", new Color(0.55f, 0.4f, 0.25f), Color.white, 20f);
            ((RectTransform)hammer.transform).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(260f, 56f));
            hammer.onClick.AddListener(() =>
            {
                HammerUsed = true;
                hammer.interactable = false;
                report.AddField(new DocumentField
                {
                    label = "Çekiç ölçümü",
                    value = BuildingCase.Concrete(buildingCase.TrueConcrete)
                }, highlight: true);
            });
        }

        public void ClearCase()
        {
            foreach (var card in cards)
                if (card != null)
                {
                    Destroy(card.Data);
                    Destroy(card.gameObject);
                }
            cards.Clear();
            MainCard = null;
            HammerUsed = false;
            tools?.Select(Seal.None);
        }

        void OnCardDropped(DocumentCard card)
        {
            card.RectTransform.localRotation = Quaternion.identity;
            if (card != MainCard || card.Stamp == Seal.None)
                return;
            // Ortadaki şeride (karaktere) bırakılan damgalı ana belge = teslim.
            float x = card.RectTransform.anchoredPosition.x / documentLayer.rect.width + 0.5f;
            if (x > SideWidth && x < 1f - SideWidth)
                SubmitRequested?.Invoke();
        }

        void Update()
        {
            if (!IsOpen)
                return;
            bool ready = MainCard != null && MainCard.Stamp != Seal.None;
            submitButton.interactable = ready;
            hintText.text = ready
                ? "Mühürlü künyeyi müteahhide sürükle ya da teslim et."
                : "Künyeye bir mühür bas:\nONAY, RED ya da PERDE EKLE.";
        }

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            if (IsOpen == open)
                return;
            IsOpen = open;
            if (!open)
                tools.Select(Seal.None);
            if (animation != null)
                StopCoroutine(animation);
            animation = StartCoroutine(Animate(open ? 1f : 0f));
        }

        IEnumerator Animate(float target)
        {
            float start = openAmount;
            for (float t = 0f; t < slideTime; t += Time.unscaledDeltaTime)
            {
                SetOpenAmount(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / slideTime)));
                yield return null;
            }
            SetOpenAmount(target);
            animation = null;
        }

        void SetOpenAmount(float amount)
        {
            openAmount = amount;
            float hidden = 1f - amount;
            leftPanel.anchorMin = new Vector2(-SideWidth * hidden, 0f);
            leftPanel.anchorMax = new Vector2(SideWidth - SideWidth * hidden, 1f);
            rightPanel.anchorMin = new Vector2(1f - SideWidth + SideWidth * hidden, 0f);
            rightPanel.anchorMax = new Vector2(1f + SideWidth * hidden, 1f);
            leftPanel.offsetMin = leftPanel.offsetMax = rightPanel.offsetMin = rightPanel.offsetMax = Vector2.zero;

            documentLayer.anchoredPosition = new Vector2(documentLayer.rect.width * SideWidth * hidden, 0f);
            documentGroup.alpha = amount;
            documentGroup.blocksRaycasts = amount > 0.99f;
            bool visible = amount > 0.001f;
            leftPanel.gameObject.SetActive(visible);
            rightPanel.gameObject.SetActive(visible);
            submitButton.transform.parent.gameObject.SetActive(amount > 0.99f);
            if (deskCamera != null)
                deskCamera.fieldOfView = Mathf.Lerp(normalFieldOfView, inspectFieldOfView, amount);
        }
    }
}
