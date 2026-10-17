using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Akşam: masada günün hesabı, aile fotoğrafı ve alışveriş yapılan telefon.
    /// Eve gidilmez; "Sabah oldu"ya basınca sıradaki gün başlar.
    /// </summary>
    public class EveningPanel : MonoBehaviour
    {
        static readonly Color Dim = new Color(0.03f, 0.03f, 0.06f, 0.88f);
        static readonly Color Paper = new Color(0.97f, 0.95f, 0.89f);
        static readonly Color Ink = new Color(0.15f, 0.14f, 0.13f);
        static readonly Color PhoneBody = new Color(0.08f, 0.08f, 0.1f);
        static readonly Color Screen = new Color(0.16f, 0.2f, 0.27f);

        class ItemRow
        {
            public Button button;
            public TextMeshProUGUI label;
        }

        RectTransform root;
        TextMeshProUGUI ledgerText;
        TextMeshProUGUI phoneHeader;
        TextMeshProUGUI phoneFooter;
        readonly Dictionary<ShopItem, ItemRow> rows = new Dictionary<ShopItem, ItemRow>();
        bool shopOpen;
        bool done;

        public event System.Action<ShopItem> BuyClicked;

        public void Build(RectTransform canvas)
        {
            root = Ui.Image(canvas, "Evening", Dim).rectTransform.Stretch();

            var ledger = Ui.Image(root, "Ledger", Paper, Ui.Rounded).rectTransform;
            ledger.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-420f, 120f), new Vector2(520f, 420f));
            Ui.DropShadow(ledger, 16f, new Vector2(4f, -6f), 0.4f);
            ledgerText = Ui.Text(ledger, "Text", "", 26f, Ink);
            ledgerText.rectTransform.Stretch(36f, 30f, 36f, 30f);
            ledgerText.richText = true;

            var photo = Ui.Image(root, "FamilyPhoto", new Color(0.55f, 0.45f, 0.3f), Ui.Rounded).rectTransform;
            photo.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-420f, -250f), new Vector2(300f, 220f));
            photo.localRotation = Quaternion.Euler(0f, 0f, 4f);
            Ui.DropShadow(photo, 12f, new Vector2(3f, -5f), 0.4f);
            var picture = Ui.Image(photo, "Picture", new Color(0.82f, 0.74f, 0.6f), raycast: false).rectTransform.Stretch(16f, 40f, 16f, 16f);
            Ui.Text(picture, "Caption", "Aile", 30f, new Color(0.4f, 0.32f, 0.22f), TextAlignmentOptions.Center, FontStyles.Italic)
                .rectTransform.Stretch();

            var phone = Ui.Image(root, "Phone", PhoneBody, Ui.Rounded).rectTransform;
            phone.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(330f, 0f), new Vector2(460f, 860f));
            Ui.DropShadow(phone, 18f, new Vector2(4f, -8f), 0.5f);
            var screen = Ui.Image(phone, "Screen", Screen, Ui.Rounded).rectTransform.Stretch(20f, 60f, 20f, 60f);

            phoneHeader = Ui.Text(screen, "Header", "", 26f, Color.white, TextAlignmentOptions.Top, FontStyles.Bold);
            phoneHeader.rectTransform.Stretch(16f, 0f, 16f, 22f);
            phoneHeader.richText = true;

            var list = Ui.Rect("Items", screen).Stretch(18f, 200f, 18f, 120f);
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            foreach (var item in Shop.Items)
            {
                var button = Ui.Button(list, item.ToString(), "", new Color(0.24f, 0.32f, 0.45f), Color.white, 22f);
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = 120f;
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.richText = true;
                var captured = item;
                button.onClick.AddListener(() => BuyClicked?.Invoke(captured));
                rows[item] = new ItemRow { button = button, label = label };
            }

            phoneFooter = Ui.Text(screen, "Footer", "", 18f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Bottom, FontStyles.Italic);
            phoneFooter.rectTransform.Stretch(16f, 110f, 16f, 0f);
            phoneFooter.rectTransform.anchorMax = new Vector2(1f, 0f);
            phoneFooter.rectTransform.offsetMax = new Vector2(-16f, 190f);

            var morning = Ui.Button(screen, "Morning", "SABAH OLDU", new Color(0.85f, 0.6f, 0.2f), Color.white, 24f);
            ((RectTransform)morning.transform).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(300f, 72f));
            morning.onClick.AddListener(() => done = true);

            root.gameObject.SetActive(false);
        }

        public IEnumerator Run(int day, int income, int rent, int money, ICollection<ShopItem> purchased, bool shopOpen)
        {
            this.shopOpen = shopOpen;
            ledgerText.text =
                $"<size=36><b>{day}. gün akşamı</b></size>\n\n" +
                $"Bugünkü mühürler   <b>+{income:N0} TL</b>\n" +
                $"Kira   <b>-{rent:N0} TL</b>\n" +
                (rent < Economy.Rent ? "<size=80%><i>Kiranın tamamı ödenemedi.</i></size>\n" : "") +
                "\n<size=80%>Eve yürünmez. Alışveriş telefondan.</size>";
            phoneFooter.text = !shopOpen ? "Dükkân kapandı."
                : day == Economy.LastPaidDay ? "Dükkân bu akşam son kez açık." : "";
            Refresh(money, purchased);

            done = false;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            while (!done)
                yield return null;
            root.gameObject.SetActive(false);
        }

        public void Refresh(int money, ICollection<ShopItem> purchased)
        {
            phoneHeader.text = $"DEPREM DÜKKÂNI\n<size=80%><color=#fad166>Kasa: {money:N0} TL</color></size>";
            foreach (var pair in rows)
            {
                int price = Shop.Price(pair.Key);
                bool owned = purchased.Contains(pair.Key);
                string state = owned ? "<color=#6fdc8c>Alındı</color>" : $"{price:N0} TL";
                pair.Value.label.text = $"{Shop.Name(pair.Key)}\n<size=85%>{state}</size>";
                pair.Value.button.interactable = shopOpen && !owned && money >= price;
            }
        }
    }
}
