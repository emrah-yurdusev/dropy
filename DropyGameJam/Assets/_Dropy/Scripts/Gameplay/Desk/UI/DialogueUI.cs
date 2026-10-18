using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>
    /// NPC'nin kafasının üstünde konuşma balonu (daktilo efektli) ve
    /// ekranın altında oyuncunun seçebileceği cevap balonları.
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        static readonly Color BubbleColor = new Color(1f, 1f, 0.97f);
        static readonly Color TextColor = new Color(0.12f, 0.12f, 0.14f);
        static readonly Color NameColor = new Color(0.75f, 0.25f, 0.15f);
        static readonly Color ChoiceColor = new Color(0.17f, 0.36f, 0.62f);

        [SerializeField, Min(1f)] float charsPerSecond = 70f;
        /// <summary>Balonun giremeyeceği üst şerit (referans çözünürlükte piksel).</summary>
        const float TopReserved = 170f;
        /// <summary>Yandaki balonla başın arası (referans çözünürlükte piksel).</summary>
        const float SideGap = 80f;

        /// <summary>Konuşanın yazı hızı çarpanı (müteahhidin kişiliğinden; telefon ve müdür için 1).</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>Balon üst şeride (gün göstergesi, harç bildirimi) girmesin: ayakta yürüyen müteahhidin tepkisi için.</summary>
        public bool KeepClearOfTop { get; set; }
        [Tooltip("Balonun kafanın ne kadar üstünde duracağı (metre)")]
        [SerializeField] float headOffset = 0.32f;

        RectTransform canvasRect;
        Camera worldCamera;
        RectTransform bubble;
        TextMeshProUGUI nameText;
        TextMeshProUGUI bodyText;
        RectTransform continueHint;
        RectTransform tail;
        RectTransform choiceRoot;
        Transform anchor;
        /// <summary>Balon başın yanına geçtiyse hangi tarafta durduğu (-1 sol, 1 sağ, 0 henüz yanda değil). Cümle bitene kadar sabit.</summary>
        int lockedSide;
        /// <summary>Yeni cümlede balon yerine atlasın; sonra kafayı yumuşakça takip etsin.</summary>
        bool snapNext = true;

        public bool IsTalking => bubble != null && bubble.gameObject.activeSelf;

        public void Build(RectTransform root, Camera camera)
        {
            canvasRect = root;
            worldCamera = camera;

            bubble = Ui.Image(root, "NpcBubble", BubbleColor, Ui.Rounded).rectTransform;
            bubble.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(460f, 100f));
            Ui.DropShadow(bubble, 6f, new Vector2(0f, -4f), 0.18f);
            var layout = bubble.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 16, 22);
            layout.spacing = 4f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            bubble.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            tail = Ui.Image(bubble, "Tail", BubbleColor, raycast: false).rectTransform;
            tail.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            tail.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(26f, 26f));
            tail.localRotation = Quaternion.Euler(0f, 0f, 45f);

            nameText = Ui.Text(bubble, "Name", "", 20f, NameColor, style: FontStyles.Bold);
            bodyText = Ui.Text(bubble, "Body", "", 26f, TextColor);

            continueHint = Ui.Image(bubble, "Continue", NameColor, raycast: false).rectTransform;
            continueHint.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            continueHint.Place(new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-22f, 16f), new Vector2(10f, 10f));
            continueHint.localRotation = Quaternion.Euler(0f, 0f, 45f);

            choiceRoot = Ui.Rect("Choices", root);
            choiceRoot.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(720f, 0f));
            var choiceLayout = choiceRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            choiceLayout.spacing = 10f;
            choiceLayout.childControlHeight = true;
            choiceLayout.childControlWidth = true;
            choiceLayout.childForceExpandHeight = false;
            choiceRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            bubble.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (anchor == null || !bubble.gameObject.activeSelf)
                return;
            var screen = worldCamera.WorldToScreenPoint(anchor.position + Vector3.up * headOffset);
            if (screen.z < 0f)
                return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
            var half = canvasRect.rect.size * 0.5f;
            float w = bubble.rect.width * 0.5f;
            float h = bubble.rect.height;
            // Balon başın üstüne sığmıyorsa (uzun metin ya da ayaktaki müteahhit) başa binmez, başın yanına geçer;
            // yandayken üst şeride (gün göstergesi, harç bildirimi) de girmez.
            float topMargin = KeepClearOfTop ? TopReserved : 16f;
            // Bir kez yana geçen balon o cümle boyunca aynı yanda kalır: müteahhit kapıya yürürken kafası ekranın
            // ortasından geçince balon sağa sola zıplamasın.
            bool beside = lockedSide != 0 || local.y > half.y - h - topMargin;
            if (beside)
            {
                if (lockedSide == 0)
                    lockedSide = local.x > 0f ? -1 : 1;
                local.x += lockedSide * (w + SideGap);
                local.y = Mathf.Clamp(local.y - h * 0.6f, -half.y + 16f, half.y - h - TopReserved);
            }
            local.x = Mathf.Clamp(local.x, -half.x + w + 16f, half.x - w - 16f);
            local.y = Mathf.Clamp(local.y, -half.y + 16f, half.y - h - topMargin);
            tail.gameObject.SetActive(!beside);
            // Kafayı yumuşak takip: yürüme sallantısı balona geçmesin.
            if (snapNext)
            {
                bubble.anchoredPosition = local;
                snapNext = false;
            }
            else
                bubble.anchoredPosition = Vector2.Lerp(bubble.anchoredPosition, local, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            continueHint.localScale = Vector3.one * (0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 6f));
        }

        /// <summary>NPC'yi konuşturur. waitForClick ise yazı bitince tıklama bekler.</summary>
        public IEnumerator Say(string speaker, string text, Transform head, bool waitForClick)
        {
            anchor = head;
            lockedSide = 0;
            snapNext = true;
            if (head == null)
                bubble.anchoredPosition = new Vector2(0f, 60f); // kafası olmayan konuşmacı (telefon): ekranın ortası
            tail.gameObject.SetActive(head != null);
            nameText.text = speaker;
            bodyText.text = text;
            bodyText.maxVisibleCharacters = 0;
            continueHint.gameObject.SetActive(false);
            bubble.gameObject.SetActive(true);
            bubble.localScale = Vector3.one * 0.85f;
            LateUpdate();

            bodyText.ForceMeshUpdate();
            int total = bodyText.textInfo.characterCount;
            float shown = 0f;
            yield return null;
            while (shown < total)
            {
                bubble.localScale = Vector3.Lerp(bubble.localScale, Vector3.one, 0.3f);
                if (AdvancePressed())
                    shown = total;
                shown += charsPerSecond * SpeedMultiplier * Time.deltaTime;
                bodyText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
                yield return null;
            }
            bubble.localScale = Vector3.one;
            bodyText.maxVisibleCharacters = total;

            if (!waitForClick)
                yield break;
            continueHint.gameObject.SetActive(true);
            yield return null;
            while (!AdvancePressed())
                yield return null;
        }

        public void HideBubble()
        {
            bubble.gameObject.SetActive(false);
            anchor = null;
            lockedSide = 0;
            snapNext = true;
        }

        /// <summary>Balonu gizler ve yarıda kalmış cevap seçeneklerini siler.</summary>
        public void Clear()
        {
            HideBubble();
            foreach (Transform child in choiceRoot)
                Destroy(child.gameObject);
        }

        /// <summary>Cevap balonlarını gösterir, oyuncu birini seçene kadar bekler.</summary>
        public IEnumerator Choose(IList<string> options, System.Action<int> onPicked)
        {
            int picked = -1;
            var buttons = new List<GameObject>();
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                var button = Ui.Button(choiceRoot, $"Choice{i}", $"{i + 1}.  {options[i]}", ChoiceColor, Color.white, 24f);
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.Left;
                label.fontStyle = FontStyles.Normal;
                var element = button.gameObject.AddComponent<LayoutElement>();
                element.minHeight = 58f;
                element.preferredHeight = Mathf.Max(58f, label.GetPreferredValues(options[i], 660f, 0f).y + 24f);
                Ui.DropShadow((RectTransform)button.transform, 5f, new Vector2(0f, -3f), 0.18f);
                button.onClick.AddListener(() => picked = index);
                buttons.Add(button.gameObject);
            }

            while (picked < 0)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && Time.timeScale > 0f)
                {
                    for (int i = 0; i < options.Count && i < 9; i++)
                    {
                        if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
                            picked = i;
                    }
                }
                yield return null;
            }

            foreach (var b in buttons)
                Destroy(b);
            onPicked?.Invoke(picked);
        }

        static bool AdvancePressed()
        {
            if (Time.timeScale == 0f) // duraklatma menüsü açık
                return false;
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            return (mouse != null && mouse.leftButton.wasPressedThisFrame)
                || (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame));
        }
    }
}
