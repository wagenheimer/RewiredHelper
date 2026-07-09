using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using TMPro;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Wagenheimer.RewiredHelper.UI
{
    public enum ShowDialogEffect
    {
        Fade,
        Move
    }

    /// <summary>
    /// A modal dialog panel: overlay ("Black") + show/hide animation + optional
    /// Escape/OK button wiring for <see cref="ModalDialogStack"/>/<see cref="DefaultModalStackProvider"/>.
    ///
    /// The default show/hide animation is a built-in coroutine tween — no third-party dependency
    /// is required. To use DOTween (or any other tweening library) instead, subclass this type in
    /// your own project and override <see cref="PlayFadeIn"/>/<see cref="PlayFadeOut"/>/
    /// <see cref="PlayMoveIn"/>/<see cref="PlayMoveOut"/>. Kept opt-in on purpose so this package
    /// never forces a DOTween dependency on consumers who don't have it installed.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ModalDialog : MonoBehaviour
    {
        [Header("Overlay")]
        public Image Black;
        public Image Black2;
        public float BlackAlpha = 0.7f;

        [Header("Modal Buttons")]
        [Tooltip("Triggered when Escape is pressed while this dialog is the top of the modal stack.")]
        public Button EscapeButton;
        [Tooltip("Triggered when Return/OK is pressed while this dialog is the top of the modal stack.")]
        public Button OkButton;

        [Header("Timing")]
        public float ShowHideDialogTime = 0.15f;
        public float FadeBlackTime = 0.2f;

        [Header("Focus")]
        public Selectable FocusOnShow;

        [Header("Sprite Fade")]
        public bool UseFadeSpriteRenderers;

        [Header("Runtime Events")]
        public UnityEvent AfterShow;
        public UnityEvent AfterHide;

        /// <summary>Fired right when Show()/Hide() is invoked, before the animation starts.</summary>
        public Action OnShow;
        public Action OnHide;

        /// <summary>
        /// Raised whenever this dialog wants the host to suppress other input/UI for the given
        /// number of seconds (roughly the show/hide animation duration). Optional — a host can
        /// forward this into its own <see cref="IUiBlocker"/>-backed blocking mechanism.
        /// </summary>
        public static event Action<float> OnBlockUiRequested;

        public ShowDialogEffect ShowEffect = ShowDialogEffect.Fade;

        private List<SpriteRenderer> _childSpriteRenderers;
        private List<TMP_Text> _childTextMeshPro;
        protected CanvasGroup _canvasGroup;
        private Coroutine _activeTween;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        public void OnEnable()
        {
            _childSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true).ToList();
            _childTextMeshPro = GetComponentsInChildren<TMP_Text>(true).ToList();

            float targetAlpha = UseFadeSpriteRenderers ? 0 : 1;
            foreach (var sr in _childSpriteRenderers) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, targetAlpha);
            foreach (var tmp in _childTextMeshPro) tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, targetAlpha);
        }

        public void ResetToDefaultTime()
        {
            ShowHideDialogTime = 0.15f;
            FadeBlackTime = 0.2f;
        }

        public void CloseDialog() => ModalDialogStack.CloseDialog(this);

        internal void RequestBlockUi(float seconds) => OnBlockUiRequested?.Invoke(seconds);

        internal void PlayShow(float delay, Action onComplete)
        {
            gameObject.SetActive(true);
            FocusOnShow?.Select();
            gameObject.SendMessage("OnShow", SendMessageOptions.DontRequireReceiver);
            OnShow?.Invoke();

            FadeInSpriteRenderers(delay);
            FadeOverlay(Black, 0, BlackAlpha, FadeBlackTime, delay);
            FadeOverlay(Black2, 0, BlackAlpha, FadeBlackTime, delay);

            switch (ShowEffect)
            {
                case ShowDialogEffect.Fade:
                    PlayFadeIn(delay, onComplete);
                    break;
                case ShowDialogEffect.Move:
                    PlayMoveIn(delay, onComplete);
                    break;
            }
        }

        internal void PlayHide(Action onComplete)
        {
            FadeOutSpriteRenderers();
            FadeOverlay(Black, BlackAlpha, 0, FadeBlackTime, 0);
            FadeOverlay(Black2, BlackAlpha, 0, FadeBlackTime, 0);

            switch (ShowEffect)
            {
                case ShowDialogEffect.Fade:
                    PlayFadeOut(onComplete);
                    break;
                case ShowDialogEffect.Move:
                    PlayMoveOut(onComplete);
                    break;
            }
        }

        private void FadeInSpriteRenderers(float delay)
        {
            if (!UseFadeSpriteRenderers) return;

            foreach (var sr in _childSpriteRenderers.Where(sr => sr != null))
                StartTween(FadeSpriteRoutine(sr, 1, ShowHideDialogTime, FadeBlackTime + delay));

            foreach (var tmp in _childTextMeshPro.Where(tmp => tmp != null))
                StartTween(FadeTmpRoutine(tmp, 1, ShowHideDialogTime, FadeBlackTime + delay));
        }

        private void FadeOutSpriteRenderers()
        {
            if (!UseFadeSpriteRenderers) return;

            foreach (var sr in _childSpriteRenderers.Where(sr => sr != null))
                StartTween(FadeSpriteRoutine(sr, 0, ShowHideDialogTime, FadeBlackTime));

            foreach (var tmp in _childTextMeshPro.Where(tmp => tmp != null))
                StartTween(FadeTmpRoutine(tmp, 0, ShowHideDialogTime, FadeBlackTime));
        }

        // ── Fade effect (override in a subclass to swap in DOTween or another tweener) ──

        protected virtual void PlayFadeIn(float delay, Action onComplete) => StartTween(FadeInCoroutine(delay, onComplete));

        protected virtual void PlayFadeOut(Action onComplete) => StartTween(FadeOutCoroutine(onComplete));

        // ── Move effect (override in a subclass to swap in DOTween or another tweener) ──

        protected virtual void PlayMoveIn(float delay, Action onComplete) => StartTween(MoveInCoroutine(delay, onComplete));

        protected virtual void PlayMoveOut(Action onComplete) => StartTween(MoveOutCoroutine(onComplete));

        // ── Coroutine implementation (default tween, no third-party dependency) ────────

        private void StartTween(IEnumerator routine) => StartCoroutine(routine);

        private void FadeOverlay(Image overlay, float from, float to, float duration, float delay)
        {
            if (overlay == null) return;
            overlay.enabled = true;
            var c = overlay.color;
            overlay.color = new Color(c.r, c.g, c.b, from);
            StartTween(FadeOverlayRoutine(overlay, from, to, duration, delay));
        }

        private IEnumerator FadeOverlayRoutine(Image overlay, float from, float to, float duration, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            float t = 0;
            while (t < duration)
            {
                t += Time.deltaTime;
                var c = overlay.color;
                overlay.color = new Color(c.r, c.g, c.b, Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            var final = overlay.color;
            overlay.color = new Color(final.r, final.g, final.b, to);
        }

        private IEnumerator FadeSpriteRoutine(SpriteRenderer sr, float to, float duration, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            float from = sr.color.a;
            float t = 0;
            while (t < duration)
            {
                t += Time.deltaTime;
                sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, to);
        }

        private IEnumerator FadeTmpRoutine(TMP_Text tmp, float to, float duration, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            float from = tmp.color.a;
            float t = 0;
            while (t < duration)
            {
                t += Time.deltaTime;
                tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, to);
        }

        private IEnumerator FadeInCoroutine(float delay, Action onComplete)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            _canvasGroup.alpha = 0;
            transform.localScale = Vector3.one * 0.9f;
            float t = 0;
            while (t < ShowHideDialogTime)
            {
                t += Time.deltaTime;
                float p = Mathf.SmoothStep(0, 1, t / ShowHideDialogTime);
                _canvasGroup.alpha = p;
                transform.localScale = Vector3.Lerp(Vector3.one * 0.9f, Vector3.one, p);
                yield return null;
            }
            _canvasGroup.alpha = 1;
            transform.localScale = Vector3.one;
            onComplete?.Invoke();
        }

        private IEnumerator FadeOutCoroutine(Action onComplete)
        {
            if (FadeBlackTime > 0) yield return new WaitForSeconds(FadeBlackTime);
            float t = 0;
            while (t < ShowHideDialogTime)
            {
                t += Time.deltaTime;
                _canvasGroup.alpha = 1 - Mathf.SmoothStep(0, 1, t / ShowHideDialogTime);
                yield return null;
            }
            _canvasGroup.alpha = 1;
            onComplete?.Invoke();
        }

        private IEnumerator MoveInCoroutine(float delay, Action onComplete)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            var start = transform.position + Vector3.down * 1000;
            var end = transform.position;
            transform.position = start;
            float t = 0;
            while (t < ShowHideDialogTime)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0, 1, t / ShowHideDialogTime));
                yield return null;
            }
            transform.position = end;
            onComplete?.Invoke();
        }

        private IEnumerator MoveOutCoroutine(Action onComplete)
        {
            if (FadeBlackTime > 0) yield return new WaitForSeconds(FadeBlackTime);
            var start = transform.position;
            var end = start + Vector3.down * 1000;
            float t = 0;
            while (t < ShowHideDialogTime)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0, 1, t / ShowHideDialogTime));
                yield return null;
            }
            transform.position = start;
            onComplete?.Invoke();
        }
    }
}
