using System.Collections;
using System.Collections.Generic;
using TMPro;
using Dropy.Menu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>
    /// F1 geliştirici paneli: dosya/gün atlama, ara sahneleri oynatma, kasa/sabitleme/aile bağı hileleri, hız,
    /// sahne yükleme. Kendiliğinden kurulur (sahneye eklemeye gerek yok). Yayın derlemesinde de vardır ki jüri farklı
    /// sonları deneyebilsin; F1 yalnız ayarlardan açıksa çalışır (Esc menüsündeki "Geliştirici paneli" anahtarı).
    /// </summary>
    public class DevPanel : MonoBehaviour
    {
        static readonly Color Background = new Color(0.08f, 0.09f, 0.11f, 0.94f);
        static readonly Color ButtonColor = new Color(0.22f, 0.25f, 0.30f);
        static readonly Color AccentColor = new Color(0.78f, 0.45f, 0.18f);
        static readonly Color DangerColor = new Color(0.62f, 0.20f, 0.18f);
        static readonly string[] ModeNames = { "Doğru mühür", "Hepsine onay", "Hepsine red" };
        const float FastScale = 3f;

        GameObject panel;
        TextMeshProUGUI info;
        TextMeshProUGUI modeLabel;
        TextMeshProUGUI targetLabel;
        TextMeshProUGUI fixingLabel;
        TextMeshProUGUI speedLabel;
        string notice = "";
        float noticeUntil;
        float nextRefresh;

        DeskManager.DebugSeal mode = DeskManager.DebugSeal.Correct;
        int targetIndex;
        bool fast;
        GameObject ownEventSystem;
        bool wasAllowed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindFirstObjectByType<DevPanel>() != null)
                return;
            var go = new GameObject("DevPanel");
            DontDestroyOnLoad(go);
            go.AddComponent<DevPanel>();
        }

        static DeskManager Desk => FindFirstObjectByType<DeskManager>();

        void Awake()
        {
            Build();
            panel.SetActive(false);
            SceneManager.sceneLoaded += OnSceneLoaded;
            Dropy.Core.GameSettings.Changed += OnSettingsChanged;
            wasAllowed = Dropy.Core.GameSettings.DevPanel;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Dropy.Core.GameSettings.Changed -= OnSettingsChanged;
        }

        /// <summary>Anahtar açılınca panel hemen görünür (nerede olduğu anlaşılsın), kapanınca gizlenir.</summary>
        void OnSettingsChanged()
        {
            bool allowed = Dropy.Core.GameSettings.DevPanel;
            if (allowed != wasAllowed)
                SetOpen(allowed);
            wasAllowed = allowed;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame && Dropy.Core.GameSettings.DevPanel)
                SetOpen(!panel.activeSelf);
            if (panel.activeSelf && Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + 0.2f;
                Refresh();
            }
        }

        void SetOpen(bool open)
        {
            panel.SetActive(open);
            if (!open)
                return;
            EnsureEventSystem();
            var desk = Desk;
            if (desk != null)
                targetIndex = Mathf.Clamp(desk.DebugFileIndex + 1, 0, desk.DebugFileCount - 1);
            Refresh();
        }

        // ---------------------------------------------------------------- arayüz

        void Build()
        {
            var canvasGo = new GameObject("DevCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var bg = Ui.Image(canvasGo.transform, "Panel", Background, Ui.Rounded);
            bg.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(460f, 1000f));
            panel = bg.gameObject;
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 16, 16);
            layout.spacing = 8f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            Label("GELİŞTİRİCİ PANELİ  ·  F1", 24f, FontStyles.Bold, Color.white, 34f);
            info = Label("", 17f, FontStyles.Normal, new Color(0.85f, 0.88f, 0.92f), 118f);

            Header("ATLAMA");
            modeLabel = Row(("", ButtonColor, CycleMode))[0];
            Row(("Bu dosyayı geç", AccentColor, () => Jump(+1)), ("Sonraki güne", AccentColor, NextDay));
            var picker = Row(("<", ButtonColor, () => StepTarget(-1)), ("", ButtonColor, null), (">", ButtonColor, () => StepTarget(+1)), ("Git", AccentColor, JumpToTarget));
            targetLabel = picker[1];
            // Tek deprem: son günün sonu (önce deprem, sonra gazete ya da hapis).
            Row(($"{Economy.Days}. gün sonu: deprem", DangerColor, () => JumpAbsolute(int.MaxValue)));

            Header("ARA SAHNELER");
            Row(("Deprem sahnesi", ButtonColor, () => PlayCinematic(prison: false)), ("Gözaltı sahnesi", ButtonColor, () => PlayCinematic(prison: true)));

            Header("DURUM");
            fixingLabel = Row(("+50.000 TL", ButtonColor, () => WithDesk(d => d.DebugAddMoney(50000))), ("", ButtonColor, ToggleFixing))[1];
            speedLabel = Row(("Aile bağını doldur", ButtonColor, () => WithDesk(d => d.DebugFillBond())), ("", ButtonColor, ToggleSpeed))[1];

            Header("SAHNELER");
            Row(("main", ButtonColor, () => Load("main")), ("Deprem", ButtonColor, () => Load(EarthquakeCinematic.SceneName)), ("Gözaltı", ButtonColor, () => Load(PrisonCinematic.SceneName)));
            Row(("Oyunu baştan başlat", DangerColor, () => Load("main")));
        }

        TextMeshProUGUI Label(string text, float size, FontStyles style, Color color, float height)
        {
            var label = Ui.Text(panel.transform, "Label", text, size, color, TextAlignmentOptions.TopLeft, style);
            label.richText = true;
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return label;
        }

        void Header(string text) =>
            Label(text, 15f, FontStyles.Bold, new Color(1f, 0.75f, 0.45f), 24f).margin = new Vector4(0f, 8f, 0f, 0f);

        /// <summary>Yan yana düğmeler; etiketlerini döner (sonradan güncellenebilsin diye).</summary>
        TextMeshProUGUI[] Row(params (string label, Color color, System.Action onClick)[] buttons)
        {
            var row = Ui.Rect("Row", panel.transform);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            var labels = new TextMeshProUGUI[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                var (label, color, onClick) = buttons[i];
                var button = Ui.Button(row, "Button", label, color, Color.white, 17f);
                if (onClick != null)
                    button.onClick.AddListener(() => onClick());
                else
                    button.interactable = false;
                if (label.Length <= 1)
                    button.gameObject.AddComponent<LayoutElement>().preferredWidth = 46f;
                else
                    button.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                labels[i] = button.GetComponentInChildren<TextMeshProUGUI>();
            }
            return labels;
        }

        void Refresh()
        {
            var desk = Desk;
            var active = SceneManager.GetActiveScene().name;
            string state;
            if (desk == null)
                state = "<color=#ffb070>Masa akışı yok.</color> Atlama düğmeleri main sahnesinde oyunu başlatır.";
            else
            {
                int i = desk.DebugFileIndex;
                int n = desk.DebugFileCount;
                state = i >= n
                    ? $"Hafta bitti  ·  {n}/{n} dosya"
                    : $"Gün {Economy.Day(i)}  ·  Dosya {i % Economy.FilesPerDay + 1}/{Economy.FilesPerDay}  ({i + 1}/{n})";
                state += $"\nKasa {Ui.Money(desk.Money)}  ·  Aile bağı {desk.DebugBond}" +
                         $"\nÇöken bina {desk.DebugCollapsedCount}  ·  Sabitleme {(desk.DebugHasFixing ? "var" : "yok")}";
            }
            if (AdditiveCinematic.IsPlaying)
                state += "\n<color=#ffb070>Ara sahne oynuyor</color>";
            string note = Time.unscaledTime < noticeUntil ? $"\n<color=#9fd3ff>{notice}</color>" : "";
            info.text = $"Sahne: <b>{active}</b>\n{state}{note}";

            modeLabel.text = $"Atlanan dosyalara: <b>{ModeNames[(int)mode]}</b>";
            targetLabel.text = $"Gün {Economy.Day(targetIndex)} · D{targetIndex % Economy.FilesPerDay + 1}";
            fixingLabel.text = desk != null && desk.DebugHasFixing ? "Sabitleme: VAR" : "Sabitleme: YOK";
            speedLabel.text = fast ? $"Hız: {FastScale:0}x" : "Hız: 1x";
        }

        void Notify(string message)
        {
            notice = message;
            noticeUntil = Time.unscaledTime + 3f;
            nextRefresh = 0f;
        }

        // ---------------------------------------------------------------- eylemler

        void CycleMode() => mode = (DeskManager.DebugSeal)(((int)mode + 1) % ModeNames.Length);

        void StepTarget(int delta)
        {
            int count = Desk != null ? Desk.DebugFileCount : Economy.Days * Economy.FilesPerDay;
            targetIndex = Mathf.Clamp(targetIndex + delta, 0, count - 1);
        }

        void Jump(int delta) => WithDesk(d => DoJump(d, d.DebugFileIndex + delta));

        void NextDay() => WithDesk(d =>
        {
            int i = d.DebugFileIndex;
            DoJump(d, (i / Economy.FilesPerDay + 1) * Economy.FilesPerDay);
        });

        void JumpToTarget() => WithDesk(d => DoJump(d, targetIndex));

        void JumpAbsolute(int index) => WithDesk(d => DoJump(d, Mathf.Min(index, d.DebugFileCount)));

        void DoJump(DeskManager desk, int index)
        {
            if (!desk.DebugCanJump)
            {
                Notify("Şu an atlanamaz (ara sahne oynuyor ya da hafta bitti).");
                return;
            }
            if (index < desk.DebugFileIndex)
            {
                Notify("Geri gidilemez; baştan başlatıp ileri atlayın.");
                return;
            }
            desk.DebugJumpTo(index, mode);
            Notify(index >= desk.DebugFileCount ? $"{Economy.Days}. günün sonuna atlandı." : $"Atlandı: Gün {Economy.Day(index)} · Dosya {index % Economy.FilesPerDay + 1}");
        }

        void ToggleFixing() => WithDesk(d => d.DebugSetFixing(!d.DebugHasFixing));

        void ToggleSpeed()
        {
            fast = !fast;
            Time.timeScale = fast ? FastScale : 1f;
        }

        void Load(string sceneName)
        {
            if (AdditiveCinematic.IsPlaying)
            {
                Notify("Ara sahne bitmeden sahne değiştirilemez.");
                return;
            }
            fast = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }

        void PlayCinematic(bool prison)
        {
            if (AdditiveCinematic.IsPlaying)
                return;
            string sceneName = prison ? PrisonCinematic.SceneName : EarthquakeCinematic.SceneName;
            if (SceneManager.GetSceneByName(sceneName).isLoaded)
            {
                // Sahne zaten açıksa üstüne bir kopyasını yüklemek yerine baştan aç.
                Load(sceneName);
                return;
            }
            StartCoroutine(RunCinematic(prison));
        }

        IEnumerator RunCinematic(bool prison)
        {
            panel.SetActive(false);
            var desk = Desk;
            IList<string> captions = desk != null ? (prison ? desk.DebugPrisonCaptions : desk.DebugQuakeCaptions) : null;
            if (captions == null || captions.Count == 0)
                captions = new List<string> { prison ? "(Test) Hapishane sahnesi" : "(Test) Deprem sahnesi" };
            if (desk != null)
                desk.DebugCinematicPlaying = true;
            yield return prison ? PrisonCinematic.Play(captions) : EarthquakeCinematic.Play(captions);
            if (desk != null)
                desk.DebugCinematicPlaying = false;
        }

        /// <summary>
        /// Masa akışı açıksa eylemi hemen yapar. main sahnesinde ana menü açıksa önce "Oyuna Başla"ya basar,
        /// masa açılınca eylemi yapar; masa hiç yoksa uyarır.
        /// </summary>
        void WithDesk(System.Action<DeskManager> action)
        {
            var desk = Desk;
            if (desk != null)
            {
                action(desk);
                nextRefresh = 0f;
                return;
            }
            var menu = FindFirstObjectByType<MainMenu>();
            if (menu == null)
            {
                Notify("Bu sahnede masa akışı yok; önce main sahnesini açın.");
                return;
            }
            StartCoroutine(StartGameThen(menu, action));
        }

        IEnumerator StartGameThen(MainMenu menu, System.Action<DeskManager> action)
        {
            Notify("Oyun başlatılıyor...");
            menu.SendMessage("StartGame", SendMessageOptions.DontRequireReceiver);
            DeskManager desk = null;
            for (float t = 0f; t < 5f && desk == null; t += Time.unscaledDeltaTime)
            {
                desk = Desk;
                yield return null;
            }
            if (desk == null)
            {
                Notify("Masa akışı açılmadı.");
                yield break;
            }
            yield return null; // DeskManager.Start haftayı başlatsın, sonra kesip atlayalım
            action(desk);
            nextRefresh = 0f;
        }

        // ---------------------------------------------------------------- EventSystem

        /// <summary>Sahnede EventSystem yoksa (ör. Deprem/Hapishane) panel tıklanabilsin diye bir tane kurar.</summary>
        void EnsureEventSystem()
        {
            if (EventSystem.current != null || ownEventSystem != null)
                return;
            ownEventSystem = new GameObject("DevEventSystem", typeof(EventSystem));
            ownEventSystem.transform.SetParent(transform, false);
            ownEventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            // Yeni sahne kendi EventSystem'ini getirdiyse bizimkini kaldır (ikisi birden uyarı verir).
            if (ownEventSystem != null && FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length > 1)
            {
                Destroy(ownEventSystem);
                ownEventSystem = null;
            }
            if (scene.name == "main" && loadMode == LoadSceneMode.Single)
            {
                fast = false;
                Time.timeScale = 1f;
            }
        }
    }
}
