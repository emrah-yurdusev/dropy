using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>Sol paneldeki sekmeli mevzuat kitapçığı.</summary>
    public class RegulationBook : MonoBehaviour
    {
        static readonly Color Cover = new Color(0.18f, 0.24f, 0.33f);
        static readonly Color PageColor = new Color(0.97f, 0.95f, 0.89f);
        static readonly Color TabColor = new Color(0.82f, 0.78f, 0.68f);
        static readonly Color TabSelected = new Color(0.97f, 0.95f, 0.89f);
        static readonly Color Ink = new Color(0.15f, 0.14f, 0.13f);

        RegulationData data;
        TextMeshProUGUI pageTitle;
        TextMeshProUGUI pageBody;
        TextMeshProUGUI pageNumber;
        readonly List<Image> tabs = new List<Image>();

        public void Build(RectTransform parent, RegulationData regulations)
        {
            data = regulations;
            // Üstte kasa göstergesi, altta "Belgeleri Kapat" butonu var; kitap aralarına sığsın.
            var book = Ui.Image(parent, "Book", Cover, Ui.Rounded).rectTransform.Stretch(28f, 104f, 28f, 92f);
            Ui.DropShadow(book, 18f, new Vector2(0f, -6f), 0.4f);

            var header = Ui.Text(book, "BookTitle", "MEVZUAT  ·  " + data.bookTitle, 24f, new Color(0.95f, 0.85f, 0.55f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            header.rectTransform.Stretch(26f, 0f, 26f, 0f);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.offsetMin = new Vector2(26f, -64f);
            header.rectTransform.offsetMax = new Vector2(-26f, -8f);

            var tabRow = Ui.Rect("Tabs", book);
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

            for (int i = 0; i < data.pages.Count; i++)
            {
                int index = i;
                var tab = Ui.Button(tabRow, "Tab" + i, data.pages[i].title, TabColor, Ink, 16f);
                tab.onClick.AddListener(() => ShowPage(index));
                tabs.Add((Image)tab.targetGraphic);
            }

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

            ShowPage(0);
        }

        public void ShowPage(int index)
        {
            if (data.pages.Count == 0)
                return;
            index = Mathf.Clamp(index, 0, data.pages.Count - 1);
            pageTitle.text = data.pages[index].title;
            pageBody.text = data.pages[index].body;
            pageNumber.text = $"— {index + 1} / {data.pages.Count} —";
            for (int i = 0; i < tabs.Count; i++)
                tabs[i].color = i == index ? TabSelected : TabColor;
        }
    }
}
