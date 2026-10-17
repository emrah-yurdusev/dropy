using UnityEngine;
using UnityEngine.EventSystems;

namespace Dropy.Menu
{
    /// <summary>Butona üzerine gelince büyüme, basınca küçülme ve isteğe bağlı nefes alma efekti verir.</summary>
    public class MenuButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float hoverScale = 1.05f;
        public float pressedScale = 0.96f;
        [Tooltip("Ana butonun dikkat çekmesi için hafif büyüyüp küçülmesi")]
        public bool pulse;

        public event System.Action Hovered;

        bool hovered;
        bool pressed;

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            Hovered?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData) => hovered = pressed = false;
        public void OnPointerDown(PointerEventData eventData) => pressed = true;
        public void OnPointerUp(PointerEventData eventData) => pressed = false;

        void OnDisable()
        {
            hovered = pressed = false;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            float target = pressed ? pressedScale : hovered ? hoverScale : 1f;
            if (pulse && !hovered && !pressed)
                target += Mathf.Sin(Time.unscaledTime * 3f) * 0.02f;
            float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
