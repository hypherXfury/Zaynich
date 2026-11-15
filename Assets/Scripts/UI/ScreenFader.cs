using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen black fade overlay, built procedurally on first use so no manual
/// Canvas/Image scene setup is required. Used for in-scene transitions such as
/// entering/exiting a Chamber arena.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    private static ScreenFader s_instance;

    public static ScreenFader Instance
    {
        get
        {
            if (s_instance == null)
            {
                var go = new GameObject("ScreenFader");
                s_instance = go.AddComponent<ScreenFader>();
            }
            return s_instance;
        }
    }

    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }
        s_instance = this;

        BuildOverlay();
    }

    private void BuildOverlay()
    {
        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;   // draw above all other UI

        var imageGO = new GameObject("FadeImage", typeof(Image));
        imageGO.transform.SetParent(canvasGO.transform, false);

        var image = imageGO.GetComponent<Image>();
        image.color         = Color.black;
        image.raycastTarget = false;

        RectTransform rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _canvasGroup = imageGO.AddComponent<CanvasGroup>();
        _canvasGroup.alpha          = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable   = false;
    }

    /// <summary>Fades the screen to black over <paramref name="duration"/> seconds.</summary>
    public IEnumerator FadeOut(float duration) => Fade(0f, 1f, duration);

    /// <summary>Fades the screen from black back to clear over <paramref name="duration"/> seconds.</summary>
    public IEnumerator FadeIn(float duration) => Fade(1f, 0f, duration);

    private IEnumerator Fade(float from, float to, float duration)
    {
        _canvasGroup.alpha = from;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, duration > 0f ? elapsed / duration : 1f);
            yield return null;
        }

        _canvasGroup.alpha = to;
    }
}
