using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Sol paneldeki sekmeli yönetmelik kitabı. Sayfalar günü gelince açılır. Konusu olan sayfanın sekmesinde ve
    /// başlığında konu rengi ve rozeti var; sayfanın ya da sekmenin üstüne gelince belgede aynı konudaki satırlar,
    /// belgede bir satırın üstüne gelince de o konunun sekmesi parlar.
    /// </summary>
    public class RegulationBook : MonoBehaviour
    {
        static readonly Color Cover = new Color(0.18f, 0.24f, 0.33f);
        static readonly Color PageColor = new Color(0.97f, 0.95f, 0.89f);
        static readonly Color TabColor = new Color(0.82f, 0.78f, 0.68f);
        static readonly Color TabSelected = new Color(0.97f, 0.95f, 0.89f);
        static readonly Color Ink = new Color(0.15f, 0.14f, 0.13f);

        RegulationData data;
        RectTransform tabRow;
        readonly List<int> visible = new List<int>();
        TextMeshProUGUI pageTitle;
        TextMeshProUGUI pageBody;
        TextMeshProUGUI pageNumber;
        readonly List<Image> tabs = new List<Image>();
        readonly List<RuleTopic> tabTopics = new List<RuleTopic>();
        RectTransform titleBadge;
        int shownSlot;

        public void Build(RectTransform parent, RegulationData regulations)
        {
            data = regulations;
            // Üstte kasa göstergesi, altta "Belgeleri Kapat" butonu var; kitap aralarına sığsın.
            var book = Ui.Image(parent, "Book", Cover, Ui.Rounded).rectTransform.Stretch(28f, 104f, 28f, 92f);
            Ui.DropShadow(book, 18f, new Vector2(0f, -6f), 0.4f);

            var header = Ui.Text(book, "BookTitle", data.bookTitle, 24f, new Color(0.95f, 0.85f, 0.55f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            header.rectTransform.Stretch(26f, 0f, 26f, 0f);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.offsetMin = new Vector2(26f, -64f);
            header.rectTransform.offsetMax = new Vector2(-26f, -8f);

            tabRow = Ui.Rect("Tabs", book);
            tabRow.anchorMin = new Vector2(0f, 1f);
            tabRow.anchorMax = new Vector2(1f, 1f);
            tabRow.pivot = new Vector2(0.5f, 1f);
            tabRow.offsetMin = new Vector2(20f, -112f);
            tabRow.offsetMax = new Vector2(-20f, -64f);
            var tabLayout = tabRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 6f;
            tabLayout.childControlWidth = true;
            tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = true;

            var page = Ui.Image(book, "Page", PageColor, Ui.Rounded).rectTransform.Stretch(20f, 20f, 20f, 112f);
            pageTitle = Ui.Text(page, "Title", "", 30f, Ink, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            pageTitle.rectTransform.Stretch(30f, 0f, 30f, 24f);
            pageTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            pageTitle.rectTransform.offsetMin = new Vector2(30f, -84f);
            pageBody = Ui.Text(page, "Body", "", 22f, Ink);
            pageBody.rectTransform.Stretch(30f, 50f, 30f, 92f);
            pageBody.lineSpacing = 12f;
            pageBody.enableAutoSizing = true;
            pageBody.fontSizeMin = 15f;
            pageBody.fontSizeMax = 22f;
            pageNumber = Ui.Text(page, "Number", "", 16f, new Color(0.5f, 0.47f, 0.42f), TextAlignmentOptions.Bottom);
            pageNumber.rectTransform.Stretch(0f, 14f, 0f, 0f);
            titleBadge = Ui.Rect("TitleBadge", page);
            titleBadge.anchorMin = titleBadge.anchorMax = new Vector2(1f, 1f);
            titleBadge.anchoredPosition = new Vector2(-46f, -44f);
            titleBadge.sizeDelta = new Vector2(34f, 34f);

            // Sayfanın üstüne gelince belgede aynı konudaki satırlar parlar.
            var trigger = page.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => HighlightShown(true));
            AddTrigger(trigger, EventTriggerType.PointerExit, () => HighlightShown(false));
            RuleTopics.Highlighted += OnTopicHighlighted;

            SetDay(1);
        }

        void OnDestroy() => RuleTopics.Highlighted -= OnTopicHighlighted;

        /// <summary>Bir konunun sekmesi (kitapta henüz açılmadıysa null).</summary>
        public RectTransform TabFor(RuleTopic topic)
        {
            int i = tabTopics.IndexOf(topic);
            return i >= 0 && tabs[i] != null ? tabs[i].rectTransform : null;
        }

        static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        void HighlightShown(bool on)
        {
            var topic = shownSlot < tabTopics.Count ? tabTopics[shownSlot] : RuleTopic.None;
            if (topic != RuleTopic.None)
                RuleTopics.Highlight(on ? topic : RuleTopic.None);
        }

        /// <summary>Belgede bir satırın üstüne gelindi: o konunun sekmesi konu rengine boyanır.</summary>
        void OnTopicHighlighted(RuleTopic topic)
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                if (tabs[i] == null)
                    continue;
                bool glow = topic != RuleTopic.None && tabTopics[i] == topic;
                tabs[i].color = glow ? Color.Lerp(RuleTopics.Color(topic), Color.white, 0.35f) : i == shownSlot ? TabSelected : TabColor;
            }
        }

        /// <summary>O güne kadar açılmış sayfaları gösterir ve en yeni sayfayı açar.</summary>
        public void SetDay(int day)
        {
            foreach (var tab in tabs)
                Destroy(tab.gameObject);
            tabs.Clear();
            tabTopics.Clear();
            visible.Clear();
            for (int i = 0; i < data.pages.Count; i++)
            {
                if (data.pages[i].day > day)
                    continue;
                int slot = visible.Count;
                visible.Add(i);
                var topic = data.pages[i].topic;
                var tab = Ui.Button(tabRow, "Tab" + i, data.pages[i].title, TabColor, Ink, 16f);
                tab.onClick.AddListener(() => ShowPage(slot));
                if (topic != RuleTopic.None)
                {
                    // Sekmenin altında konu rengi şerit: belgedeki aynı renkli satırlarla eşleşir.
                    var strip = Ui.Image(tab.transform, "TopicStrip", RuleTopics.Color(topic), raycast: false).rectTransform;
                    strip.anchorMin = new Vector2(0.1f, 0f);
                    strip.anchorMax = new Vector2(0.9f, 0f);
                    strip.pivot = new Vector2(0.5f, 0f);
                    strip.anchoredPosition = new Vector2(0f, 3f);
                    strip.sizeDelta = new Vector2(0f, 5f);
                    var tabTrigger = tab.gameObject.AddComponent<EventTrigger>();
                    AddTrigger(tabTrigger, EventTriggerType.PointerEnter, () => RuleTopics.Highlight(topic));
                    AddTrigger(tabTrigger, EventTriggerType.PointerExit, () => RuleTopics.Highlight(RuleTopic.None));
                }
                tabs.Add(tab.GetComponent<Image>());
                tabTopics.Add(topic);
            }
            RuleTopics.SetActive(visible.Select(i => data.pages[i].topic));
            ShowPage(visible.Count - 1);
        }

        public void ShowPage(int slot)
        {
            if (visible.Count == 0)
                return;
            slot = Mathf.Clamp(slot, 0, visible.Count - 1);
            var page = data.pages[visible[slot]];
            pageTitle.text = page.title;
            pageBody.text = string.IsNullOrEmpty(page.why)
                ? page.body
                : $"{page.body}\n\n<size=85%><i><color=#6b5a45>Neden? {page.why}</color></i></size>";
            pageNumber.text = $"— {slot + 1} / {visible.Count} —";
            shownSlot = slot;
            for (int i = 0; i < tabs.Count; i++)
                tabs[i].color = i == slot ? TabSelected : TabColor;
            // Başlık konu renginde, sağ üstte harfli rozet.
            pageTitle.color = page.topic != RuleTopic.None ? RuleTopics.Color(page.topic) * 0.8f + new Color(0f, 0f, 0f, 0.2f) : Ink;
            foreach (Transform child in titleBadge)
                Destroy(child.gameObject);
            if (page.topic != RuleTopic.None)
                RuleTopics.Badge(titleBadge, page.topic, 34f).anchoredPosition = Vector2.zero;
        }
    }
}
