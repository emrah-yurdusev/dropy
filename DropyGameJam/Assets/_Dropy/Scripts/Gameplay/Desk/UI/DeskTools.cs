using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Sağdaki mühürler: Onay, Red, Perde ekle. Her mührün üstünde kazandıracağı para yazar.
    /// Mühür seçilince imleci takip eder; sağ tık bırakır (ESC'yi DeskManager yönetir).
    /// </summary>
    public class DeskTools : MonoBehaviour
    {
        static readonly Color TrayColor = new Color(0.2f, 0.17f, 0.15f, 0.92f);
        static readonly Color SlotColor = new Color(0.32f, 0.28f, 0.25f);
        static readonly Color SelectedColor = new Color(0.95f, 0.75f, 0.3f);

        class Slot
        {
            public Button button;
            public Image image;
            public TextMeshProUGUI label;
            public string title;
        }

        RectTransform canvasRect;
        RectTransform ghostRoot;
        readonly Dictionary<Seal, Slot> slots = new Dictionary<Seal, Slot>();
        readonly Dictionary<Seal, RectTransform> ghosts = new Dictionary<Seal, RectTransform>();

        public Seal Active { get; private set; }

        /// <summary>Mühür tepsisindeki düğme (öğretici ok çizerken hedef alır).</summary>
        public RectTransform SlotRect(Seal seal) => slots.TryGetValue(seal, out var slot) ? (RectTransform)slot.button.transform : null;

        /// <summary>Eldeki mühür değişti (None = bırakıldı). İmleç buna göre damgaya döner.</summary>
        public event System.Action<Seal> ActiveChanged;

        public void Build(RectTransform trayParent, RectTransform canvasRoot)
        {
            canvasRect = canvasRoot;

            var tray = Ui.Image(trayParent, "SealTray", TrayColor, Ui.Rounded).rectTransform;
            tray.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(600f, 104f));
            Ui.DropShadow(tray, 12f, new Vector2(0f, -4f), 0.35f);
            var layout = tray.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            AddSlot(tray, Seal.Approve, "ONAY", new Color(0.3f, 0.8f, 0.45f));
            AddSlot(tray, Seal.Reject, "RED", new Color(0.95f, 0.4f, 0.35f));
            AddSlot(tray, Seal.ShearWall, "PERDE EKLE", new Color(0.45f, 0.7f, 1f));

            ghostRoot = Ui.Rect("SealGhost", canvasRoot);
            ghostRoot.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            foreach (var seal in slots.Keys)
                ghosts[seal] = MakeStampGhost(seal);
            Select(Seal.None);
        }

        void AddSlot(RectTransform tray, Seal seal, string title, Color labelColor)
        {
            var button = Ui.Button(tray, seal.ToString(), title, SlotColor, labelColor, 19f);
            ButtonClickSound.Skip(button);
            button.onClick.AddListener(() => Select(Active == seal ? Seal.None : seal));
            slots[seal] = new Slot
            {
                button = button,
                image = button.GetComponent<Image>(),
                label = button.GetComponentInChildren<TextMeshProUGUI>(),
                title = title
            };
        }

        /// <summary>Mührün altındaki küçük yazıyı (tutar, "harç yok"...) ve kullanılabilirliğini günceller.</summary>
        public void SetSeal(Seal seal, string caption, bool available)
        {
            var slot = slots[seal];
            slot.label.text = $"{slot.title}\n<size=80%>{caption}</size>";
            slot.button.interactable = available;
            if (!available && Active == seal)
                Select(Seal.None);
        }

        RectTransform MakeStampGhost(Seal seal)
        {
            var stamp = DocumentCard.CreateStampGraphic(ghostRoot, seal);
            stamp.localScale = Vector3.one * 0.9f;
            foreach (var g in stamp.GetComponentsInChildren<Graphic>())
            {
                g.raycastTarget = false;
                g.color = new Color(g.color.r, g.color.g, g.color.b, 0.55f);
            }
            return stamp;
        }

        public void Select(Seal seal)
        {
            if (seal != Active)
            {
                if (seal == Seal.None)
                    Sfx.StampDrop();
                else
                    Sfx.StampPick();
            }
            Active = seal;
            foreach (var pair in slots)
                pair.Value.image.color = pair.Key == seal ? SelectedColor : SlotColor;
            foreach (var pair in ghosts)
                pair.Value.gameObject.SetActive(pair.Key == seal);
            ActiveChanged?.Invoke(seal);
        }

        void Update()
        {
            if (Active == Seal.None)
                return;

            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                Select(Seal.None);
                return;
            }
            if (mouse == null)
                return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mouse.position.ReadValue(), null, out var local);
            ghostRoot.anchoredPosition = local;
            ghostRoot.SetAsLastSibling();
        }

        void OnDisable() => Select(Seal.None);
    }
}
