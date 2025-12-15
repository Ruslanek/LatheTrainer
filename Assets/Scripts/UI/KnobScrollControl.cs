using UnityEngine;
using UnityEngine.EventSystems;

namespace LatheTrainer.UI
{
    public class KnobScrollControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private SpindleFeedPanel panel;
        [SerializeField] private bool isSpindleKnob = true;

        [Header("Scroll sensitivity")]
        [SerializeField] private float scrollSpeed = 0.08f; //czułość pokrętła – określa, jak szybko zmienia się wartość (0..1)

        private bool _pressed;
        private bool _hover;

        private void Reset()
        {
            // automatyczne wyszukiwanie panelu w obiektach nadrzędnych
            panel = GetComponentInParent<SpindleFeedPanel>();
        }

        private void Update()
        {
            if (!panel) return;

            // działamy tylko wtedy, gdy kursor znajduje się nad pokrętłem i lewy przycisk myszy jest wciśnięty
            if (!_pressed || !_hover) return;

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) < 0.001f) return;

            float delta = wheel * scrollSpeed;

            if (isSpindleKnob)
                panel.spindleValue = Mathf.Clamp01(panel.spindleValue + delta);
            else
                panel.feedValue = Mathf.Clamp01(panel.feedValue + delta);

            // panel samodzielnie zaktualizuje interfejs w metodzie Update()
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                _pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                _pressed = false;
        }

        public void OnPointerEnter(PointerEventData eventData) => _hover = true;
        public void OnPointerExit(PointerEventData eventData) => _hover = false;
    }
}