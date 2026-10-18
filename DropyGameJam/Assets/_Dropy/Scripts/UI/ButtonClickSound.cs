using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Oyun açılınca kendini kurar; hangi sahnede olursa olsun etkileşimli bir butona
    /// basıldığında Sfx.Click çalar. Butonlara tek tek ses bağlamak gerekmez.
    /// Kendi sesi olan butonlar (ödeme, sayfa sekmesi, çekiç...) <see cref="Skip"/> ile işaretlenir.
    /// </summary>
    public class ButtonClickSound : MonoBehaviour
    {
        readonly List<RaycastResult> hits = new List<RaycastResult>();

        /// <summary>Bu buton blup çalmaz; sesini kendisi çalar.</summary>
        public static void Skip(Button button) => button.gameObject.AddComponent<OwnSound>();

        class OwnSound : MonoBehaviour { }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var go = new GameObject("ButtonClickSound");
            DontDestroyOnLoad(go);
            go.AddComponent<ButtonClickSound>();
        }

        void Update()
        {
            var mouse = Mouse.current;
            var events = EventSystem.current;
            if (mouse == null || events == null || !mouse.leftButton.wasPressedThisFrame)
                return;

            var pointer = new PointerEventData(events) { position = mouse.position.ReadValue() };
            hits.Clear();
            events.RaycastAll(pointer, hits);
            if (hits.Count == 0)
                return;

            // En üstteki hedef bir butonun parçasıysa ve buton basılabilir durumdaysa
            var button = hits[0].gameObject.GetComponentInParent<Button>();
            if (button != null && button.IsInteractable() && !button.TryGetComponent<OwnSound>(out _))
                Sfx.Click();
        }
    }
}
