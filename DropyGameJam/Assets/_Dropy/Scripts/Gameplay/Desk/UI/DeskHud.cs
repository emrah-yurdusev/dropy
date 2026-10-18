using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>Kasa, gün/dosya sayacı, "Belgeleri İncele" butonu, para bildirimi, yönetmelik uyarısı,
    /// 1. gün brifingi, duraklatma menüsü ve dosya karnesi. Sabah televizyonu (<see cref="TvNewsView"/>) ve
    /// final gazetesi (<see cref="NewspaperView"/>) ayrı bileşenlerdir; burası onları açar.</summary>
    public class DeskHud : MonoBehaviour
    {
        static readonly Color Dark = new Color(0.1f, 0.1f, 0.12f, 0.85f);
        static readonly Color Gold = new Color(0.98f, 0.82f, 0.4f);

        TextMeshProUGUI moneyText;
        DayTracker tracker;
        Button inspectButton;
        TextMeshProUGUI inspectLabel;
        TextMeshProUGUI toastText;
        CanvasGroup toastGroup;
        TextMeshProUGUI warnText;
        CanvasGroup warnGroup;
        RectTransform summary;
        TextMeshProUGUI summaryText;
        NewsStyle style;
        TvNewsView tv;
        NewspaperView newspaper;
        GameObject title;
        bool started;
        GameObject pauseMenu;
        CanvasGroup fade;
        TextMeshProUGUI skipHint;
        Coroutine toastRoutine;
        Coroutine warnRoutine;
        CanvasGroup phoneGroup;
        TextMeshProUGUI phoneText;
        Coroutine phoneRoutine;

        public event System.Action InspectClicked;
        public event System.Action ResumeClicked;
        public event System.Action RestartClicked;

        public void Build(RectTransform root, NewsStyle newsStyle)
        {
            style = newsStyle;
            var money = Ui.Image(root, "Money", Dark, Ui.Rounded).rectTransform;
            money.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(260f, 56f));
            moneyText = Ui.Text(money, "Text", "", 26f, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            moneyText.rectTransform.Stretch();

            tracker = gameObject.AddComponent<DayTracker>();

            inspectButton = Ui.Button(root, "Inspect", "BELGELERİ İNCELE  [Tab]", new Color(0.2f, 0.45f, 0.75f), Color.white, 22f);
            ((RectTransform)inspectButton.transform).Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(340f, 64f));
            Ui.DropShadow((RectTransform)inspectButton.transform, 10f, new Vector2(0f, -3f), 0.3f);
            inspectLabel = inspectButton.GetComponentInChildren<TextMeshProUGUI>();
            inspectButton.onClick.AddListener(() => InspectClicked?.Invoke());
            inspectButton.gameObject.SetActive(false);

            var toast = Ui.Image(root, "Toast", Dark, Ui.Rounded).rectTransform;
            toast.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(520f, 64f));
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
            toastText = Ui.Text(toast, "Text", "", 24f, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            toastText.rectTransform.Stretch(12f, 0f, 12f, 0f);

            var warn = Ui.Image(root, "Warning", new Color(0.55f, 0.08f, 0.08f, 0.92f), Ui.Rounded).rectTransform;
            warn.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(620f, 56f));
            warnGroup = warn.gameObject.AddComponent<CanvasGroup>();
            warnGroup.alpha = 0f;
            warnGroup.blocksRaycasts = false;
            warnText = Ui.Text(warn, "Text", "", 22f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            warnText.rectTransform.Stretch(12f, 0f, 12f, 0f);

            // Gün içinde gelen mesaj: sağ altta telefon bildirimi.
            var phone = Ui.Image(root, "PhoneToast", new Color(0.08f, 0.09f, 0.12f, 0.95f), Ui.Rounded).rectTransform;
            phone.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 150f), new Vector2(430f, 130f));
            phoneGroup = phone.gameObject.AddComponent<CanvasGroup>();
            phoneGroup.alpha = 0f;
            phoneGroup.blocksRaycasts = false;
            var phoneHead = Ui.Text(phone, "Head", "TELEFON  ·  yeni mesaj", 16f, new Color(0.98f, 0.82f, 0.4f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            phoneHead.rectTransform.Stretch(18f, 0f, 18f, 12f);
            phoneText = Ui.Text(phone, "Text", "", 20f, Color.white, TextAlignmentOptions.TopLeft);
            phoneText.richText = true;
            phoneText.rectTransform.Stretch(18f, 12f, 18f, 38f);

            tv = gameObject.AddComponent<TvNewsView>();
            tv.Build(root, style, () => BlocksInput);
            newspaper = gameObject.AddComponent<NewspaperView>();
            newspaper.Build(root, style);
            newspaper.ReportClicked += ShowReport;
            newspaper.RestartClicked += () => RestartClicked?.Invoke();

            summary = Ui.Image(root, "Summary", new Color(0.08f, 0.08f, 0.1f, 0.94f), Ui.Rounded).rectTransform;
            summary.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 980f));
            summaryText = Ui.Text(summary, "Text", "", 22f, Color.white);
            summaryText.enableAutoSizing = true;
            summaryText.fontSizeMin = 14f;
            summaryText.fontSizeMax = 30f;
            summaryText.rectTransform.Stretch(40f, 110f, 40f, 36f);
            summaryText.richText = true;
            // Karne sayfası: gazeteden açılır, "Gazete" ile geri döner.
            summary.GetComponent<Image>().color = new Color(0.95f, 0.93f, 0.86f, 0.98f);
            summaryText.color = new Color(0.12f, 0.11f, 0.1f);
            var toggle = Ui.Button(summary, "PageToggle", "GAZETE", new Color(0.24f, 0.32f, 0.45f), Color.white, 22f);
            ((RectTransform)toggle.transform).Place(new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 28f), new Vector2(300f, 64f));
            toggle.onClick.AddListener(ShowNewspaper);
            var again = Ui.Button(summary, "Restart", "YENİDEN OYNA", new Color(0.85f, 0.6f, 0.2f), Color.white, 22f);
            ((RectTransform)again.transform).Place(new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(10f, 28f), new Vector2(300f, 64f));
            again.onClick.AddListener(() => RestartClicked?.Invoke());
            summary.gameObject.SetActive(false);

            // Gelişi/gidişi geçerken ekranı kısa süre karartan perde ve yürürken görünen ipucu.
            var fadeImage = Ui.Image(root, "Fade", Color.black, raycast: false);
            fadeImage.rectTransform.Stretch();
            fade = fadeImage.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 0f;
            fade.blocksRaycasts = false;
            skipHint = Ui.Text(root, "SkipHint", "Geç  [Sol tık]", 34f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Bottom, FontStyles.Italic);
            skipHint.rectTransform.Stretch(0f, 110f, 0f, 0f);
            skipHint.outlineWidth = 0.25f;
            skipHint.outlineColor = new Color32(0, 0, 0, 200);
            skipHint.gameObject.SetActive(false);

            title = BuildTitle(root);
            pauseMenu = BuildPause(root);
        }

        static readonly Color Paper = new Color(0.98f, 0.96f, 0.9f);
        static readonly Color Ink = new Color(0.16f, 0.15f, 0.14f);
        static readonly Color Faded = new Color(0.42f, 0.39f, 0.35f);
        static readonly Color OrderHeader = new Color(0.24f, 0.32f, 0.45f);
        static readonly Color Danger = new Color(0.55f, 0.1f, 0.08f);
        static readonly Color StepText = new Color(0.3f, 0.28f, 0.26f);
        const float ColumnWidth = 320f;
        const float ColumnGap = 30f;

        /// <summary>
        /// Mesainin ilk sabahı, masaya bırakılmış bir görev emri: kim olduğun, mesainin üç adımı,
        /// mühürlerin ne demek olduğu ve neyin tehlikede olduğu. Oyunun adı ana menüde.
        /// </summary>
        GameObject BuildTitle(RectTransform root)
        {
            var panel = Ui.Image(root, "Title", new Color(0.05f, 0.05f, 0.07f, 0.97f)).rectTransform.Stretch();

            var card = Ui.Image(panel, "Order", Paper, Ui.Rounded).rectTransform;
            card.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(1120f, 740f));
            Ui.DropShadow(card, 22f, new Vector2(6f, -10f), 0.5f);

            var header = Ui.Image(card, "Header", OrderHeader, Ui.Rounded, raycast: false).rectTransform;
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.offsetMin = new Vector2(12f, -96f);
            header.offsetMax = new Vector2(-12f, -12f);
            var heading = Ui.Text(header, "Heading", "İSKÂN BÜROSU", 40f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            heading.rectTransform.Stretch(30f, 0f, 30f, 0f);
            heading.characterSpacing = 2f;
            var tag = Ui.Text(header, "Tag", "GÖREV EMRİ  ·  1. GÜN", 22f, Gold, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            tag.rectTransform.Stretch(30f, 0f, 30f, 0f);
            tag.characterSpacing = 2f;

            var intro = Ui.Text(card, "Intro",
                "Bu ilçenin iskân memurusun. Binalar bitti; aileler taşınmak için senin mührünü bekliyor.",
                26f, Ink, TextAlignmentOptions.TopLeft);
            intro.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(1020f, 70f));

            SectionLabel(card, "MESAİ NASIL GEÇER", -196f);
            Step(card, 0, "1", "İNCELE", "Künyeyi ve raporu yönetmelikle karşılaştır, binaya büyüteçle bak.");
            Step(card, 1, "2", "SOR", "Şüpheli bir satıra ya da kusura tıkla; müteahhit cevap versin.");
            Step(card, 2, "3", "MÜHÜRLE", "Künyeye mührünü bas, müteahhide uzat ya da teslim et.");

            SectionLabel(card, "MÜHÜRLER", -400f);
            SealColumn(card, 0, Seal.Approve, $"Aileler taşınır.\nHarç: {Ui.Money(Economy.ApproveFee)}");
            SealColumn(card, 1, Seal.Reject, "Bina boş kalır.\nHarç yok.");
            SealColumn(card, 2, Seal.ShearWall, $"Perde eklenir, aileler taşınır.\nHarç: {Ui.Money(Economy.ShearWallFee)}");

            var risk = Ui.Image(card, "Risk", Danger, Ui.Rounded, raycast: false).rectTransform;
            risk.anchorMin = new Vector2(0f, 0f);
            risk.anchorMax = new Vector2(1f, 0f);
            risk.pivot = new Vector2(0.5f, 0f);
            risk.offsetMin = new Vector2(12f, 12f);
            risk.offsetMax = new Vector2(-12f, 112f);
            var riskText = Ui.Text(risk, "Text",
                $"<b>İlçe bir fay hattının üstünde; {Economy.QuakeDays[0]}. günün sonunda deprem bekleniyor.</b>\n" +
                "Onayladığın bina çökerse hapse girersin. Senin ailen de bu ilçede yaşıyor.",
                22f, Color.white, TextAlignmentOptions.MidlineLeft);
            riskText.richText = true;
            riskText.rectTransform.Stretch(30f, 0f, 30f, 0f);

            var start = Ui.Button(panel, "Start", "MESAİYE BAŞLA", new Color(0.85f, 0.6f, 0.2f), Color.white, 28f);
            ((RectTransform)start.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -350f), new Vector2(380f, 80f));
            Ui.DropShadow((RectTransform)start.transform, 10f, new Vector2(0f, -4f), 0.35f);
            start.onClick.AddListener(() => started = true);
            var credit = Ui.Text(panel, "Credit", "Dropy  ·  CIVIL GAME JAM  ·  Afete hazırlık ve farkındalık", 18f,
                new Color(1f, 1f, 1f, 0.45f), TextAlignmentOptions.Bottom, FontStyles.Italic);
            credit.rectTransform.Stretch(0f, 24f, 0f, 0f);
            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        static float ColumnX(int index) => (index - 1) * (ColumnWidth + ColumnGap);

        static void SectionLabel(RectTransform card, string label, float y)
        {
            var text = Ui.Text(card, label, label, 17f, Faded, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            text.characterSpacing = 3f;
            text.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(1020f, 28f));
            var line = Ui.Image(card, label + " Line", new Color(0f, 0f, 0f, 0.1f), raycast: false).rectTransform;
            line.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y - 30f), new Vector2(1020f, 2f));
        }

        /// <summary>Mesainin bir adımı: numara rozeti, fiil ve tek cümle.</summary>
        static void Step(RectTransform card, int index, string number, string verb, string line)
        {
            var box = Ui.Image(card, verb, new Color(0f, 0f, 0f, 0.045f), Ui.Rounded, raycast: false).rectTransform;
            box.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(ColumnX(index), -244f), new Vector2(ColumnWidth, 136f));
            var badge = Ui.Image(box, "Badge", OrderHeader, Ui.Rounded, raycast: false).rectTransform;
            badge.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(40f, 40f));
            var digit = Ui.Text(badge, "Digit", number, 22f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            digit.rectTransform.Stretch();
            var title = Ui.Text(box, "Verb", verb, 22f, Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            title.characterSpacing = 2f;
            title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(68f, -16f), new Vector2(ColumnWidth - 84f, 40f));
            var body = Ui.Text(box, "Line", line, 19f, StepText, TextAlignmentOptions.TopLeft);
            body.rectTransform.Stretch(16f, 10f, 16f, 66f);
        }

        /// <summary>Masadaki damganın aynısı ve altında ne sonuç doğurduğu.</summary>
        static void SealColumn(RectTransform card, int index, Seal seal, string outcome)
        {
            var column = Ui.Rect(seal.ToString(), card);
            column.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(ColumnX(index), -446f), new Vector2(ColumnWidth, 170f));
            var stamp = DocumentCard.CreateStampGraphic(column, seal);
            stamp.anchorMin = stamp.anchorMax = new Vector2(0.5f, 1f);
            stamp.pivot = new Vector2(0.5f, 1f);
            stamp.anchoredPosition = new Vector2(0f, -8f);
            stamp.localScale = Vector3.one * 0.85f;
            stamp.localRotation = Quaternion.Euler(0f, 0f, index == 1 ? 3f : -3f);
            var text = Ui.Text(column, "Outcome", outcome, 19f, Ink, TextAlignmentOptions.Top);
            text.rectTransform.Stretch(0f, 0f, 0f, 84f);
        }

        GameObject BuildPause(RectTransform root)
        {
            var panel = Ui.Image(root, "Pause", new Color(0.03f, 0.03f, 0.05f, 0.96f)).rectTransform.Stretch();
            var box = Ui.Rect("Box", panel);
            box.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 360f));
            var layout = box.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            var head = Ui.Text(box, "Head", "DURAKLATILDI", 40f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            head.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;
            AddMenuButton(box, "DEVAM  [Esc]", new Color(0.24f, 0.32f, 0.45f), () => ResumeClicked?.Invoke());
            AddMenuButton(box, "BAŞTAN BAŞLA", new Color(0.45f, 0.33f, 0.25f), () => RestartClicked?.Invoke());
            AddMenuButton(box, "OYUNDAN ÇIK", new Color(0.4f, 0.15f, 0.15f), Application.Quit);
            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        static void AddMenuButton(RectTransform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var button = Ui.Button(parent, label, label, color, Color.white, 24f);
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
            button.onClick.AddListener(onClick);
        }

        /// <summary>1. gün brifingini gösterir, "Mesaiye başla"ya basılana kadar bekler.</summary>
        public IEnumerator Briefing()
        {
            started = false;
            title.SetActive(true);
            title.transform.SetAsLastSibling();
            while (!started)
                yield return null;
            title.SetActive(false);
        }

        public void SetSkipHint(bool visible) => skipHint.gameObject.SetActive(visible);

        /// <summary>Yarıda kesilen akışın ekranda bıraktıklarını (başlık, televizyon, kararma) kaldırır.</summary>
        public void ResetOverlays()
        {
            title.SetActive(false);
            tv.Hide();
            fade.alpha = 0f;
            SetSkipHint(false);
        }

        void Update()
        {
            if (skipHint.gameObject.activeSelf)
                skipHint.alpha = 0.55f + 0.3f * Mathf.Sin(Time.unscaledTime * 3f);
        }

        /// <summary>Ekranı karartır ya da açar (gerçek zamanla; oyun duraklatılmışken de çalışır).</summary>
        public IEnumerator FadeTo(float target, float duration)
        {
            float start = fade.alpha;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                fade.alpha = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            fade.alpha = target;
        }

        public void SetPaused(bool paused)
        {
            pauseMenu.SetActive(paused);
            if (paused)
                pauseMenu.transform.SetAsLastSibling();
        }

        /// <summary>Başlık ya da duraklatma açıkken oyun içi tıklamalar sayılmaz.</summary>
        public bool BlocksInput => title.activeSelf || pauseMenu.activeSelf;

        /// <summary>Kasa eksideyse (kira borcu) kırmızı yazar.</summary>
        public void SetMoney(int amount)
        {
            moneyText.text = $"Kasa: {Ui.Money(amount)}";
            moneyText.color = amount < 0 ? new Color(1f, 0.45f, 0.4f) : Gold;
        }

        /// <summary>Sağ üstteki gün göstergesini kurar (hangi gün deprem, hangi gün sarsıntı, üstüne gelince ne yazacak).</summary>
        public void BuildTracker(RectTransform root, System.Func<int, string> forecast, System.Func<int, bool> isQuakeDay, System.Func<int, bool> isTremorDay) =>
            tracker.Build(root, Economy.Days, Economy.FilesPerDay, forecast, isQuakeDay, isTremorDay);

        public void SetCounter(int day, int file, int filesPerDay, string contractor) => tracker.Set(day, file, contractor);

        public void SetInspectAvailable(bool available) => inspectButton.gameObject.SetActive(available);

        public void SetInspectOpen(bool open) =>
            inspectLabel.text = open ? "BELGELERİ KAPAT  [Tab]" : "BELGELERİ İNCELE  [Tab]";

        public void Toast(string message, Color color)
        {
            if (toastRoutine != null)
                StopCoroutine(toastRoutine);
            toastText.text = message;
            toastText.color = color;
            toastRoutine = StartCoroutine(Fade(toastGroup, 3f));
        }

        /// <summary>Gün içinde telefona düşen mesaj: sağ altta birkaç saniye görünür.</summary>
        public void PhoneToast(string sender, string text)
        {
            if (phoneRoutine != null)
                StopCoroutine(phoneRoutine);
            phoneText.text = $"<b>{sender}</b>\n{text}";
            phoneGroup.transform.SetAsLastSibling();
            phoneRoutine = StartCoroutine(Fade(phoneGroup, 7f));
        }

        /// <summary>Kitapta yazan bir kural çiğnenince ya da mühür basılamayınca kısa kırmızı uyarı.</summary>
        public void Warn(string message)
        {
            if (warnRoutine != null)
                StopCoroutine(warnRoutine);
            warnText.text = message;
            warnRoutine = StartCoroutine(Fade(warnGroup, 3.5f));
        }

        static IEnumerator Fade(CanvasGroup group, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                group.alpha = t < 0.2f ? t / 0.2f : t > duration - 0.5f ? (duration - t) / 0.5f : 1f;
                yield return null;
            }
            group.alpha = 0f;
        }

        /// <summary>Gün başında sabah haberleri: televizyon ekranı kaplar, oyuncu tıklayana kadar kalır.</summary>
        public IEnumerator Morning(TvBroadcast broadcast) => tv.Show(broadcast);

        public void Quake(string message)
        {
            toastText.text = message;
            toastText.color = new Color(1f, 0.4f, 0.35f);
            if (toastRoutine != null)
                StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(Fade(toastGroup, 4.5f));
        }

        const string Green = "#1f7a3a", Red = "#a3221b";

        /// <summary>Reddedilen çürük bina: depremde çöktü ama iskân verilmediği için boştu.</summary>
        public static bool FellEmpty(DeskManager.CaseResult r) =>
            r.fate == BuildingFate.NotBuilt && BuildingRules.Flaws(r.buildingCase).Count > 0;

        /// <summary>Oyun sonu gazetesi; altındaki düğmeyle karneye geçilir.</summary>
        public void ShowFinal(IList<DeskManager.CaseResult> results, IDictionary<ShopItem, int> boughtOn,
            IEnumerable<(FamilyNeed need, bool paid)> family, bool estranged, int money, string report,
            FamilyOutcome home = FamilyOutcome.HomeStood)
        {
            summaryText.text = report;
            newspaper.Show(NewspaperWriter.Final(results, boughtOn, family, estranged, money, style, home));
        }

        /// <summary>Hapis: depremde onayladığın bina çöktü, oyun biter. Tutuklanma haberi; karne yine açılır.</summary>
        public void ShowJail(IList<DeskManager.CaseResult> results, IList<DeskManager.CaseResult> fallen, int quakeDay,
            IDictionary<ShopItem, int> boughtOn = null, FamilyOutcome home = FamilyOutcome.HomeStood)
        {
            summaryText.text = ReportCard(results);
            newspaper.Show(NewspaperWriter.Jail(results, fallen, quakeDay, style, boughtOn, home));
        }

        void ShowReport()
        {
            newspaper.Hide();
            summary.gameObject.SetActive(true);
            summary.SetAsLastSibling();
        }

        void ShowNewspaper()
        {
            summary.gameObject.SetActive(false);
            newspaper.Show();
        }

        public static int CountCorrect(IList<DeskManager.CaseResult> results) => results.Count(r => r.seal == RightSeal(r, results));

        /// <summary>
        /// Dosya karnesi: her dosyada basılan ve doğru mühür; yanlışlarda kuralın kısa açıklaması.
        /// Gazete ne olduğunu söyler, karne nedenini öğretir.
        /// </summary>
        public static string ReportCard(IList<DeskManager.CaseResult> results)
        {
            int correct = CountCorrect(results);
            var sb = new StringBuilder();
            sb.AppendLine("<size=20><b>BÜRO KAYITLARI</b>  ·  DOSYA KARNESİ</size>");
            sb.AppendLine("<size=14>________________________________________________________________________________</size>");
            sb.AppendLine($"<size=34><b>{results.Count} dosyadan {correct} doğru</b></size>\n");
            int day = 0;
            foreach (var r in results)
            {
                if (r.day != day)
                {
                    day = r.day;
                    sb.AppendLine($"<b>{day}. gün</b>");
                }
                var right = RightSeal(r, results);
                string bribe = r.bribed ? $"  ·  <color={Red}>rüşvet aldın</color>" : "";
                if (r.seal == right)
                {
                    sb.AppendLine($"<color={Green}>Doğru</color>  {r.buildingCase.buildingName}  ·  {DeskManager.SealName(r.seal)}{bribe}");
                    continue;
                }
                sb.AppendLine($"<color={Red}>Yanlış</color>  {r.buildingCase.buildingName}  ·  {DeskManager.SealName(r.seal)} bastın, doğrusu {DeskManager.SealName(right)}.{bribe}");
                sb.AppendLine($"<size=85%><i>      {BuildingRules.Explain(r.buildingCase, r.seal)}</i></size>");
            }
            return sb.ToString();
        }

        /// <summary>Perde günde bir kez basılabilir: o gün perde başka binaya basıldıysa ikinci yumuşak kata red de doğrudur.</summary>
        static Seal RightSeal(DeskManager.CaseResult r, IList<DeskManager.CaseResult> results)
        {
            var right = BuildingRules.CorrectSeal(r.buildingCase);
            if (right == Seal.ShearWall && r.seal == Seal.Reject && results.Any(o => o != r && o.day == r.day && o.seal == Seal.ShearWall))
                return Seal.Reject;
            return right;
        }

        public static string Headline(DeskManager.CaseResult r) =>
            string.IsNullOrEmpty(r.buildingCase.collapseHeadline) ? r.buildingCase.buildingName + " çöktü" : r.buildingCase.collapseHeadline;
    }
}
