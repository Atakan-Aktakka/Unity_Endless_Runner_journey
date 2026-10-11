using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

// Press feedback for any UI button: shrinks while held, overshoots on release.
// Unscaled time, so it also works on pause / game over panels (Time.timeScale = 0).
public class UIButtonPunch : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private float pressedScale = 0.9f;
    [SerializeField] private float releaseOvershoot = 1.08f;
    [SerializeField] private float pressDuration = 0.06f;
    [SerializeField] private float releaseDuration = 0.18f;

    private RectTransform rect;
    private Vector3 baseScale;
    private Coroutine routine;
    private bool pressed;

    private void Awake()
    {
        rect = (RectTransform)transform;
        baseScale = rect.localScale;
    }

    private void OnDisable()
    {
        if (rect != null) rect.localScale = baseScale;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        Play(ScaleTo(baseScale * pressedScale, pressDuration));
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pressed) return;
        pressed = false;
        Play(Release());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!pressed) return;
        pressed = false;
        Play(ScaleTo(baseScale, releaseDuration));
    }

    // Can also be triggered from code (e.g. keyboard shortcuts)
    public void Punch()
    {
        Play(Release());
    }

    private void Play(IEnumerator next)
    {
        if (!isActiveAndEnabled) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(next);
    }

    private IEnumerator Release()
    {
        yield return ScaleTo(baseScale * releaseOvershoot, releaseDuration * 0.4f);
        yield return ScaleTo(baseScale, releaseDuration * 0.6f);
    }

    private IEnumerator ScaleTo(Vector3 target, float duration)
    {
        Vector3 start = rect.localScale;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duration);
            float eased = 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));
            rect.localScale = Vector3.LerpUnclamped(start, target, eased);
            yield return null;
        }
        rect.localScale = target;
    }
}
