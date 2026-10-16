using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>Kasa, müşteri sayacı, "Belgeleri İncele" butonu, para bildirimi ve gün sonu özeti.</summary>
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
        RectTransform summary;
        TextMeshProUGUI summaryText;
        Coroutine toastRoutine;

        public event System.Action InspectClicked;

        public void Build(RectTransform root)
        {
            var money = Ui.Image(root, "Money", Dark, Ui.Rounded).rectTransform;
            money.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(260f, 56f));
            moneyText = Ui.Text(money, "Text", "", 26f, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            moneyText.rectTransform.Stretch();

            var counter = Ui.Image(root, "Counter", Dark, Ui.Rounded).rectTransform;
            counter.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(300f, 56f));
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

            summary = Ui.Image(root, "Summary", new Color(0.08f, 0.08f, 0.1f, 0.94f), Ui.Rounded).rectTransform;
            summary.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 620f));
            summaryText = Ui.Text(summary, "Text", "", 24f, Color.white);
            summaryText.rectTransform.Stretch(40f, 40f, 40f, 36f);
            summaryText.richText = true;
            summary.gameObject.SetActive(false);
        }

        public void SetMoney(int amount) => moneyText.text = $"Kasa: {amount:N0} TL";

        public void SetCounter(int current, int total, string contractor) =>
            counterText.text = $"Müşteri {current}/{total}  ·  {contractor}";

        public void SetInspectAvailable(bool available) => inspectButton.gameObject.SetActive(available);

        public void SetInspectOpen(bool open) =>
            inspectLabel.text = open ? "BELGELERİ KAPAT  [Tab]" : "BELGELERİ İNCELE  [Tab]";

        public void Toast(string message, Color color)
        {
            if (toastRoutine != null)
                StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ShowToast(message, color));
        }

        IEnumerator ShowToast(string message, Color color)
        {
            toastText.text = message;
            toastText.color = color;
            for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
            {
                toastGroup.alpha = t < 0.2f ? t / 0.2f : t > 2.5f ? (3f - t) / 0.5f : 1f;
                yield return null;
            }
            toastGroup.alpha = 0f;
        }

        public void ShowSummary(IList<DeskManager.CaseResult> results, int money)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<size=38><b>MESAİ BİTTİ</b></size>\n");
            foreach (var r in results)
            {
                string safety = r.buildingSafe ? "<color=#6fdc8c>Depreme dayanıklı</color>" : "<color=#ff6b6b>RİSKLİ</color>";
                sb.AppendLine($"<b>{r.buildingCase.buildingName}</b>  ·  {DeskManager.DecisionName(r.decision)}  ·  +{r.money:N0} TL  ·  {safety}");
            }
            sb.AppendLine($"\n<size=30><color=#fad166>Kasa: {money:N0} TL</color></size>");
            summaryText.text = sb.ToString();
            summary.gameObject.SetActive(true);
        }
    }
}
