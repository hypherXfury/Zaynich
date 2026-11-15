using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Fixed on-screen joystick for mobile top-down input.
/// Attach this to the joystick background RectTransform.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class Joystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private const float DefaultDeadZone = 0.1f;

    [SerializeField] private RectTransform _handle;
    [SerializeField] [Range(0f, 1f)] private float _deadZone = DefaultDeadZone;

    /// <summary>Normalized input direction in the range [-1, 1] per axis. Zero when idle or inside the dead zone.</summary>
    public Vector2 Direction { get; private set; }

    private RectTransform _background;
    private Canvas _canvas;
    private Camera _canvasCamera;

    private void Awake()
    {
        _background = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        _canvasCamera = _canvas.renderMode == RenderMode.ScreenSpaceCamera ? _canvas.worldCamera : null;
    }

    public void OnPointerDown(PointerEventData eventData) => ProcessInput(eventData);

    public void OnDrag(PointerEventData eventData) => ProcessInput(eventData);

    public void OnPointerUp(PointerEventData eventData)
    {
        Direction = Vector2.zero;
        _handle.anchoredPosition = Vector2.zero;
    }

    private void ProcessInput(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _background, eventData.position, _canvasCamera, out Vector2 localPoint);

        // The background pivot is (0, 0) = bottom-left corner, so localPoint origin
        // is at the corner rather than the center. Subtract the center offset (radius)
        // to make (0, 0) correspond to the joystick center before normalizing.
        Vector2 radius = _background.sizeDelta * 0.5f;
        Vector2 input = (localPoint - radius) / radius;

        if (input.magnitude > 1f)
            input = input.normalized;

        _handle.anchoredPosition = input * radius;
        Direction = input.magnitude > _deadZone ? input : Vector2.zero;
    }
}
