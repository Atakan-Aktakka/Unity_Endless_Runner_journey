using System.Collections;
using UnityEngine;
using TMPro;

// Main menu "NASIL OYNANIR" popup: controls overview + "reset tutorial".
// Built by Tools > Journey > Build How To Play Panel (ScoreMenuScene).
public class HowToPlayPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup panel;
    [SerializeField] private RectTransform card;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private float popupDuration = 0.35f;
    [SerializeField] private AnimationCurve popupCurve = new AnimationCurve(
        new Keyframe(0f, 0.6f), new Keyframe(0.65f, 1.06f), new Keyframe(1f, 1f));
    [SerializeField] private string resetMessage = "Eğitim sıfırlandı! Bir sonraki oyunda tekrar gösterilecek.";
    [SerializeField] private string notCompletedMessage = "Eğitim bir sonraki oyunda gösterilecek.";

    private Coroutine routine;

    private void Awake()
    {
        if (panel != null)
        {
            panel.alpha = 0f;
            panel.blocksRaycasts = false;
            panel.gameObject.SetActive(false);
        }
    }

    public void Open()
    {
        if (panel == null) return;

        panel.gameObject.SetActive(true);
        panel.blocksRaycasts = true;
        if (statusText != null)
        {
            statusText.text = TutorialManager.IsCompleted ? "" : notCompletedMessage;
        }
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Pop());
    }

    public void Close()
    {
        if (panel == null) return;
        if (routine != null) StopCoroutine(routine);
        panel.alpha = 0f;
        panel.blocksRaycasts = false;
        panel.gameObject.SetActive(false);
    }

    public void ResetTutorial()
    {
        TutorialManager.ResetProgress();
        if (statusText != null) statusText.text = resetMessage;
    }

    // Unscaled: the menu may be opened after a paused game
    private IEnumerator Pop()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, popupDuration);
            float c = Mathf.Clamp01(t);
            panel.alpha = Mathf.Clamp01(c * 2.5f);
            if (card != null) card.localScale = Vector3.one * popupCurve.Evaluate(c);
            yield return null;
        }
        panel.alpha = 1f;
        if (card != null) card.localScale = Vector3.one;
        routine = null;
    }
}
