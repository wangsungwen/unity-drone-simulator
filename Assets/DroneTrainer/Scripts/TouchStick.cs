using UnityEngine;
using UnityEngine.EventSystems;

namespace DroneTrainer
{
    public sealed class TouchStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform knob;
        public Vector2 Value { get; private set; }
        int pointer = int.MinValue;
        public void OnPointerDown(PointerEventData e)
        { if (pointer == int.MinValue) { pointer = e.pointerId; OnDrag(e); } }
        public void OnDrag(PointerEventData e)
        {
            if (pointer != e.pointerId) return;
            Vector2 position;
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out position)) return;
            float radius = rect.rect.width * .36f;
            Value = Vector2.ClampMagnitude((position - rect.rect.center) / radius, 1);
            knob.anchoredPosition = Value * radius;
        }
        public void OnPointerUp(PointerEventData e) { if (pointer == e.pointerId) Clear(); }
        public void Clear() { pointer = int.MinValue; Value = Vector2.zero; if (knob != null) knob.anchoredPosition = Vector2.zero; }
        void OnDisable() { Clear(); }
        void OnApplicationFocus(bool focus) { if (!focus) Clear(); }
        void OnApplicationPause(bool pause) { if (pause) Clear(); }
    }
}
