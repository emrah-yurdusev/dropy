using System.Collections;
using Dropy.Core;
using Dropy.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dropy.Menu
{
    /// <summary>
    /// Ayarlar kartı: ses (müzik, efekt) ve oyun (yazı hızı, tam ekran, ekran sarsıntısı, kalem öğretici).
    /// Ana menü ve masadaki duraklatma menüsü aynı kartı kullanır; ayarlar <see cref="GameAudio"/> ve
    /// <see cref="GameSettings"/> üzerinden kaydedilir. Esc ile kapatmayı sahibi yapar (<see cref="Close"/>).
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        static readonly Color CardFace = new Color(0.17f, 0.19f, 0.24f, 0.98f);
        static readonly Color CardEdge = new Color(0.07f, 0.08f, 0.1f);
        static readonly Color Track = new Color(0.08f, 0.09f, 0.11f);
        static readonly Color Muted = new Color(0.85f, 0.2f, 0.18f);
        static readonly Color OkFace = new Color(0.38f, 0.72f, 0.24f);
        static readonly Color OkEdge = new Color(0.12f, 0.3f, 0.08f);
        static readonly Color PillOff = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color SectionColor = new Color(1f, 1f, 1f, 0.45f);
        const float CardHeight = 780f;

        RectTransform panel;
        RectTransform card;
        System.Action click;

        public bool IsOpen => panel != null && panel.gameObject.activeSelf;

        public static SettingsPanel Create(RectTransform root, System.Action onClick = null)
        {
            var settings = root.gameObject.AddComponent<SettingsPanel>();
            settings.click = onClick;
            settings.Build(root);
            return settings;
        }

        public void Open()
        {
            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
            StartCoroutine(Pop(card));
        }

        public void Close()
        {
            GameAudio.Save();
            panel.gameObject.SetActive(false);
        }

        void Build(RectTransform root)
        {
            panel = Ui.Image(root, "Settings", new Color(0f, 0f, 0f, 0.6f)).rectTransform.Stretch();

            card = Ui.Image(panel, "Card", CardEdge, Ui.Rounded).rectTransform;
            card.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, CardHeight));
            Ui.DropShadow(card, 22f, new Vector2(6f, -10f), 0.5f);
            var face = Ui.Image(card, "Face", CardFace, Ui.Rounded).rectTransform.Stretch(5f, 5f, 5f, 5f);

            // Başlık: dişli ikon + yazı, altında ince ikaz şeridi
            var header = Ui.Rect("Header", face).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(750f, 96f));
            var gear = Ui.Image(header, "Gear", MenuArt.HazardYellow, MenuArt.Gear, raycast: false);
            gear.type = Image.Type.Simple;
            gear.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-150f, 0f), new Vector2(50f, 50f));
            var title = Ui.Text(header, "Title", "AYARLAR", 46f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30f, 0f), new Vector2(300f, 80f));
            title.characterSpacing = 4f;
            var band = Ui.Image(face, "Stripes", Color.white, raycast: false);
            band.sprite = MenuArt.HazardStripes;
            band.type = Image.Type.Tiled;
            band.pixelsPerUnitMultiplier = 3f;
            band.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(750f, 12f));

            Section(face, "SES", -128f);
            VolumeRow(face, "MÜZİK", -190f, () => GameAudio.Music, v => GameAudio.Music = v,
                () => GameAudio.MusicMuted, m => GameAudio.MusicMuted = m);
            VolumeRow(face, "SES EFEKTLERİ", -268f, () => GameAudio.Sfx, v => GameAudio.Sfx = v,
                () => GameAudio.SfxMuted, m => GameAudio.SfxMuted = m);

            Section(face, "OYUN", -340f);
            OptionRow(face, "YAZI HIZI", -398f, () => GameSettings.TextSpeedNames[GameSettings.TextSpeed], () => true,
                () => GameSettings.TextSpeed = (GameSettings.TextSpeed + 1) % GameSettings.TextSpeedNames.Length);
            Toggle(face, "TAM EKRAN", -462f, () => GameSettings.Fullscreen, v => GameSettings.Fullscreen = v);
            Toggle(face, "EKRAN SARSINTISI", -526f, () => GameSettings.ScreenShake, v => GameSettings.ScreenShake = v);
            Toggle(face, "KALEM ÖĞRETİCİ", -590f, () => GameSettings.PenTutorial, v => GameSettings.PenTutorial = v);

            var ok = OkButton(face);
            ok.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(280f, 84f));
            ok.GetComponent<Button>().onClick.AddListener(Close);

            panel.gameObject.SetActive(false);
        }

        static void Section(RectTransform parent, string label, float y)
        {
            var section = Ui.Text(parent, "Section", label, 24f, SectionColor, TextAlignmentOptions.Left, FontStyles.Bold);
            section.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, y), new Vector2(200f, 34f));
            section.characterSpacing = 8f;
        }

        void Toggle(RectTransform parent, string label, float y, System.Func<bool> get, System.Action<bool> set) =>
            OptionRow(parent, label, y, () => get() ? "AÇIK" : "KAPALI", get, () => set(!get()));

        /// <summary>Solda ad, sağda tıklayınca sıradaki değere geçen hap düğme (açık/kapalı ya da yavaş/normal/hızlı).</summary>
        void OptionRow(RectTransform parent, string label, float y, System.Func<string> value, System.Func<bool> on, System.Action next)
        {
            var row = Ui.Rect(label, parent).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(660f, 56f));
            var nameText = Ui.Text(row, "Name", label, 26f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            nameText.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(400f, 56f));
            nameText.textWrappingMode = TextWrappingModes.NoWrap;

            var pill = Ui.Image(row, "Value", PillOff, Ui.Rounded);
            pill.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(210f, 50f));
            var button = pill.gameObject.AddComponent<Button>();
            pill.gameObject.AddComponent<MenuButtonFx>().hoverScale = 1.06f;
            var text = Ui.Text(pill.transform, "Text", "", 24f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            text.rectTransform.Stretch();
            text.characterSpacing = 2f;

            void Refresh()
            {
                bool active = on();
                text.text = value();
                pill.color = active ? MenuArt.HazardYellow : PillOff;
                text.color = active ? new Color(0.15f, 0.12f, 0.05f) : new Color(1f, 1f, 1f, 0.7f);
            }

            button.onClick.AddListener(() =>
            {
                next();
                click?.Invoke();
                Refresh();
            });
            Refresh();
            // Tam ekran gibi dışarıdan değişebilen ayarlar kart her açıldığında güncel görünsün.
            row.gameObject.AddComponent<RefreshOnEnable>().refresh = Refresh;
        }

        void VolumeRow(RectTransform parent, string label, float y,
            System.Func<float> getLevel, System.Action<float> setLevel, System.Func<bool> getMuted, System.Action<bool> setMuted)
        {
            var row = Ui.Rect(label, parent).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(660f, 70f));

            // Hoparlör: tıklayınca sessize alır, üstünde kırmızı çizgi belirir
            var muteBg = Ui.Image(row, "Mute", new Color(1f, 1f, 1f, 0.08f), Ui.Rounded);
            muteBg.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
            var muteButton = muteBg.gameObject.AddComponent<Button>();
            muteBg.gameObject.AddComponent<MenuButtonFx>().hoverScale = 1.1f;
            var speaker = Ui.Image(muteBg.transform, "Icon", Color.white, MenuArt.Speaker, raycast: false);
            speaker.type = Image.Type.Simple;
            speaker.rectTransform.Stretch(10f, 10f, 10f, 10f);
            var slash = Ui.Image(muteBg.transform, "Slash", Muted, Ui.Rounded, raycast: false).rectTransform;
            slash.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(62f, 8f));
            slash.localRotation = Quaternion.Euler(0f, 0f, -45f);

            var nameText = Ui.Text(row, "Name", label, 26f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            nameText.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(84f, 0f), new Vector2(250f, 60f));
            nameText.textWrappingMode = TextWrappingModes.NoWrap;

            var percent = Ui.Text(row, "Value", "", 28f, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
            percent.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(90f, 60f));

            var slider = CreateSlider(row);
            ((RectTransform)slider.transform).Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(350f, 0f), new Vector2(210f, 18f));

            void Refresh()
            {
                bool muted = getMuted();
                slash.gameObject.SetActive(muted);
                speaker.color = muted ? new Color(1f, 1f, 1f, 0.4f) : Color.white;
                slider.interactable = !muted;
                slider.SetValueWithoutNotify(getLevel());
                percent.text = muted ? "KAPALI" : $"%{Mathf.RoundToInt(getLevel() * 100f)}";
                percent.fontSize = muted ? 20f : 28f;
                percent.color = muted ? Muted : Color.white;
            }

            slider.onValueChanged.AddListener(v =>
            {
                setLevel(v);
                Refresh();
            });
            muteButton.onClick.AddListener(() =>
            {
                setMuted(!getMuted());
                click?.Invoke();
                Refresh();
            });
            Refresh();
        }

        static Slider CreateSlider(RectTransform parent)
        {
            var track = Ui.Image(parent, "Slider", Track, Ui.Rounded);
            var slider = track.gameObject.AddComponent<Slider>();

            var fillArea = Ui.Rect("Fill Area", track.transform).Stretch();
            var fill = Ui.Image(fillArea, "Fill", MenuArt.HazardYellow, Ui.Rounded, raycast: false);
            fill.rectTransform.Stretch();

            var handleArea = Ui.Rect("Handle Area", track.transform).Stretch(16f, 0f, 16f, 0f);
            var handle = Ui.Image(handleArea, "Handle", Color.white, MenuArt.Circle);
            handle.type = Image.Type.Simple;
            handle.rectTransform.sizeDelta = new Vector2(36f, 18f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }

        /// <summary>Ana menüdeki gibi kalın kenarlı yeşil "TAMAM" düğmesi.</summary>
        RectTransform OkButton(Transform parent)
        {
            var root = Ui.Rect("OkButton", parent);
            var edgeImage = Ui.Image(root, "Edge", OkEdge, Ui.Rounded);
            edgeImage.rectTransform.Stretch();
            Ui.DropShadow(root, 14f, new Vector2(4f, -8f), 0.45f);
            var faceImage = Ui.Image(root, "Face", OkFace, Ui.Rounded, raycast: false);
            faceImage.rectTransform.Stretch(5f, 10f, 5f, 5f);
            var text = Ui.Text(faceImage.transform, "Label", "TAMAM", 34f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            text.rectTransform.Stretch();
            text.characterSpacing = 2f;
            text.outlineColor = OkEdge;
            text.outlineWidth = 0.2f;
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = faceImage;
            button.onClick.AddListener(() => click?.Invoke());
            root.gameObject.AddComponent<MenuButtonFx>();
            return root;
        }

        static IEnumerator Pop(RectTransform target)
        {
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.25f;
                float back = 1f + 2.70158f * Mathf.Pow(k - 1f, 3f) + 1.70158f * Mathf.Pow(k - 1f, 2f);
                target.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, back);
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        /// <summary>Satır her görünür olduğunda değerini yeniden okur.</summary>
        class RefreshOnEnable : MonoBehaviour
        {
            public System.Action refresh;
            void OnEnable() => refresh?.Invoke();
        }
    }
}
