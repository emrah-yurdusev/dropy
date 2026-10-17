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
    /// gün başı yazısı, başlık ekranı, duraklatma menüsü ve deprem sonrası gazete ile dosya karnesi.</summary>
    public class DeskHud : MonoBehaviour
    {
        static readonly Color Dark = new Color(0.1f, 0.1f, 0.12f, 0.85f);
        static readonly Color Gold = new Color(0.98f, 0.82f, 0.4f);

        TextMeshProUGUI moneyText;
        TextMeshProUGUI counterText;
        Button inspectButton;
        TextMeshProUGUI inspectLabel;
        TextMeshProUGUI toastText;
        CanvasGroup toastGroup;
        TextMeshProUGUI warnText;
        CanvasGroup warnGroup;
        TextMeshProUGUI bannerText;
        TextMeshProUGUI bannerHint;
        CanvasGroup bannerGroup;
        RectTransform summary;
        TextMeshProUGUI summaryText;
        TextMeshProUGUI pageToggleLabel;
        string newspaperPage;
        string reportPage;
        bool showingReport;
        GameObject title;
        bool started;
        GameObject pauseMenu;
        CanvasGroup fade;
        TextMeshProUGUI skipHint;
        Coroutine toastRoutine;
        Coroutine warnRoutine;

        public event System.Action InspectClicked;
        public event System.Action ResumeClicked;
        public event System.Action RestartClicked;

        public void Build(RectTransform root)
        {
            var money = Ui.Image(root, "Money", Dark, Ui.Rounded).rectTransform;
            money.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(260f, 56f));
            moneyText = Ui.Text(money, "Text", "", 26f, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            moneyText.rectTransform.Stretch();

            var counter = Ui.Image(root, "Counter", Dark, Ui.Rounded).rectTransform;
            counter.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(460f, 56f));
            counterText = Ui.Text(counter, "Text", "", 22f, Color.white, TextAlignmentOptions.Center);
            counterText.rectTransform.Stretch();

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

            var banner = Ui.Image(root, "DayBanner", new Color(0.05f, 0.05f, 0.07f, 0.9f)).rectTransform.Stretch();
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerGroup.blocksRaycasts = false;
            bannerText = Ui.Text(banner, "Text", "", 30f, Color.white, TextAlignmentOptions.Center);
            bannerText.rectTransform.Stretch(200f, 0f, 200f, 0f);
            bannerText.richText = true;
            bannerHint = Ui.Text(banner, "Hint", "Devam etmek için tıkla", 22f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Bottom, FontStyles.Italic);
            bannerHint.rectTransform.Stretch(0f, 60f, 0f, 0f);

            summary = Ui.Image(root, "Summary", new Color(0.08f, 0.08f, 0.1f, 0.94f), Ui.Rounded).rectTransform;
            summary.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 980f));
            summaryText = Ui.Text(summary, "Text", "", 22f, Color.white);
            summaryText.enableAutoSizing = true;
            summaryText.fontSizeMin = 14f;
            summaryText.fontSizeMax = 30f;
            summaryText.rectTransform.Stretch(40f, 110f, 40f, 36f);
            summaryText.richText = true;
            var toggle = Ui.Button(summary, "PageToggle", "DOSYA KARNESİ", new Color(0.24f, 0.32f, 0.45f), Color.white, 22f);
            ((RectTransform)toggle.transform).Place(new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 28f), new Vector2(300f, 64f));
            pageToggleLabel = toggle.GetComponentInChildren<TextMeshProUGUI>();
            toggle.onClick.AddListener(TogglePage);
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
            skipHint = Ui.Text(root, "SkipHint", "Geç  [Sol tık]", 22f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Bottom, FontStyles.Italic);
            skipHint.rectTransform.Stretch(0f, 30f, 0f, 0f);
            skipHint.outlineWidth = 0.2f;
            skipHint.outlineColor = new Color32(0, 0, 0, 200);
            skipHint.gameObject.SetActive(false);

            title = BuildTitle(root);
            pauseMenu = BuildPause(root);
        }

        GameObject BuildTitle(RectTransform root)
        {
            var panel = Ui.Image(root, "Title", new Color(0.05f, 0.05f, 0.07f, 0.97f)).rectTransform.Stretch();
            var text = Ui.Text(panel, "Text",
                "<size=110><b>VİZE BÜROSU</b></size>\n\n" +
                "<size=30>Bir deprem bölgesinde, beş günlük mesai.\nHer mühür bir binaya, her bina birilerine.</size>",
                30f, Color.white, TextAlignmentOptions.Center);
            text.richText = true;
            text.rectTransform.Stretch(200f, 260f, 200f, 120f);
            var start = Ui.Button(panel, "Start", "MESAİYE BAŞLA", new Color(0.85f, 0.6f, 0.2f), Color.white, 28f);
            ((RectTransform)start.transform).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(380f, 80f));
            start.onClick.AddListener(() => started = true);
            var credit = Ui.Text(panel, "Credit", "Dropy  ·  CIVIL GAME JAM  ·  Afete hazırlık ve farkındalık", 18f,
                new Color(1f, 1f, 1f, 0.45f), TextAlignmentOptions.Bottom, FontStyles.Italic);
            credit.rectTransform.Stretch(0f, 40f, 0f, 0f);
            panel.gameObject.SetActive(false);
            return panel.gameObject;
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

        /// <summary>Başlık ekranını gösterir, "Mesaiye başla"ya basılana kadar bekler.</summary>
        public IEnumerator Title()
        {
            started = false;
            title.SetActive(true);
            title.transform.SetAsLastSibling();
            while (!started)
                yield return null;
            title.SetActive(false);
        }

        public void SetSkipHint(bool visible) => skipHint.gameObject.SetActive(visible);

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

        public void SetMoney(int amount) => moneyText.text = $"Kasa: {Ui.Money(amount)}";

        public void SetCounter(int day, int file, int filesPerDay, string contractor) =>
            counterText.text = $"Gün {day}  ·  Dosya {file}/{filesPerDay}  ·  {contractor}";

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

        /// <summary>
        /// Gün başında ekranı kaplayan "GÜN N" yazısı. Oyuncu tıklayana kadar (Space / Enter da olur) kalır;
        /// ilk yarım saniye tıklama sayılmaz ki önceki ekrandan kalan tık yanlışlıkla geçirmesin.
        /// </summary>
        public IEnumerator DayBanner(int day, string subtitle)
        {
            bannerText.text = $"<size=90><b>GÜN {day}</b></size>\n\n{subtitle}";
            bannerGroup.alpha = 1f;
            bannerHint.alpha = 0f;
            for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
                yield return null;
            while (!ContinuePressed())
            {
                bannerHint.alpha = 0.45f + 0.35f * Mathf.Sin(Time.unscaledTime * 3f);
                yield return null;
            }
            bannerHint.alpha = 0f;
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                bannerGroup.alpha = 1f - t / 0.6f;
                yield return null;
            }
            bannerGroup.alpha = 0f;
        }

        bool ContinuePressed()
        {
            if (BlocksInput)
                return false;
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            return (mouse != null && mouse.leftButton.wasPressedThisFrame)
                || (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame));
        }

        public void Quake(string message)
        {
            toastText.text = message;
            toastText.color = new Color(1f, 0.4f, 0.35f);
            if (toastRoutine != null)
                StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(Fade(toastGroup, 4.5f));
        }

        /// <summary>
        /// Deprem sonrası gazete: çöken binalar manşet olur, onaylayan müteahhidin son sözü altına düşer;
        /// ayakta kalanlar ve hiç yapılmayanlar kısa listelenir. Altta evin ve ailenin durumu.
        /// </summary>
        public void ShowFinal(IList<DeskManager.CaseResult> results, ICollection<ShopItem> purchased,
            IEnumerable<(FamilyNeed need, bool paid)> family, bool estranged, int money, string report)
        {
            summary.GetComponent<Image>().color = new Color(0.95f, 0.93f, 0.86f, 0.98f);
            summaryText.color = new Color(0.12f, 0.11f, 0.1f);

            var collapsed = results.Where(r => r.fate == BuildingFate.Collapsed).ToList();
            var standing = results.Where(r => r.fate == BuildingFate.Standing).ToList();
            var notBuilt = results.Where(r => r.fate == BuildingFate.NotBuilt).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("<size=20><b>KENT GAZETESİ</b>  ·  DEPREM ÖZEL BASKISI</size>");
            sb.AppendLine("<size=14>________________________________________________________________________________</size>");
            string main = collapsed.Count == 0
                ? "Büronun onayladığı binaların hepsi ayakta"
                : Headline(collapsed[0]);
            sb.AppendLine($"<size=42><b>{main.ToUpper(new System.Globalization.CultureInfo("tr-TR"))}</b></size>");
            if (collapsed.Count > 0)
                sb.AppendLine(Quote(collapsed[0]));
            for (int i = 1; i < collapsed.Count; i++)
            {
                sb.AppendLine($"\n<size=28><b>{Headline(collapsed[i])}</b></size>");
                sb.AppendLine(Quote(collapsed[i]));
            }

            if (standing.Count > 0)
            {
                sb.AppendLine("\n<b>Ayakta kalanlar</b>");
                foreach (var r in standing.Where(r => !string.IsNullOrEmpty(r.buildingCase.standingLine)))
                    sb.AppendLine(r.buildingCase.standingLine);
                var plain = standing.Where(r => string.IsNullOrEmpty(r.buildingCase.standingLine)).Select(r => r.buildingCase.buildingName);
                sb.AppendLine(string.Join(", ", plain));
            }
            if (notBuilt.Count > 0)
                sb.AppendLine("\n<b>Hiç yapılmayanlar:</b> " + string.Join(", ", notBuilt.Select(r => r.buildingCase.buildingName)));

            sb.AppendLine("\n<size=28><b>EVDE</b></size>");
            sb.AppendLine(Line(purchased, ShopItem.Bag, "Su, fener ve ilk yardım çantası hazırdı.", "Karanlıkta ne su vardı ne fener."));
            sb.AppendLine(Line(purchased, ShopItem.Fixing, "Dolaplar duvara sabitliydi, yerinde kaldı.", "Sabitlenmemiş dolap Elif'in odasına devrildi."));
            sb.AppendLine(Line(purchased, ShopItem.Retrofit, "Güçlendirilen apartman ayakta kaldı.", "Güçlendirilmemiş apartman ağır hasar aldı."));
            foreach (var (need, paid) in family)
            {
                string line = paid ? need.endingPaid : need.endingUnpaid;
                if (!string.IsNullOrEmpty(line))
                    sb.AppendLine((paid ? "<color=#1f7a3a>Ödendi</color>  " : "<color=#a3221b>Ödenmedi</color>  ") + line);
            }
            bool safe = Shop.Items.All(purchased.Contains);
            sb.AppendLine(safe ? "\n<color=#1f7a3a><b>Ailen güvende.</b></color>" : "\n<color=#a3221b><b>Ailen güvende değil.</b></color>");
            if (estranged)
                sb.AppendLine("<color=#a3221b><b>Ailen hayatta ama seninle konuşmuyor.</b></color>");
            sb.AppendLine($"<size=18>Kasa: {Ui.Money(money)}</size>");
            newspaperPage = sb.ToString();
            reportPage = report;
            showingReport = false;
            summaryText.text = newspaperPage;
            pageToggleLabel.text = "DOSYA KARNESİ";
            summary.gameObject.SetActive(true);
            summary.SetAsLastSibling();
        }

        void TogglePage()
        {
            showingReport = !showingReport;
            summaryText.text = showingReport ? reportPage : newspaperPage;
            pageToggleLabel.text = showingReport ? "GAZETE" : "DOSYA KARNESİ";
        }

        /// <summary>
        /// Dosya karnesi: her dosyada basılan ve doğru mühür; yanlışlarda kuralın kısa açıklaması.
        /// Gazete ne olduğunu söyler, karne nedenini öğretir.
        /// </summary>
        public static string ReportCard(IList<DeskManager.CaseResult> results, int citations)
        {
            int correct = results.Count(r => r.seal == BuildingRules.CorrectSeal(r.buildingCase));
            var sb = new StringBuilder();
            sb.AppendLine("<size=20><b>BÜRO KAYITLARI</b>  ·  DOSYA KARNESİ</size>");
            sb.AppendLine("<size=14>________________________________________________________________________________</size>");
            sb.AppendLine($"<size=34><b>{results.Count} dosyadan {correct} doğru</b></size>   <size=22>Tutanak: {citations}</size>\n");
            int day = 0;
            foreach (var r in results)
            {
                if (r.day != day)
                {
                    day = r.day;
                    sb.AppendLine($"<b>{day}. gün</b>");
                }
                var right = BuildingRules.CorrectSeal(r.buildingCase);
                if (r.seal == right)
                {
                    sb.AppendLine($"<color=#1f7a3a>Doğru</color>  {r.buildingCase.buildingName}  ·  {DeskManager.SealName(r.seal)}");
                    continue;
                }
                sb.AppendLine($"<color=#a3221b>Yanlış</color>  {r.buildingCase.buildingName}  ·  {DeskManager.SealName(r.seal)} bastın, doğrusu {DeskManager.SealName(right)}.");
                sb.AppendLine($"<size=85%><i>      {BuildingRules.Explain(r.buildingCase, r.seal)}</i></size>");
            }
            return sb.ToString();
        }

        static string Headline(DeskManager.CaseResult r) =>
            string.IsNullOrEmpty(r.buildingCase.collapseHeadline) ? r.buildingCase.buildingName + " çöktü" : r.buildingCase.collapseHeadline;

        /// <summary>Onay anında müteahhidin söylediği söz, şimdi enkazın altına yazılır.</summary>
        static string Quote(DeskManager.CaseResult r)
        {
            string line = r.seal == Seal.ShearWall ? r.buildingCase.shearWallLine : r.buildingCase.approvedLine;
            return $"<i>Müteahhit {r.buildingCase.contractorName}, onay günü: \"{line}\"</i>";
        }

        static string Line(ICollection<ShopItem> purchased, ShopItem item, string yes, string no) =>
            purchased.Contains(item)
                ? $"<color=#1f7a3a>Alındı</color>  {Shop.Name(item)}: {yes}"
                : $"<color=#a3221b>Alınmadı</color>  {Shop.Name(item)}: {no}";
    }
}
