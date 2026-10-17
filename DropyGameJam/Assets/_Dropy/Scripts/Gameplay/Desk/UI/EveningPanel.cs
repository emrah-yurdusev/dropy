using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>Akşam telefonunda tek bir mesaj. İstek ise balonun içinde "Öde" düğmesi çıkar.</summary>
    public struct EveningMessage
    {
        public string sender;
        public string text;
        public FamilyNeed request;
    }

    /// <summary>
    /// Akşam: masada günün hesabı, aile fotoğrafı ve aile bağı, bir de telefon. Telefonda o akşamın
    /// mesajları (aileden gelen istekler Öde düğmesiyle) ve deprem dükkânı var; ikisi aynı kasadan ödenir.
    /// Eve gidilmez; "Sabah oldu"ya basınca sıradaki gün başlar.
    /// </summary>
    public class EveningPanel : MonoBehaviour
    {
        static readonly Color Dim = new Color(0.03f, 0.03f, 0.06f, 0.88f);
        static readonly Color Paper = new Color(0.97f, 0.95f, 0.89f);
        static readonly Color Ink = new Color(0.15f, 0.14f, 0.13f);
        static readonly Color PhoneBody = new Color(0.08f, 0.08f, 0.1f);
        static readonly Color Screen = new Color(0.16f, 0.2f, 0.27f);
        static readonly Color Bubble = new Color(0.26f, 0.32f, 0.42f);
        static readonly Color RequestBubble = new Color(0.4f, 0.3f, 0.4f);
        static readonly Color PayColor = new Color(0.85f, 0.6f, 0.2f);
        static readonly Color Owned = new Color(0.36f, 0.7f, 0.45f);
        static readonly Color BondOn = new Color(0.95f, 0.58f, 0.45f);
        static readonly Color BondOff = new Color(0.25f, 0.24f, 0.26f);
        const float RowHeight = 56f;

        class ItemRow
        {
            public Button button;
            public TextMeshProUGUI label;
            public GameObject chip;
        }

        RectTransform root;
        TextMeshProUGUI ledgerText;
        TextMeshProUGUI phoneHeader;
        TextMeshProUGUI phoneFooter;
        RectTransform messageList;
        RectTransform bondRow;
        TextMeshProUGUI bondLabel;
        readonly List<Image> bondSegments = new List<Image>();
        GameObject picture;
        GameObject caption;
        GameObject photoBack;
        Image photoFade;
        readonly Dictionary<ShopItem, ItemRow> rows = new Dictionary<ShopItem, ItemRow>();
        readonly Dictionary<FamilyNeed, ItemRow> requestRows = new Dictionary<FamilyNeed, ItemRow>();
        bool shopOpen;
        bool done;

        public event System.Action<ShopItem> BuyClicked;
        public event System.Action<FamilyNeed> PayClicked;

        public void Build(RectTransform canvas)
        {
            root = Ui.Image(canvas, "Evening", Dim).rectTransform.Stretch();

            var ledger = Ui.Image(root, "Ledger", Paper, Ui.Rounded).rectTransform;
            ledger.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-440f, 200f), new Vector2(540f, 340f));
            Ui.DropShadow(ledger, 16f, new Vector2(4f, -6f), 0.4f);
            ledgerText = Ui.Text(ledger, "Text", "", 24f, Ink);
            ledgerText.rectTransform.Stretch(36f, 22f, 36f, 24f);
            ledgerText.richText = true;

            BuildPhoto(root);
            BuildBond(root);

            var chips = Ui.Rect("Purchased", root);
            chips.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(-440f, -440f), new Vector2(540f, 56f));
            var chipLayout = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
            chipLayout.spacing = 10f;
            chipLayout.childAlignment = TextAnchor.MiddleCenter;
            chipLayout.childControlWidth = true;
            chipLayout.childControlHeight = true;
            chipLayout.childForceExpandWidth = false;

            var phone = Ui.Image(root, "Phone", PhoneBody, Ui.Rounded).rectTransform;
            phone.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(330f, 0f), new Vector2(500f, 980f));
            Ui.DropShadow(phone, 18f, new Vector2(4f, -8f), 0.5f);
            var screen = Ui.Image(phone, "Screen", Screen, Ui.Rounded).rectTransform.Stretch(18f, 40f, 18f, 40f);

            phoneHeader = Ui.Text(screen, "Header", "", 24f, Color.white, TextAlignmentOptions.Top, FontStyles.Bold);
            phoneHeader.rectTransform.Stretch(16f, 0f, 16f, 16f);
            phoneHeader.richText = true;

            messageList = Ui.Rect("Messages", screen);
            messageList.anchorMin = new Vector2(0f, 1f);
            messageList.anchorMax = new Vector2(1f, 1f);
            messageList.pivot = new Vector2(0.5f, 1f);
            messageList.offsetMin = new Vector2(16f, -470f);
            messageList.offsetMax = new Vector2(-16f, -76f);
            var messageLayout = messageList.gameObject.AddComponent<VerticalLayoutGroup>();
            messageLayout.spacing = 8f;
            messageLayout.childControlHeight = true;
            messageLayout.childControlWidth = true;
            messageLayout.childForceExpandHeight = false;

            var list = Ui.Rect("Shop", screen).Stretch(16f, 186f, 16f, 480f);
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            var shopTitle = Ui.Text(list, "Title", "DEPREM DÜKKÂNI", 18f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Left, FontStyles.Bold);
            shopTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;
            foreach (var item in Shop.Items)
            {
                var button = Ui.Button(list, item.ToString(), "", new Color(0.24f, 0.32f, 0.45f), Color.white, 20f);
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.richText = true;
                var captured = item;
                button.onClick.AddListener(() => BuyClicked?.Invoke(captured));
                var chip = Ui.Image(chips, item + "Chip", Owned, Ui.Rounded).gameObject;
                var chipText = Ui.Text(chip.transform, "Text", Shop.Name(item), 18f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                chipText.rectTransform.Stretch(14f, 0f, 14f, 0f);
                chip.AddComponent<LayoutElement>().preferredWidth = chipText.GetPreferredValues().x + 28f;
                rows[item] = new ItemRow { button = button, label = label, chip = chip };
            }

            phoneFooter = Ui.Text(screen, "Footer", "", 18f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Center, FontStyles.Italic);
            phoneFooter.rectTransform.anchorMin = Vector2.zero;
            phoneFooter.rectTransform.anchorMax = new Vector2(1f, 0f);
            phoneFooter.rectTransform.offsetMin = new Vector2(16f, 112f);
            phoneFooter.rectTransform.offsetMax = new Vector2(-16f, 176f);

            var morning = Ui.Button(screen, "Morning", "SABAH OLDU", PayColor, Color.white, 24f);
            ((RectTransform)morning.transform).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(300f, 72f));
            morning.onClick.AddListener(() => done = true);

            root.gameObject.SetActive(false);
        }

        /// <summary>Masadaki aile fotoğrafı: iki yetişkin, bir çocuk, arkada ev. Aile bağı düştükçe solar, sıfırda ters kapanır.</summary>
        void BuildPhoto(RectTransform parent)
        {
            var frame = Ui.Image(parent, "FamilyPhoto", new Color(0.96f, 0.94f, 0.9f), Ui.Rounded).rectTransform;
            frame.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-440f, -190f), new Vector2(340f, 300f));
            frame.localRotation = Quaternion.Euler(0f, 0f, 3f);
            Ui.DropShadow(frame, 12f, new Vector2(3f, -5f), 0.4f);

            var pic = Ui.Image(frame, "Picture", new Color(0.62f, 0.78f, 0.9f), raycast: false).rectTransform.Stretch(16f, 56f, 16f, 16f);
            picture = pic.gameObject;
            var ground = Ui.Image(pic, "Ground", new Color(0.5f, 0.68f, 0.42f), raycast: false).rectTransform;
            ground.anchorMin = Vector2.zero;
            ground.anchorMax = new Vector2(1f, 0.3f);
            ground.offsetMin = ground.offsetMax = Vector2.zero;
            var house = Ui.Image(pic, "House", new Color(0.86f, 0.74f, 0.58f), raycast: false).rectTransform;
            house.Place(new Vector2(0.78f, 0.3f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(80f, 110f));
            for (int w = 0; w < 4; w++)
            {
                var window = Ui.Image(house, "Window", new Color(0.4f, 0.55f, 0.7f), raycast: false).rectTransform;
                window.Place(new Vector2(w % 2 == 0 ? 0.3f : 0.7f, w < 2 ? 0.75f : 0.4f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
            }

            Person(pic, 0.2f, 1f, new Color(0.32f, 0.42f, 0.6f));
            Person(pic, 0.38f, 0.93f, new Color(0.72f, 0.35f, 0.38f));
            Person(pic, 0.53f, 0.62f, new Color(0.95f, 0.7f, 0.25f));
            photoFade = Ui.Image(pic, "Fade", new Color(0.55f, 0.53f, 0.5f, 0f), raycast: false);
            photoFade.rectTransform.Stretch();

            var cap = Ui.Text(frame, "Caption", "Biz, geçen bayram", 20f, new Color(0.35f, 0.3f, 0.25f), TextAlignmentOptions.Center, FontStyles.Italic);
            cap.rectTransform.anchorMin = Vector2.zero;
            cap.rectTransform.anchorMax = new Vector2(1f, 0f);
            cap.rectTransform.offsetMin = new Vector2(10f, 8f);
            cap.rectTransform.offsetMax = new Vector2(-10f, 52f);
            caption = cap.gameObject;

            // Fotoğrafın arkası: aile küsünce masaya ters kapanır.
            var back = Ui.Image(frame, "Back", new Color(0.62f, 0.52f, 0.4f), Ui.Rounded, raycast: false);
            back.rectTransform.Stretch();
            Ui.Text(back.transform, "Note", "Elif, 7 yaşında", 18f, new Color(0.35f, 0.28f, 0.2f), TextAlignmentOptions.BottomRight, FontStyles.Italic)
                .rectTransform.Stretch(20f, 16f, 20f, 16f);
            photoBack = back.gameObject;
            photoBack.SetActive(false);
        }

        void BuildBond(RectTransform parent)
        {
            bondRow = Ui.Rect("Bond", parent);
            bondRow.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(-440f, -360f), new Vector2(420f, 64f));
            bondLabel = Ui.Text(bondRow, "Label", "", 20f, Color.white, TextAlignmentOptions.Top, FontStyles.Bold);
            bondLabel.rectTransform.Stretch(0f, 30f, 0f, 0f);
            bondLabel.richText = true;
            var bar = Ui.Rect("Segments", bondRow).Stretch(40f, 0f, 40f, 36f);
            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
        }

        static void Person(RectTransform picture, float x, float scale, Color shirt)
        {
            var body = Ui.Image(picture, "Body", shirt, Ui.Rounded, raycast: false).rectTransform;
            body.Place(new Vector2(x, 0.18f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(46f, 90f) * scale);
            var head = Ui.Image(picture, "Head", new Color(0.93f, 0.78f, 0.64f), Ui.Rounded, raycast: false).rectTransform;
            head.Place(new Vector2(x, 0.18f), new Vector2(0.5f, 0f), new Vector2(0f, 88f * scale), new Vector2(36f, 36f) * scale);
        }

        /// <param name="bondChange">Dünden bu yana aile bağı farkı (ödenmeyen istekler).</param>
        public IEnumerator Run(int day, int income, int fine, int rent, int money, ICollection<ShopItem> purchased,
            IList<EveningMessage> messages, ICollection<FamilyNeed> paidNeeds,
            int bond, int bondMax, int bondChange, bool shopOpen)
        {
            this.shopOpen = shopOpen;
            // Önce aç: TextMeshPro kapalı nesnede yazı boyutunu ölçemiyor (mesaj balonları ölçülüyor).
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            string bondNote = bond <= 0 ? "\n<color=#a3221b>Ailen seninle konuşmuyor.</color>"
                : bondChange < 0 ? $"\n<size=80%><color=#a3221b>Dün karşılanmayan istek: aile bağı {bondChange}</color></size>" : "";
            ledgerText.text =
                $"<size=34><b>{day}. gün akşamı</b></size>\n\n" +
                $"Bugünkü mühürler   <b>+{Ui.Money(income)}</b>\n" +
                (fine > 0 ? $"<color=#a3221b>Tutanak cezası   <b>-{Ui.Money(fine)}</b></color>\n" : "") +
                $"Kira   <b>-{Ui.Money(rent)}</b>" +
                (rent < Economy.Rent ? "\n<size=80%><i>Kiranın tamamı ödenemedi.</i></size>" : "") +
                bondNote +
                "\n\n<size=80%>Eve yürünmez. Alışveriş telefondan.</size>";
            phoneFooter.text = !shopOpen ? "Dükkân kapandı."
                : day == Economy.LastPaidDay ? "Dükkân bu akşam son kez açık." : "";
            ShowMessages(messages);
            Refresh(money, purchased, paidNeeds, bond, bondMax);

            done = false;
            while (!done)
                yield return null;
            root.gameObject.SetActive(false);
        }

        void ShowMessages(IList<EveningMessage> messages)
        {
            foreach (Transform child in messageList)
                Destroy(child.gameObject);
            requestRows.Clear();
            if (messages.Count == 0)
            {
                var empty = Ui.Text(messageList, "Empty", "Yeni mesaj yok.", 18f, new Color(1f, 1f, 1f, 0.5f), TextAlignmentOptions.Center, FontStyles.Italic);
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
                return;
            }
            foreach (var message in messages)
            {
                bool request = message.request != null;
                var bubble = Ui.Image(messageList, request ? "Request" : "Message", request ? RequestBubble : Bubble, Ui.Rounded);
                string body = $"<b>{message.sender}</b>\n{message.text}";
                var text = Ui.Text(bubble.transform, "Text", body, 18f, Color.white);
                text.richText = true;
                float buttonRow = request ? 50f : 0f;
                text.rectTransform.Stretch(14f, 8f + buttonRow, 14f, 8f);
                bubble.gameObject.AddComponent<LayoutElement>().preferredHeight = text.GetPreferredValues(body, 400f, 0f).y + 18f + buttonRow;
                if (!request)
                    continue;

                var pay = Ui.Button(bubble.transform, "Pay", "", PayColor, Color.white, 18f);
                ((RectTransform)pay.transform).Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 10f), new Vector2(190f, 42f));
                var label = pay.GetComponentInChildren<TextMeshProUGUI>();
                label.richText = true;
                var need = message.request;
                pay.onClick.AddListener(() => PayClicked?.Invoke(need));
                requestRows[need] = new ItemRow { button = pay, label = label };
            }
        }

        public void Refresh(int money, ICollection<ShopItem> purchased, ICollection<FamilyNeed> paidNeeds, int bond, int bondMax)
        {
            phoneHeader.text = $"Mesajlar  ·  <color=#fad166>Kasa: {Ui.Money(money)}</color>";
            foreach (var pair in requestRows)
            {
                bool paid = paidNeeds.Contains(pair.Key);
                pair.Value.label.text = paid ? "Ödendi" : $"Öde  {Ui.Money(pair.Key.price)}";
                pair.Value.button.interactable = !paid && money >= pair.Key.price;
            }
            foreach (var pair in rows)
            {
                int price = Shop.Price(pair.Key);
                bool owned = purchased.Contains(pair.Key);
                string state = owned ? "<color=#6fdc8c>Alındı</color>" : $"{Ui.Money(price)}";
                pair.Value.label.text = $"{Shop.Name(pair.Key)}\n<size=85%>{state}</size>";
                pair.Value.button.interactable = shopOpen && !owned && money >= price;
                pair.Value.chip.SetActive(owned);
            }
            ShowBond(bond, bondMax);
        }

        void ShowBond(int bond, int bondMax)
        {
            var bar = bondRow.Find("Segments");
            while (bondSegments.Count < bondMax)
                bondSegments.Add(Ui.Image(bar, "Segment", BondOff, Ui.Rounded, raycast: false));
            for (int i = 0; i < bondSegments.Count; i++)
                bondSegments[i].color = i < bond ? BondOn : BondOff;

            bool estranged = bond <= 0;
            bondLabel.text = estranged ? "AİLE BAĞI  ·  <color=#ff8a80>küs</color>" : $"AİLE BAĞI  ·  {bond}/{bondMax}";
            photoBack.SetActive(estranged);
            picture.SetActive(!estranged);
            caption.SetActive(!estranged);
            // 5–4 tam renk, 3–2 hafif soluk, 1 iyice soluk.
            float fade = bond >= 4 ? 0f : bond >= 2 ? 0.3f : 0.6f;
            photoFade.color = new Color(0.55f, 0.53f, 0.5f, fade);
        }
    }
}
