using System.Collections;
using Dropy.Core;
using Dropy.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dropy.Menu
{
    /// <summary>
    /// Giriş ekranı: ofis arka planı, ikaz şeritli logo, Oyuna Başla / Ayarlar / Çıkış butonları ve
    /// ayarlar kartı (<see cref="SettingsPanel"/>). Arayüzü (Canvas, EventSystem) kendisi kurar;
    /// sahnede yalnızca bu bileşen ve isteğe bağlı arka plan görseli / sesler gerekir.
    /// main sahnesinde oyunun önüne konur: DeskManager kapalı bekler, Oyuna Başla'da açılır.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        const string Title = "SAĞLAM\nMÜHÜR";
        const string Subtitle = "FAYKÖY İSKÂN BÜROSU";

        static readonly Color Wall = new Color(0.84f, 0.67f, 0.38f);
        static readonly Color LogoPanel = new Color(0.13f, 0.12f, 0.11f);
        static readonly Color TitleTop = new Color(1f, 0.9f, 0.45f);
        static readonly Color TitleBottom = new Color(0.98f, 0.62f, 0.08f);
        static readonly Color TitleEdge = new Color(0.22f, 0.12f, 0.03f);
        static readonly Color Rivet = new Color(0.62f, 0.62f, 0.6f);
        static readonly Color StartFace = new Color(0.38f, 0.72f, 0.24f);
        static readonly Color StartEdge = new Color(0.12f, 0.3f, 0.08f);
        static readonly Color SettingsFace = new Color(0.42f, 0.48f, 0.57f);
        static readonly Color SettingsEdge = new Color(0.14f, 0.17f, 0.22f);
        static readonly Color ExitFace = new Color(0.34f, 0.34f, 0.37f);
        static readonly Color ExitEdge = new Color(0.12f, 0.12f, 0.14f);

        [Header("Görsel")]
        [Tooltip("Arka plan: ofisten alınmış görüntü. Boşsa düz duvar rengi kullanılır.")]
        [SerializeField] Sprite background;
        [Tooltip("Açıksa duvar rengi ve arka plan görseli çizilmez; menü sahnedeki 3B görüntünün üstünde durur.")]
        [SerializeField] bool liveBackground;

        [Header("Akış")]
        [Tooltip("Oyuna Başla'ya basınca yüklenecek sahne (Build Settings'te olmalı). " +
                 "Boşsa menü aynı sahnede kalır: aşağıdaki nesneler açılır ve menü kapanır.")]
        [SerializeField] string gameScene = "";
        [Tooltip("Aynı sahnede başlatırken açılacak, oyun boyunca kapalı bekleyen nesneler (ör. DeskManager)")]
        [SerializeField] GameObject[] enableOnStart;

        // Menü oyun açılışında bir kez gösterilir; sahne yeniden yüklenince (yeniden başla vb.) atlanır.
        static bool shown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetShown() => shown = false;

        /// <summary>Oyundan ana menüye döner (duraklatma menüsündeki "Ana menü"): menü yeniden gösterilir.</summary>
        public static void ReturnToMenu()
        {
            shown = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            // Masanın ortam uğultusu kalıcı bir kaynakta çalar: menüde yalnız menü müziği duyulsun.
            Sfx.Ambience(0f);
            Sfx.Room(0f);
            GameAudio.Save();
            SceneManager.LoadScene(0);
        }

        [Header("Ses")]
        [SerializeField] AudioClip menuMusic;
        [SerializeField] AudioClip clickSound;
        [SerializeField] AudioClip hoverSound;

        RectTransform backgroundHolder;
        RectTransform logo;
        SettingsPanel settings;
        Image fade;
        AudioSource musicSource;
        AudioSource sfxSource;
        readonly RectTransform[] menuButtons = new RectTransform[3];
        bool leaving;

        void Awake()
        {
            if (shown)
            {
                enabled = false;
                EnableGame();
                Destroy(gameObject);
                return;
            }
            shown = true;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;

            BuildUi();
            GameAudio.Changed += ApplyAudio;
            ApplyAudio();
        }

        void Start()
        {
            if (!enabled)
                return;
            if (menuMusic != null)
            {
                musicSource.clip = menuMusic;
                musicSource.Play();
            }
            StartCoroutine(Intro());
        }

        void OnDestroy() => GameAudio.Changed -= ApplyAudio;

        void Update()
        {
            float t = Time.unscaledTime;

            // Arka planda yavaş yakınlaşma, logoda hafif sallanma
            if (backgroundHolder != null)
            {
                float s = 1.03f + Mathf.Sin(t * 0.12f) * 0.03f;
                backgroundHolder.localScale = new Vector3(s, s, 1f);
            }
            if (!leaving)
                logo.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.9f) * 0.8f);

            if (settings.IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                settings.Close();
        }

        // ---------------------------------------------------------------- Kurulum

        void BuildUi()
        {
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            var canvasGo = new GameObject("MenuCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Oyunun arayüzünün (sortingOrder 10) üstünde kalsın; geçişte kararma her şeyi örtsün
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)canvasGo.transform;

            BuildBackground(root);
            BuildLogo(root);
            BuildMenuButtons(root);
            settings = SettingsPanel.Create(root, PlayClick);

            var version = Ui.Text(root, "Version", $"v{Application.version}", 20f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.BottomRight);
            version.rectTransform.Place(Vector2.right, Vector2.right, new Vector2(-24f, 16f), new Vector2(300f, 30f));

            fade = Ui.Image(root, "Fade", Color.black);
            fade.rectTransform.Stretch();
        }

        void BuildBackground(RectTransform root)
        {
            if (!liveBackground)
                Ui.Image(root, "Wall", Wall, raycast: false).rectTransform.Stretch();
            if (background != null && !liveBackground)
            {
                backgroundHolder = Ui.Rect("Background", root).Stretch();
                var image = Ui.Image(backgroundHolder, "Image", Color.white, raycast: false);
                image.sprite = background;
                image.type = Image.Type.Simple;
                image.rectTransform.Stretch();
                var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = background.rect.width / background.rect.height;
            }

            // Butonların okunması için sağa doğru, logonun okunması için yukarı doğru karartma
            var right = Ui.Image(root, "ShadeRight", new Color(0f, 0f, 0f, 0.6f), MenuArt.FadeRight, raycast: false);
            right.type = Image.Type.Simple;
            right.rectTransform.Stretch();
            right.rectTransform.anchorMin = new Vector2(0.45f, 0f);
            var top = Ui.Image(root, "ShadeTop", new Color(0f, 0f, 0f, 0.4f), MenuArt.FadeUp, raycast: false);
            top.type = Image.Type.Simple;
            top.rectTransform.Stretch();
            top.rectTransform.anchorMin = new Vector2(0f, 0.6f);
        }

        void BuildLogo(RectTransform root)
        {
            // Sol üst köşede: ortada oturan karakterin başını kapatmasın.
            logo = Ui.Rect("Logo", root).Place(new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(540f, -185f), new Vector2(800f, 300f));

            // İkaz şeritli çerçeve: yuvarlak köşeli maske içinde döşenmiş şerit
            var frame = Ui.Image(logo, "Frame", Color.white, Ui.Rounded);
            frame.rectTransform.Stretch();
            Ui.DropShadow(logo, 22f, new Vector2(8f, -12f), 0.55f);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var stripes = Ui.Image(frame.transform, "Stripes", Color.white, raycast: false);
            stripes.sprite = MenuArt.HazardStripes;
            stripes.type = Image.Type.Tiled;
            stripes.pixelsPerUnitMultiplier = 1.6f;
            stripes.rectTransform.Stretch();

            var panel = Ui.Image(logo, "Panel", LogoPanel, Ui.Rounded, raycast: false);
            panel.rectTransform.Stretch(26f, 26f, 26f, 26f);
            var inner = Ui.Image(panel.transform, "InnerEdge", new Color(1f, 1f, 1f, 0.08f), Ui.Frame, raycast: false);
            inner.rectTransform.Stretch(6f, 6f, 6f, 6f);

            foreach (var corner in new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) })
            {
                var rivet = Ui.Image(panel.transform, "Rivet", Rivet, MenuArt.Circle, raycast: false);
                rivet.type = Image.Type.Simple;
                var offset = new Vector2(corner.x < 0.5f ? 22f : -22f, corner.y < 0.5f ? 22f : -22f);
                rivet.rectTransform.Place(corner, new Vector2(0.5f, 0.5f), offset, new Vector2(16f, 16f));
            }

            var title = Ui.Text(panel.transform, "Title", Title, 118f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            title.rectTransform.Stretch(40f, 18f, 40f, 12f);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.enableAutoSizing = true;
            title.fontSizeMin = 60f;
            title.fontSizeMax = 118f;
            title.lineSpacing = -22f;
            title.characterSpacing = 3f;
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(TitleTop, TitleTop, TitleBottom, TitleBottom);
            Outline(title, TitleEdge, 0.22f, 0.9f);

            // Alt şerit: kurdele gibi panelin altına taşan alt başlık
            var ribbon = Ui.Image(logo, "Ribbon", new Color(0.08f, 0.08f, 0.09f), Ui.Rounded, raycast: false).rectTransform;
            ribbon.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(520f, 62f));
            Ui.Image(ribbon, "Edge", MenuArt.HazardYellow, Ui.Frame, raycast: false).rectTransform.Stretch(3f, 3f, 3f, 3f);
            var sub = Ui.Text(ribbon, "Subtitle", Subtitle, 30f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            sub.rectTransform.Stretch(10f, 0f, 10f, 0f);
            sub.characterSpacing = 6f;
            sub.textWrappingMode = TextWrappingModes.NoWrap;

            var hat = Ui.Image(logo, "HardHat", Color.white, raycast: false);
            hat.sprite = MenuArt.HardHat;
            hat.type = Image.Type.Simple;
            hat.rectTransform.Place(new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-30f, 6f), new Vector2(200f, 138f));
            hat.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -16f);
        }

        void BuildMenuButtons(RectTransform root)
        {
            var column = Ui.Rect("Buttons", root).Place(new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-400f, -300f), new Vector2(600f, 460f));

            var start = MenuButton(column, "StartButton", "OYUNA BAŞLA", StartFace, StartEdge, new Vector2(580f, 128f), 50f, PlayIcon);
            start.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(580f, 128f));
            start.GetComponent<MenuButtonFx>().pulse = true;
            start.GetComponent<Button>().onClick.AddListener(StartGame);

            var settings = MenuButton(column, "SettingsButton", "AYARLAR", SettingsFace, SettingsEdge, new Vector2(500f, 104f), 40f, GearIcon);
            settings.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(500f, 104f));
            settings.GetComponent<Button>().onClick.AddListener(OpenSettings);

            var exit = MenuButton(column, "ExitButton", "ÇIKIŞ", ExitFace, ExitEdge, new Vector2(380f, 90f), 36f, null);
            exit.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -310f), new Vector2(380f, 90f));
            exit.GetComponent<Button>().onClick.AddListener(Quit);

            menuButtons[0] = start;
            menuButtons[1] = settings;
            menuButtons[2] = exit;
        }

        /// <summary>Kalın kenarlı, alt dudağı koyu "3B" buton. İkon verilirse sağ tarafa konur.</summary>
        RectTransform MenuButton(Transform parent, string name, string label, Color face, Color edge, Vector2 size, float fontSize,
            System.Func<RectTransform, Color, RectTransform> icon)
        {
            var root = Ui.Rect(name, parent);
            root.sizeDelta = size;

            var edgeImage = Ui.Image(root, "Edge", edge, Ui.Rounded);
            edgeImage.rectTransform.Stretch();
            Ui.DropShadow(root, 14f, new Vector2(4f, -8f), 0.45f);
            var faceImage = Ui.Image(root, "Face", face, Ui.Rounded, raycast: false);
            faceImage.rectTransform.Stretch(5f, 10f, 5f, 5f);
            var shine = Ui.Image(faceImage.transform, "Shine", new Color(1f, 1f, 1f, 0.16f), Ui.Rounded, raycast: false).rectTransform.Stretch(6f, 0f, 6f, 5f);
            shine.anchorMin = new Vector2(0f, 0.5f);

            bool hasIcon = icon != null;
            var text = Ui.Text(faceImage.transform, "Label", label, fontSize, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            text.rectTransform.Stretch(20f, 0f, hasIcon ? size.y : 20f, 0f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.characterSpacing = 2f;
            Outline(text, edge, 0.2f, 0.6f);

            if (hasIcon)
            {
                var iconRect = icon(faceImage.rectTransform, edge);
                // Pivot ortada: dönen dişli kendi merkezi etrafında dönsün
                float d = size.y * 0.6f;
                iconRect.Place(new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-size.y * 0.22f - d / 2f, 0f), new Vector2(d, d));
            }

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = faceImage;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(PlayClick);

            var fx = root.gameObject.AddComponent<MenuButtonFx>();
            fx.Hovered += PlayHover;
            return root;
        }

        static RectTransform PlayIcon(RectTransform parent, Color edge)
        {
            var circle = Ui.Image(parent, "Icon", Color.white, MenuArt.Circle, raycast: false);
            circle.type = Image.Type.Simple;
            var triangle = Ui.Image(circle.transform, "Play", edge, MenuArt.Play, raycast: false);
            triangle.type = Image.Type.Simple;
            triangle.rectTransform.Stretch(8f, 8f, 4f, 8f);
            return circle.rectTransform;
        }

        static RectTransform GearIcon(RectTransform parent, Color edge)
        {
            var gear = Ui.Image(parent, "Icon", Color.white, MenuArt.Gear, raycast: false);
            gear.type = Image.Type.Simple;
            gear.gameObject.AddComponent<Spin>();
            return gear.rectTransform;
        }

        static void Outline(TextMeshProUGUI text, Color color, float width, float shadow)
        {
            text.outlineColor = color;
            text.outlineWidth = width;
            var mat = text.fontMaterial;
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.6f));
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, shadow * 0.6f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -shadow);
            mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, width);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.15f);
        }

        // ---------------------------------------------------------------- Akış

        IEnumerator Intro()
        {
            var starts = new Vector2[menuButtons.Length];
            for (int i = 0; i < menuButtons.Length; i++)
                starts[i] = menuButtons[i].anchoredPosition;

            for (float t = 0f; t < 1.1f; t += Time.unscaledDeltaTime)
            {
                fade.color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01(t / 0.5f));

                float l = EaseOutBack(Mathf.Clamp01((t - 0.1f) / 0.55f));
                logo.localScale = Vector3.one * Mathf.LerpUnclamped(0.7f, 1f, l);

                for (int i = 0; i < menuButtons.Length; i++)
                {
                    float k = EaseOutCubic(Mathf.Clamp01((t - 0.25f - i * 0.1f) / 0.45f));
                    menuButtons[i].anchoredPosition = starts[i] + new Vector2((1f - k) * 700f, 0f);
                }
                yield return null;
            }

            fade.color = Color.clear;
            fade.raycastTarget = false;
            logo.localScale = Vector3.one;
            for (int i = 0; i < menuButtons.Length; i++)
                menuButtons[i].anchoredPosition = starts[i];
        }

        void StartGame()
        {
            if (leaving)
                return;
            GameAudio.Save();
            StartCoroutine(LoadGame());
        }

        IEnumerator LoadGame()
        {
            leaving = true;
            fade.raycastTarget = true;
            float startVolume = musicSource.volume;
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.6f;
                fade.color = new Color(0f, 0f, 0f, k);
                musicSource.volume = startVolume * (1f - k);
                yield return null;
            }
            fade.color = Color.black;

            if (!string.IsNullOrEmpty(gameScene))
            {
                SceneManager.LoadScene(gameScene);
                yield break;
            }

            // Aynı sahnede başlat: menüyü gizle, oyunu aç, karanlıktan aç
            foreach (Transform child in fade.transform.parent)
                if (child != fade.transform)
                    child.gameObject.SetActive(false);
            musicSource.Stop();
            EnableGame();
            yield return null;
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                fade.color = new Color(0f, 0f, 0f, 1f - t / 0.6f);
                yield return null;
            }
            Destroy(gameObject);
        }

        void EnableGame()
        {
            if (enableOnStart == null)
                return;
            foreach (var go in enableOnStart)
                if (go != null)
                    go.SetActive(true);
        }

        void Quit() => GameExit.Quit();

        void OpenSettings()
        {
            settings.Open();
            fade.transform.SetAsLastSibling();
        }

        void ApplyAudio()
        {
            if (!leaving)
                musicSource.volume = GameAudio.MusicVolume;
        }

        void PlayClick()
        {
            if (clickSound != null)
                sfxSource.PlayOneShot(clickSound, GameAudio.SfxVolume);
        }

        void PlayHover()
        {
            if (hoverSound != null)
                sfxSource.PlayOneShot(hoverSound, GameAudio.SfxVolume * 0.6f);
            else
                Sfx.Hover();
        }

        static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        /// <summary>Ayarlar butonundaki dişliyi yavaşça döndürür.</summary>
        class Spin : MonoBehaviour
        {
            void Update() => transform.Rotate(0f, 0f, -40f * Time.unscaledDeltaTime);
        }
    }
}
