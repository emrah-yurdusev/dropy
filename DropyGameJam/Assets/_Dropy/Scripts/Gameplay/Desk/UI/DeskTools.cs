using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    public enum DeskTool
    {
        None,
        ApproveStamp,
        RejectStamp,
        RedPen
    }

    /// <summary>
    /// Masa araçları: onay damgası, red damgası ve kırmızı kalem.
    /// Araç seçilince imleci takip eder; sağ tık veya ESC bırakır.
    /// </summary>
    public class DeskTools : MonoBehaviour
    {
        static readonly Color TrayColor = new Color(0.2f, 0.17f, 0.15f, 0.92f);
        static readonly Color SlotColor = new Color(0.32f, 0.28f, 0.25f);
        static readonly Color SelectedColor = new Color(0.95f, 0.75f, 0.3f);

        RectTransform canvasRect;
        RectTransform ghostRoot;
        readonly Dictionary<DeskTool, Image> slots = new Dictionary<DeskTool, Image>();
        readonly Dictionary<DeskTool, RectTransform> ghosts = new Dictionary<DeskTool, RectTransform>();

        public DeskTool Active { get; private set; }

        public void Build(RectTransform trayParent, RectTransform canvasRoot)
        {
            canvasRect = canvasRoot;

            var tray = Ui.Image(trayParent, "ToolTray", TrayColor, Ui.Rounded).rectTransform;
            tray.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(560f, 92f));
            Ui.DropShadow(tray, 12f, new Vector2(0f, -4f), 0.35f);
            var layout = tray.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            AddSlot(tray, DeskTool.ApproveStamp, "ONAY\nDamgası", new Color(0.3f, 0.8f, 0.45f));
            AddSlot(tray, DeskTool.RejectStamp, "RED\nDamgası", new Color(0.95f, 0.4f, 0.35f));
            AddSlot(tray, DeskTool.RedPen, "Kırmızı\nKalem", new Color(1f, 0.55f, 0.5f));

            ghostRoot = Ui.Rect("ToolGhost", canvasRoot);
            ghostRoot.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            ghosts[DeskTool.ApproveStamp] = MakeStampGhost(StampKind.Approve);
            ghosts[DeskTool.RejectStamp] = MakeStampGhost(StampKind.Reject);
            ghosts[DeskTool.RedPen] = MakePenGhost();
            Select(DeskTool.None);
        }

        void AddSlot(RectTransform tray, DeskTool tool, string label, Color labelColor)
        {
            var button = Ui.Button(tray, tool.ToString(), label, SlotColor, labelColor, 20f);
            button.onClick.AddListener(() => Select(Active == tool ? DeskTool.None : tool));
            slots[tool] = (Image)button.targetGraphic;
        }

        RectTransform MakeStampGhost(StampKind kind)
        {
            var stamp = DocumentCard.CreateStampGraphic(ghostRoot, kind);
            stamp.localScale = Vector3.one * 0.9f;
            foreach (var g in stamp.GetComponentsInChildren<Graphic>())
            {
                g.raycastTarget = false;
                g.color = new Color(g.color.r, g.color.g, g.color.b, 0.55f);
            }
            return stamp;
        }

        RectTransform MakePenGhost()
        {
            var pen = Ui.Image(ghostRoot, "Pen", new Color(0.85f, 0.1f, 0.1f), Ui.Rounded, raycast: false).rectTransform;
            pen.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(90f, 14f));
            pen.localRotation = Quaternion.Euler(0f, 0f, 45f);
            return pen;
        }

        public void Select(DeskTool tool)
        {
            Active = tool;
            foreach (var pair in slots)
                pair.Value.color = pair.Key == tool ? SelectedColor : SlotColor;
            foreach (var pair in ghosts)
                pair.Value.gameObject.SetActive(pair.Key == tool);
        }

        void Update()
        {
            if (Active == DeskTool.None)
                return;

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if ((mouse != null && mouse.rightButton.wasPressedThisFrame) || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame))
            {
                Select(DeskTool.None);
                return;
            }
            if (mouse == null)
                return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mouse.position.ReadValue(), null, out var local);
            ghostRoot.anchoredPosition = local;
            ghostRoot.SetAsLastSibling();
        }

        void OnDisable() => Select(DeskTool.None);
    }
}
