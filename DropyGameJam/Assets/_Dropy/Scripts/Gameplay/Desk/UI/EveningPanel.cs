using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>Akşam telefonunda tek bir mesaj. İstek ise bildirimin içinde "Öde" düğmesi çıkar.</summary>
    public struct EveningMessage
    {
        public string sender;
        public string text;
        public FamilyNeed request;
        /// <summary>Altın kenarlı önemli bildirim (prim gibi): gözden kaçmasın.</summary>
        public bool important;
    }

    /// <summary>
    /// Akşam: solda günün hesabı (yeşil banka kartı; borçta kırmızı), aile fotoğrafı ve aile bağı; sağda
    /// telefonun kilit ekranı. Mesajlar bildirim kartı olarak düşer (aileden gelen istekler Öde düğmesiyle),
    /// altta deprem dükkânı widget'ı; ikisi aynı kasadan ödenir. "Sabah oldu"ya basınca sıradaki gün başlar.
    /// </summary>
    public class EveningPanel : MonoBehaviour
    {
        static readonly Color Dim = new Color(0.03f, 0.03f, 0.06f, 0.88f);
        static readonly Color LedgerGreen = new Color(0.13f, 0.5f, 0.32f);
        static readonly Color LedgerDebt = new Color(0.6f, 0.15f, 0.12f);
        static readonly Color PhoneFrame = new Color(0.17f, 0.17f, 0.19f);
        static readonly Color PhoneBody = new Color(0.02f, 0.02f, 0.03f);
        static readonly Color Wallpaper = new Color(0.13f, 0.15f, 0.29f);
        static readonly Color Glass = new Color(0.97f, 0.97f, 0.98f, 0.96f);
        static readonly Color GlassInk = new Color(0.1f, 0.1f, 0.12f);
        static readonly Color GlassFaded = new Color(0.33f, 0.33f, 0.38f);
        /// <summary>Mesaj kartı tam opak, yazısı neredeyse siyah: koyu duvar kâğıdı yazıyı griye çekmesin.</summary>
        static readonly Color MessageCard = Color.white;
        static readonly Color MessageInk = new Color(0.04f, 0.04f, 0.06f);
        static readonly Color MessagesGreen = new Color(0.2f, 0.78f, 0.35f);
        static readonly Color ImportantGold = new Color(0.96f, 0.72f, 0.12f);
        /// <summary>Bildirim kartının başlık (uygulama adı) ve gönderen satırının kapladığı üst pay.</summary>
        const float CardTop = 76f;
        const float MessageWidth = 396f;
        static readonly Color ShopOrange = new Color(0.98f, 0.58f, 0.18f);
        static readonly Color IosBlue = new Color(0f, 0.48f, 1f);
        static readonly Color PillGrey = new Color(0.9f, 0.9f, 0.93f);
        static readonly Color OwnedGreen = new Color(0.18f, 0.62f, 0.3f);
        static readonly Color PayColor = new Color(0.85f, 0.6f, 0.2f);
        static readonly Color Owned = new Color(0.36f, 0.7f, 0.45f);
        static readonly Color BondOn = new Color(0.95f, 0.58f, 0.45f);
        static readonly Color BondOff = new Color(0.25f, 0.24f, 0.26f);
        const float ShopRowHeight = 50f;
        const string Clock = "19:42";

        class ItemRow
        {
            public GameObject row;
            public Button button;
            public TextMeshProUGUI label;
            public GameObject chip;
        }

        RectTransform root;
        Image ledger;
        TextMeshProUGUI ledgerTitle;
        TextMeshProUGUI ledgerBalanceLabel;
        TextMeshProUGUI ledgerBalance;
        TextMeshProUGUI ledgerLabels;
        TextMeshProUGUI ledgerAmounts;
        TextMeshProUGUI ledgerNote;
        TextMeshProUGUI lockDate;
        TextMeshProUGUI walletText;
        TextMeshProUGUI phoneFooter;
        RectTransform messageList;
        ScrollRect messageScroll;
        TextMeshProUGUI moreHint;
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
        int shopDay;
        /// <summary>Bu akşam satılanlar (Run'da gelir); boşsa günün takvimine bakılır.</summary>
        ICollection<ShopItem> offeredTonight;
        RectTransform shopWidget;
        RectTransform messageViewport;
        /// <summary>Okunan bildirimler (gönderen + metin): aynı mesaj sonraki akşam tekrar düşerse okunmuş görünür.</summary>
        readonly HashSet<string> readMessages = new HashSet<string>();
        bool done;

        public event System.Action<ShopItem> BuyClicked;
        public event System.Action<FamilyNeed> PayClicked;

        /// <param name="familyPhoto">Aile fotoğrafı; boşsa fotoğraf düz şekillerle çizilir.</param>
        public void Build(RectTransform canvas, Sprite familyPhoto = null)
        {
            root = Ui.Image(canvas, "Evening", Dim).rectTransform.Stretch();

            BuildLedger(root);
            BuildPhoto(root, familyPhoto);
            BuildBond(root);

            var chips = Ui.Rect("Purchased", root);
            chips.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(-440f, -440f), new Vector2(540f, 56f));
            var chipLayout = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
            chipLayout.spacing = 10f;
            chipLayout.childAlignment = TextAnchor.MiddleCenter;
            chipLayout.childControlWidth = true;
            chipLayout.childControlHeight = true;
            chipLayout.childForceExpandWidth = false;

            var screen = BuildPhone(root);
            BuildLockScreen(screen);
            BuildShop(screen, chips);

            phoneFooter = Ui.Text(screen, "Footer", "", 17f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center, FontStyles.Italic);
            phoneFooter.rectTransform.anchorMin = Vector2.zero;
            phoneFooter.rectTransform.anchorMax = new Vector2(1f, 0f);
            phoneFooter.rectTransform.offsetMin = new Vector2(20f, 96f);
            phoneFooter.rectTransform.offsetMax = new Vector2(-20f, 124f);

            var morning = Ui.Button(screen, "Morning", "SABAH OLDU", PayColor, Color.white, 24f);
            ((RectTransform)morning.transform).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(300f, 62f));
            morning.onClick.AddListener(() => done = true);
            var home = Ui.Image(screen, "HomeIndicator", new Color(1f, 1f, 1f, 0.85f), Ui.Rounded, raycast: false).rectTransform;
            home.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(150f, 6f));

            root.gameObject.SetActive(false);
        }

        // ---------------- Günün hesabı ----------------

        /// <summary>Banka kartı gibi: ortada kasa, altında bugünkü gelir ve giderler. Borçta kırmızıya döner.</summary>
        void BuildLedger(RectTransform parent)
        {
            ledger = Ui.Image(parent, "Ledger", LedgerGreen, Ui.Rounded);
            var rect = ledger.rectTransform;
            rect.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-440f, 200f), new Vector2(540f, 340f));
            Ui.DropShadow(rect, 16f, new Vector2(4f, -6f), 0.4f);

            var faint = new Color(1f, 1f, 1f, 0.72f);
            ledgerTitle = Ui.Text(rect, "Title", "", 18f, faint, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            ledgerTitle.characterSpacing = 3f;
            ledgerTitle.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -26f), new Vector2(470f, 26f));

            ledgerBalanceLabel = Ui.Text(rect, "BalanceLabel", "", 16f, faint, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            ledgerBalanceLabel.characterSpacing = 2f;
            ledgerBalanceLabel.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -66f), new Vector2(470f, 22f));
            ledgerBalance = Ui.Text(rect, "Balance", "", 58f, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            ledgerBalance.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -86f), new Vector2(470f, 70f));

            var line = Ui.Image(rect, "Divider", new Color(1f, 1f, 1f, 0.22f), raycast: false).rectTransform;
            line.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(472f, 2f));

            ledgerLabels = Ui.Text(rect, "Labels", "", 22f, Color.white, TextAlignmentOptions.TopLeft);
            ledgerLabels.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -184f), new Vector2(300f, 80f));
            ledgerAmounts = Ui.Text(rect, "Amounts", "", 22f, Color.white, TextAlignmentOptions.TopRight, FontStyles.Bold);
            ledgerAmounts.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -184f), new Vector2(220f, 80f));

            ledgerNote = Ui.Text(rect, "Note", "", 16f, faint, TextAlignmentOptions.BottomLeft, FontStyles.Italic);
            ledgerNote.richText = true;
            ledgerNote.rectTransform.Stretch(34f, 20f, 34f, 260f);
        }

        // ---------------- Telefon ----------------

        /// <summary>Gövde, ince metal çerçeve, ekran ve ön kameranın adası. Ekranı döner.</summary>
        static RectTransform BuildPhone(RectTransform parent)
        {
            var frame = Ui.Image(parent, "Phone", PhoneFrame, Ui.Device).rectTransform;
            frame.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(330f, 0f), new Vector2(480f, 980f));
            Ui.DropShadow(frame, 18f, new Vector2(4f, -8f), 0.5f);
            var body = Ui.Image(frame, "Body", PhoneBody, Ui.Device, raycast: false).rectTransform.Stretch(4f, 4f, 4f, 4f);
            var screen = Ui.Image(body, "Screen", Wallpaper, Ui.Device).rectTransform.Stretch(12f, 12f, 12f, 12f);
            var island = Ui.Image(screen, "Island", Color.black, Ui.Rounded, raycast: false).rectTransform;
            island.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(124f, 34f));

            var time = Ui.Text(screen, "StatusTime", Clock, 17f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            time.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(76f, -18f), new Vector2(90f, 28f));
            BuildBattery(screen);
            return screen;
        }

        static void BuildBattery(RectTransform screen)
        {
            var battery = Ui.Image(screen, "Battery", new Color(1f, 1f, 1f, 0.35f), Ui.Rounded, raycast: false).rectTransform;
            battery.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(34f, 17f));
            var level = Ui.Image(battery, "Level", Color.white, Ui.Rounded, raycast: false).rectTransform;
            level.anchorMin = Vector2.zero;
            level.anchorMax = new Vector2(0.62f, 1f);
            level.offsetMin = new Vector2(2f, 2f);
            level.offsetMax = new Vector2(0f, -2f);
            for (int i = 0; i < 4; i++)
            {
                var bar = Ui.Image(screen, "Signal", Color.white, raycast: false).rectTransform;
                bar.Place(new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-104f + i * 7f, -41f), new Vector2(4f, 5f + i * 3f));
            }
        }

        void BuildLockScreen(RectTransform screen)
        {
            lockDate = Ui.Text(screen, "Date", "", 20f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center, FontStyles.Bold);
            lockDate.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(400f, 28f));
            var clock = Ui.Text(screen, "Clock", Clock, 80f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            clock.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(400f, 88f));

            // Banka uygulamasının kilit ekranı widget'ı: kasadaki para.
            var wallet = Ui.Image(screen, "Wallet", new Color(1f, 1f, 1f, 0.16f), Ui.Rounded, raycast: false).rectTransform;
            wallet.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -174f), new Vector2(230f, 36f));
            walletText = Ui.Text(wallet, "Text", "", 19f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            walletText.richText = true;
            walletText.rectTransform.Stretch();

            // Bildirimler kaydırılan bir listede: kartlar sıkışıp metni kesmez, sığmayanlar tekerlekle görülür.
            // Alt kenarı dükkân widget'ının üstüne oturur (widget boyu akşama göre değişir, Refresh ayarlar).
            var viewport = Ui.Rect("Messages", screen);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.offsetMin = new Vector2(14f, 380f);
            viewport.offsetMax = new Vector2(-14f, -224f);
            messageViewport = viewport;
            viewport.gameObject.AddComponent<RectMask2D>();
            // Boş alanda da tekerlek ve sürükleme çalışsın diye görünmez bir zemin.
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            messageList = Ui.Rect("Content", viewport);
            messageList.anchorMin = new Vector2(0f, 1f);
            messageList.anchorMax = new Vector2(1f, 1f);
            messageList.pivot = new Vector2(0.5f, 1f);
            messageList.offsetMin = messageList.offsetMax = Vector2.zero;
            var messageLayout = messageList.gameObject.AddComponent<VerticalLayoutGroup>();
            messageLayout.spacing = 10f;
            messageLayout.childControlHeight = true;
            messageLayout.childControlWidth = true;
            messageLayout.childForceExpandHeight = false;
            messageList.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            messageScroll = viewport.gameObject.AddComponent<ScrollRect>();
            messageScroll.viewport = viewport;
            messageScroll.content = messageList;
            messageScroll.horizontal = false;
            messageScroll.movementType = ScrollRect.MovementType.Clamped;
            messageScroll.scrollSensitivity = 28f;

            // Altta daha mesaj varsa küçük ipucu.
            moreHint = Ui.Text(screen, "MoreMessages", "aşağı kaydır · daha fazla mesaj", 15f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center, FontStyles.Bold);
            moreHint.rectTransform.anchorMin = new Vector2(0f, 0f);
            moreHint.rectTransform.anchorMax = new Vector2(1f, 0f);
            moreHint.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            moreHint.rectTransform.anchoredPosition = new Vector2(0f, 367f);
            moreHint.rectTransform.sizeDelta = new Vector2(0f, 22f);
            moreHint.gameObject.SetActive(false);
        }

        void Update()
        {
            if (messageScroll == null || moreHint == null || !root.gameObject.activeInHierarchy)
                return;
            float overflow = messageList.rect.height - messageScroll.viewport.rect.height;
            bool more = overflow > 4f && messageScroll.verticalNormalizedPosition > 0.02f;
            if (moreHint.gameObject.activeSelf != more)
                moreHint.gameObject.SetActive(more);
            if (more)
                moreHint.alpha = 0.6f + 0.3f * Mathf.Sin(Time.unscaledTime * 4f);
        }

        /// <summary>Kilit ekranındaki deprem dükkânı widget'ı: her satırda ad ve fiyat hapı (dokununca alınır).</summary>
        void BuildShop(RectTransform screen, RectTransform chips)
        {
            int count = Shop.All.Length;
            float height = 52f + count * ShopRowHeight + 12f;
            var widget = Ui.Image(screen, "Shop", Glass, Ui.Rounded).rectTransform;
            widget.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 132f), new Vector2(428f, height));
            shopWidget = widget;
            AppHeader(widget, "DEPREM DÜKKÂNI", "", ShopOrange);

            for (int i = 0; i < count; i++)
            {
                var item = Shop.All[i];
                var row = Ui.Rect(item.ToString(), widget);
                row.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -52f - i * ShopRowHeight), new Vector2(400f, ShopRowHeight));
                var name = Ui.Text(row, "Name", Shop.Name(item), 19f, GlassInk, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                name.rectTransform.Stretch(4f, 0f, 150f, 0f);
                if (i > 0)
                {
                    var line = Ui.Image(row, "Line", new Color(0f, 0f, 0f, 0.08f), raycast: false).rectTransform;
                    line.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 1.5f));
                }

                var button = Ui.Button(row, "Price", "", PillGrey, IosBlue, 17f);
                ((RectTransform)button.transform).Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-2f, 0f), new Vector2(124f, 36f));
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.richText = true;
                var captured = item;
                ButtonClickSound.Skip(button);
                button.onClick.AddListener(() => BuyClicked?.Invoke(captured));

                var chip = Ui.Image(chips, item + "Chip", Owned, Ui.Rounded).gameObject;
                var chipText = Ui.Text(chip.transform, "Text", Shop.Name(item), 18f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                chipText.rectTransform.Stretch(14f, 0f, 14f, 0f);
                chip.AddComponent<LayoutElement>().preferredWidth = chipText.GetPreferredValues().x + 28f;
                rows[item] = new ItemRow { row = row.gameObject, button = button, label = label, chip = chip };
            }
        }

        /// <summary>Bildirim ve widget'ların üst satırı: renkli uygulama ikonu, uygulama adı, sağda zaman.</summary>
        static void AppHeader(RectTransform card, string app, string when, Color iconColor)
        {
            var icon = Ui.Image(card, "Icon", iconColor, Ui.Rounded, raycast: false).rectTransform;
            icon.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -12f), new Vector2(28f, 28f));
            // İkonun içinde küçük beyaz konuşma balonu.
            var glyph = Ui.Image(icon, "Glyph", Color.white, Ui.Rounded, raycast: false).rectTransform;
            glyph.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(16f, 12f));
            var name = Ui.Text(card, "App", app, 14f, GlassFaded, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            name.characterSpacing = 2f;
            name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(52f, -12f), new Vector2(260f, 28f));
            if (string.IsNullOrEmpty(when))
                return;
            var time = Ui.Text(card, "When", when, 14f, GlassFaded, TextAlignmentOptions.MidlineRight);
            time.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -12f), new Vector2(100f, 28f));
        }

        // ---------------- Fotoğraf ve aile bağı ----------------

        /// <summary>Masadaki aile fotoğrafı: iki yetişkin, bir çocuk, arkada ev. Aile bağı düştükçe solar, sıfırda ters kapanır.</summary>
        void BuildPhoto(RectTransform parent, Sprite familyPhoto)
        {
            var frame = Ui.Image(parent, "FamilyPhoto", new Color(0.96f, 0.94f, 0.9f), Ui.Rounded).rectTransform;
            frame.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-440f, -190f), new Vector2(340f, 300f));
            frame.localRotation = Quaternion.Euler(0f, 0f, 3f);
            Ui.DropShadow(frame, 12f, new Vector2(3f, -5f), 0.4f);

            var picImage = Ui.Image(frame, "Picture", new Color(0.62f, 0.78f, 0.9f), raycast: false);
            var pic = picImage.rectTransform.Stretch(16f, 56f, 16f, 16f);
            picture = pic.gameObject;
            if (familyPhoto != null)
            {
                picImage.sprite = familyPhoto;
                picImage.color = Color.white;
            }
            else
                DrawPlaceholder(pic);
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

        /// <summary>Fotoğraf yoksa: gökyüzü, çimen, ev ve üç kişilik aile düz şekillerle.</summary>
        static void DrawPlaceholder(RectTransform pic)
        {
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

        // ---------------- Akış ----------------

        /// <param name="bondNote">Aile bağının neden düştüğü (dün ödenmeyen istek, depremde devrilen dolap...); yoksa null.</param>
        /// <param name="offered">Bu akşam dükkânda satılanlar (onarım gibi şartlı ürünler dahil); boşsa günün takvimi.</param>
        public IEnumerator Run(int day, int income, int rent, int money, ICollection<ShopItem> purchased,
            IList<EveningMessage> messages, ICollection<FamilyNeed> paidNeeds,
            int bond, int bondMax, string bondNote, bool shopOpen, ICollection<ShopItem> offered = null, int bonus = 0)
        {
            this.shopOpen = shopOpen;
            shopDay = day;
            offeredTonight = offered;
            // Önce aç: TextMeshPro kapalı nesnede yazı boyutunu ölçemiyor (bildirimler ölçülüyor).
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();

            ledgerTitle.text = $"{day}. GÜN AKŞAMI  ·  HESAP";
            ledgerLabels.text = "Bugünkü mühürler\nKira" + (bonus > 0 ? "\n<color=#fff3a8>Performans primi</color>" : "");
            ledgerAmounts.text = $"+{Ui.Money(income)}\n-{Ui.Money(rent)}" + (bonus > 0 ? $"\n<color=#fff3a8>+{Ui.Money(bonus)}</color>" : "");
            ledgerLabels.fontSize = ledgerAmounts.fontSize = bonus > 0 ? 19f : 22f;
            string bondLine = bond <= 0 ? "<color=#ffc4b8><b>Ailen seninle konuşmuyor.</b></color>\n"
                : !string.IsNullOrEmpty(bondNote) ? $"<color=#ffc4b8>{bondNote}</color>\n" : "";
            ledgerNote.text = bondLine + "Eve yürünmez. Alışveriş telefondan.";

            lockDate.text = $"Akşam  ·  {day}. gün";
            phoneFooter.text = !shopOpen ? "Dükkân kapandı."
                : day == Economy.LastEvening ? "Dükkân bu akşam son kez açık. Deprem kapıda." : "";
            ShowMessages(messages);
            Refresh(money, purchased, paidNeeds, bond, bondMax);

            done = false;
            while (!done)
                yield return null;
            root.gameObject.SetActive(false);
        }

        /// <summary>Paneli kapatır (akış yarıda kesildiğinde).</summary>
        public void Hide()
        {
            done = true;
            root.gameObject.SetActive(false);
        }

        /// <summary>
        /// Her mesaj kilit ekranına kısa bir bildirim kartı olarak düşer: gönderen ve tek satırlık önizleme ("…").
        /// Okunmamış kart mavi noktayla nabız gibi atar; dokununca tamamı açılır ve okundu olur. İstekte altta Öde düğmesi,
        /// önemli bildirimde (prim) altın kenar var.
        /// </summary>
        void ShowMessages(IList<EveningMessage> messages)
        {
            foreach (Transform child in messageList)
                Destroy(child.gameObject);
            requestRows.Clear();
            if (messages.Count == 0)
            {
                var empty = Ui.Text(messageList, "Empty", "Bildirim yok", 18f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Center, FontStyles.Bold);
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
                return;
            }
            // En yeni bildirim en üstte (gerçek telefondaki gibi): önemli haber (prim, borç, istek) hep görünür.
            for (int i = messages.Count - 1; i >= 0; i--)
            {
                var message = messages[i];
                bool request = message.request != null;
                var cardImage = Ui.Image(messageList, request ? "Request" : "Message", MessageCard, Ui.Rounded);
                var card = cardImage.rectTransform;
                AppHeader(card, "MESAJLAR", i == messages.Count - 1 ? "şimdi" : $"{messages.Count - 1 - i} dk önce", MessagesGreen);
                if (message.important)
                {
                    var outline = card.gameObject.AddComponent<Outline>();
                    outline.effectColor = ImportantGold;
                    outline.effectDistance = new Vector2(3f, -3f);
                }

                var sender = Ui.Text(card, "Sender", message.sender, 21f, MessageInk, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                sender.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(36f, -60f), new Vector2(MessageWidth - 20f, 28f));
                sender.textWrappingMode = TextWrappingModes.NoWrap;

                // Okunmamış işaretleri tek kapta: soldaki mavi nokta ve gönderenin yanında "YENİ" etiketi.
                var unread = Ui.Rect("Unread", card).Stretch();
                var dot = Ui.Image(unread, "Dot", IosBlue, Ui.Rounded, raycast: false).rectTransform;
                dot.Place(new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(22f, -60f), new Vector2(12f, 12f));
                float senderWidth = sender.GetPreferredValues(message.sender).x;
                var badge = Ui.Image(unread, "New", IosBlue, Ui.Rounded, raycast: false).rectTransform;
                badge.Place(new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(36f + senderWidth + 10f, -60f), new Vector2(52f, 22f));
                var badgeText = Ui.Text(badge, "Text", "YENİ", 13f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                badgeText.rectTransform.Stretch();
                badgeText.characterSpacing = 2f;

                float buttonRow = request ? 50f : 0f;
                var text = Ui.Text(card, "Text", message.text, 19f, MessageInk);
                text.richText = true;
                text.rectTransform.Stretch(16f, 12f + buttonRow, 16f, CardTop);
                float full = text.GetPreferredValues(message.text, MessageWidth, 0f).y;
                float oneLine = text.GetPreferredValues("Ag", MessageWidth, 0f).y;
                float collapsed = CardTop + oneLine + 14f + buttonRow;
                float expanded = CardTop + full + 14f + buttonRow;
                var size = card.gameObject.AddComponent<LayoutElement>();

                var view = card.gameObject.AddComponent<MessageCardView>();
                string key = message.sender + "|" + message.text;
                view.Init(cardImage, text, size, unread.gameObject, dot, collapsed, expanded, messageList,
                    readMessages.Contains(key), () => readMessages.Add(key));
                var open = card.gameObject.AddComponent<Button>();
                open.transition = Selectable.Transition.None;
                open.onClick.AddListener(view.Toggle);
                if (!request)
                    continue;

                var pay = Ui.Button(card, "Pay", "", IosBlue, Color.white, 18f);
                ((RectTransform)pay.transform).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(396f, 40f));
                var label = pay.GetComponentInChildren<TextMeshProUGUI>();
                label.richText = true;
                var need = message.request;
                ButtonClickSound.Skip(pay);
                pay.onClick.AddListener(() => PayClicked?.Invoke(need));
                requestRows[need] = new ItemRow { button = pay, label = label };
            }
            // Liste en üstten (en yeniden) başlasın.
            Canvas.ForceUpdateCanvases();
            messageScroll.verticalNormalizedPosition = 1f;
        }

        public void Refresh(int money, ICollection<ShopItem> purchased, ICollection<FamilyNeed> paidNeeds, int bond, int bondMax)
        {
            bool debt = money < 0;
            ledger.color = debt ? LedgerDebt : LedgerGreen;
            ledgerBalanceLabel.text = debt ? "BORÇ" : "KASA";
            ledgerBalance.text = Ui.Money(Mathf.Abs(money));
            walletText.text = debt ? $"Borç  <color=#ff9d8f>{Ui.Money(-money)}</color>" : $"Kasa  <color=#7ee39a>{Ui.Money(money)}</color>";

            foreach (var pair in requestRows)
            {
                bool paid = paidNeeds.Contains(pair.Key);
                pair.Value.label.text = paid ? "Ödendi" : $"Öde  {Ui.Money(pair.Key.price)}";
                pair.Value.button.interactable = !paid && money >= pair.Key.price;
            }
            // Görünen satırlar üstten sıralanır, widget onlara göre kısalır (güçlendirme satırı 2. akşamdan çıkar).
            int visible = 0;
            foreach (var item in Shop.All)
            {
                var pair = new KeyValuePair<ShopItem, ItemRow>(item, rows[item]);
                // Sabitleme yalnız ilk depremden önce satılır; alınan ürün listede "Alındı" olarak kalır.
                bool owned = purchased.Contains(pair.Key);
                bool offered = owned || (offeredTonight != null ? offeredTonight.Contains(pair.Key) : Shop.Offered(pair.Key, shopDay));
                pair.Value.row.SetActive(offered);
                if (offered)
                    ((RectTransform)pair.Value.row.transform).anchoredPosition = new Vector2(0f, -52f - visible++ * ShopRowHeight);
                pair.Value.label.text = owned ? "Alındı" : Ui.Money(Shop.Price(pair.Key));
                pair.Value.label.color = owned ? OwnedGreen : IosBlue;
                pair.Value.button.interactable = shopOpen && !owned && money >= Shop.Price(pair.Key);
                pair.Value.chip.SetActive(owned);
            }
            float shopHeight = 52f + visible * ShopRowHeight + 12f;
            shopWidget.sizeDelta = new Vector2(shopWidget.sizeDelta.x, shopHeight);
            // Bildirim listesi dükkânın hemen üstünde biter; arada "daha fazla mesaj" ipucunun payı kalır.
            float shopTop = shopWidget.anchoredPosition.y + shopHeight;
            messageViewport.offsetMin = new Vector2(messageViewport.offsetMin.x, shopTop + 28f);
            moreHint.rectTransform.anchoredPosition = new Vector2(0f, shopTop + 14f);
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

    /// <summary>
    /// Akşam telefonundaki bir bildirim kartı: kısa hâlde tek satır önizleme ve "…", dokununca tam metin.
    /// Oyuncu karta dokunup açana kadar okunmamıştır: kart nabız gibi açık maviye döner, mavi nokta yanıp söner
    /// ve "YENİ" etiketi durur. Okundu bilgisi panelde saklanır, aynı mesaj sonraki akşam gelirse okunmuş görünür.
    /// </summary>
    public class MessageCardView : MonoBehaviour
    {
        static readonly Color UnreadTint = new Color(0.83f, 0.91f, 1f);

        Image card;
        Color baseColor;
        TextMeshProUGUI text;
        LayoutElement size;
        GameObject marks;
        Transform dot;
        RectTransform list;

        float collapsedHeight;
        float expandedHeight;
        bool expanded;
        bool read;
        System.Action onRead;

        public void Init(Image cardImage, TextMeshProUGUI body, LayoutElement layout, GameObject unreadMarks, Transform pulseDot,
            float collapsed, float full, RectTransform parentList, bool alreadyRead, System.Action markRead)
        {
            read = alreadyRead;
            onRead = markRead;
            marks = unreadMarks;
            dot = pulseDot;
            marks.SetActive(!read);
            card = cardImage;
            baseColor = cardImage.color;
            text = body;
            size = layout;
            list = parentList;
            collapsedHeight = collapsed;
            expandedHeight = Mathf.Max(collapsed, full);
            Apply();
        }

        /// <summary>Kartı açar/kapatır; ilk dokunuşta okundu sayılır.</summary>
        public void Toggle()
        {
            expanded = !expanded;
            MarkRead();
            Apply();
        }

        void MarkRead()
        {
            if (read)
                return;
            read = true;
            marks.SetActive(false);
            card.color = baseColor;
            onRead?.Invoke();
        }

        void Apply()
        {
            text.textWrappingMode = expanded ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = expanded ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            size.preferredHeight = expanded ? expandedHeight : collapsedHeight;
            if (list != null)
                LayoutRebuilder.MarkLayoutForRebuild(list);
        }

        void Update()
        {
            if (read)
                return;
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
            card.color = Color.Lerp(baseColor, UnreadTint, k * 0.8f);
            dot.transform.localScale = Vector3.one * (0.8f + 0.4f * k);
        }
    }
}
