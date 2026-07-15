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
        Move,
        Scale,
        FadeAndScale,
        FadeAndMove
    }

    public enum SlideDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    /// <summary>
    /// A modal dialog panel: overlay ("Black") + show/hide animation + optional
    /// Escape/OK button wiring for <see cref="ModalDialogStack"/>/<see cref="DefaultModalStackProvider"/>.
    ///
    /// Exposes public Show() and Hide() methods so it can be wired directly in UnityEvents
    /// (e.g. OnShowControllerHelp) without requiring custom scripts.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class Dialog : MonoBehaviour
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

        [Header("Extended Effects")]
        public ShowDialogEffect ShowEffect = ShowDialogEffect.Fade;
        public SlideDirection MoveDirection = SlideDirection.Down;
        [Range(0f, 1f)]
        public float StartScale = 0.5f;

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

        private List<SpriteRenderer> _childSpriteRenderers;
        private List<TMP_Text> _childTextMeshPro;
        protected CanvasGroup _canvasGroup;
        private Coroutine _activeTween;

        // Stores original positions to reset after animations
        private Vector3 _originalLocalScale;
        private Vector2 _originalAnchoredPosition;
        private RectTransform _rectTransform;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform != null)
            {
                _originalAnchoredPosition = _rectTransform.anchoredPosition;
            }
            _originalLocalScale = transform.localScale;
        }

        public void OnEnable()
        {
            _childSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true).ToList();
            _childTextMeshPro = GetComponentsInChildren<TMP_Text>(true).ToList();

            float targetAlpha = UseFadeSpriteRenderers ? 0 : 1;
            foreach (var sr in _childSpriteRenderers) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, targetAlpha);
            foreach (var tmp in _childTextMeshPro) tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, targetAlpha);
        }

        /// <summary>
        /// Shows this dialog, placing it on the active modal stack (ModalDialogStack).
        /// Can be called directly from Inspector UnityEvents.
        /// </summary>
        public void Show()
        {
            ModalDialogStack.ShowDialog(this);
        }

        /// <summary>
        /// Closes this dialog, removing it from the active modal stack (ModalDialogStack).
        /// Can be called directly from Inspector UnityEvents.
        /// </summary>
        public void Hide()
        {
            ModalDialogStack.CloseDialog(this);
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

            // For effects that move/scale the panel (Move, Scale), the Black overlay is hidden (alpha 0) and only
            // begins to appear with a quick fade once the entry animation completes —
            // prevents the overlay from shrinking/sliding along with the panel.
            bool overlayWaitsForTransform = ShowEffect == ShowDialogEffect.Move || ShowEffect == ShowDialogEffect.Scale;
            float overlayDelay = delay + (overlayWaitsForTransform ? ShowHideDialogTime : 0f);
            float overlayDuration = overlayWaitsForTransform ? Mathf.Min(FadeBlackTime, 0.1f) : FadeBlackTime;

            SetOverlayAlphaImmediate(Black, 0);
            SetOverlayAlphaImmediate(Black2, 0);
            FadeOverlay(Black, 0, BlackAlpha, overlayDuration, overlayDelay);
            FadeOverlay(Black2, 0, BlackAlpha, overlayDuration, overlayDelay);

            if (_activeTween != null) StopCoroutine(_activeTween);

            switch (ShowEffect)
            {
                case ShowDialogEffect.Fade:
                    _activeTween = StartCoroutine(FadeInCoroutine(delay, onComplete));
                    break;
                case ShowDialogEffect.Move:
                    _activeTween = StartCoroutine(MoveInCoroutine(delay, onComplete, false));
                    break;
                case ShowDialogEffect.Scale:
                    _activeTween = StartCoroutine(ScaleInCoroutine(delay, onComplete, false));
                    break;
                case ShowDialogEffect.FadeAndScale:
                    _activeTween = StartCoroutine(ScaleInCoroutine(delay, onComplete, true));
                    break;
                case ShowDialogEffect.FadeAndMove:
                    _activeTween = StartCoroutine(MoveInCoroutine(delay, onComplete, true));
                    break;
            }
        }

        internal void PlayHide(Action onComplete)
        {
            gameObject.SendMessage("OnHide", SendMessageOptions.DontRequireReceiver);
            OnHide?.Invoke();

            FadeOutSpriteRenderers();
            FadeOverlay(Black, BlackAlpha, 0, FadeBlackTime, 0);
            FadeOverlay(Black2, BlackAlpha, 0, FadeBlackTime, 0);

            if (_activeTween != null) StopCoroutine(_activeTween);

            switch (ShowEffect)
            {
                case ShowDialogEffect.Fade:
                    _activeTween = StartCoroutine(FadeOutCoroutine(onComplete));
                    break;
                case ShowDialogEffect.Move:
                    _activeTween = StartCoroutine(MoveOutCoroutine(onComplete, false));
                    break;
                case ShowDialogEffect.Scale:
                    _activeTween = StartCoroutine(ScaleOutCoroutine(onComplete, false));
                    break;
                case ShowDialogEffect.FadeAndScale:
                    _activeTween = StartCoroutine(ScaleOutCoroutine(onComplete, true));
                    break;
                case ShowDialogEffect.FadeAndMove:
                    _activeTween = StartCoroutine(MoveOutCoroutine(onComplete, true));
                    break;
            }
        }

        private void FadeInSpriteRenderers(float delay)
        {
            if (!UseFadeSpriteRenderers) return;

            foreach (var sr in _childSpriteRenderers.Where(sr => sr != null))
                StartCoroutine(FadeSpriteRoutine(sr, 1, ShowHideDialogTime, FadeBlackTime + delay));

            foreach (var tmp in _childTextMeshPro.Where(tmp => tmp != null))
                StartCoroutine(FadeTmpRoutine(tmp, 1, ShowHideDialogTime, FadeBlackTime + delay));
        }

        private void FadeOutSpriteRenderers()
        {
            if (!UseFadeSpriteRenderers) return;

            foreach (var sr in _childSpriteRenderers.Where(sr => sr != null))
                StartCoroutine(FadeSpriteRoutine(sr, 0, ShowHideDialogTime, FadeBlackTime));

            foreach (var tmp in _childTextMeshPro.Where(tmp => tmp != null))
                StartCoroutine(FadeTmpRoutine(tmp, 0, ShowHideDialogTime, FadeBlackTime));
        }

        private void SetOverlayAlphaImmediate(Image overlay, float alpha)
        {
            if (overlay == null) return;
            overlay.enabled = alpha > 0f;
            var c = overlay.color;
            overlay.color = new Color(c.r, c.g, c.b, alpha);
        }

        private void FadeOverlay(Image overlay, float from, float to, float duration, float delay)
        {
            if (overlay == null) return;
            overlay.enabled = true;
            var c = overlay.color;
            overlay.color = new Color(c.r, c.g, c.b, from);
            StartCoroutine(FadeOverlayRoutine(overlay, from, to, duration, delay));
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
            if (to <= 0f) overlay.enabled = false;
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

        // ── Fade animations ──

        private IEnumerator FadeInCoroutine(float delay, Action onComplete)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            _canvasGroup.alpha = 0;
            float t = 0;
            while (t < ShowHideDialogTime)
            {
                t += Time.deltaTime;
                _canvasGroup.alpha = Mathf.SmoothStep(0, 1, t / ShowHideDialogTime);
                yield return null;
            }
            _canvasGroup.alpha = 1;
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
            _canvasGroup.alpha = 0;
            onComplete?.Invoke();
        }

        // ── Scale animations ──

        private IEnumerator ScaleInCoroutine(float delay, Action onComplete, bool alsoFade)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            transform.localScale = Vector3.one * StartScale;
            if (alsoFade) _canvasGroup.alpha = 0;

            float t = 0;
            while (t < ShowHideDialogTime)
            {
                t += Time.deltaTime;
                float p = Mathf.SmoothStep(0, 1, t / ShowHideDialogTime);
                transform.localScale = Vector3.Lerp(Vector3.one * StartScale, _originalLocalScale, p);
                if (alsoFade) _canvasGroup.alpha = p;
                yield return null;
            }
            transform.localScale = _originalLocalScale;
            if (alsoFade) _canvasGroup.alpha = 1;
            onComplete?.Invoke();
        }

        private IEnumerator ScaleOutCoroutine(Action onComplete, bool alsoFade)
        {
            if (FadeBlackTime > 0) yield return new WaitForSeconds(FadeBlackTime);
            float t = 0;
            while (t < ShowHideDialogTime)
            {
                t += Time.deltaTime;
                float p = t / ShowHideDialogTime;
                transform.localScale = Vector3.Lerp(_originalLocalScale, Vector3.one * StartScale, Mathf.SmoothStep(0, 1, p));
                if (alsoFade) _canvasGroup.alpha = 1 - p;
                yield return null;
            }
            transform.localScale = _originalLocalScale;
            onComplete?.Invoke();
        }

        // ── Move animations ──

        private Vector2 GetMoveOffset()
        {
            var size = _rectTransform != null ? _rectTransform.rect.size : new Vector2(Screen.width, Screen.height);
            switch (MoveDirection)
            {
                case SlideDirection.Up: return Vector2.up * size.y;
                case SlideDirection.Down: return Vector2.down * size.y;
                case SlideDirection.Left: return Vector2.left * size.x;
                case SlideDirection.Right: return Vector2.right * size.x;
                default: return Vector2.down * size.y;
            }
        }

        private IEnumerator MoveInCoroutine(float delay, Action onComplete, bool alsoFade)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            Vector2 offset = GetMoveOffset();

            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = _originalAnchoredPosition + offset;
            }
            else
            {
                transform.position = transform.position + (Vector3)offset;
            }

            if (alsoFade) _canvasGroup.alpha = 0;

            float t = 0;
            while (t < ShowHideDialogTime)
            {
                t += Time.deltaTime;
                float p = Mathf.SmoothStep(0, 1, t / ShowHideDialogTime);

                if (_rectTransform != null)
                    _rectTransform.anchoredPosition = Vector2.Lerp(_originalAnchoredPosition + offset, _originalAnchoredPosition, p);
                else
                    transform.position = Vector3.Lerp(transform.position, transform.position - (Vector3)offset * (1 - p), p);

                if (alsoFade) _canvasGroup.alpha = p;
                yield return null;
            }

            if (_rectTransform != null) _rectTransform.anchoredPosition = _originalAnchoredPosition;
            if (alsoFade) _canvasGroup.alpha = 1;
            onComplete?.Invoke();
        }

        private IEnumerator MoveOutCoroutine(Action onComplete, bool alsoFade)
        {
            if (FadeBlackTime > 0) yield return new WaitForSeconds(FadeBlackTime);
            Vector2 offset = GetMoveOffset();
            Vector3 startPos = transform.position;

            float t = 0;
            while (t < ShowHideDialogTime)
            {
                t += Time.deltaTime;
                float p = t / ShowHideDialogTime;
                float smoothP = Mathf.SmoothStep(0, 1, p);

                if (_rectTransform != null)
                    _rectTransform.anchoredPosition = Vector2.Lerp(_originalAnchoredPosition, _originalAnchoredPosition + offset, smoothP);
                else
                    transform.position = Vector3.Lerp(startPos, startPos + (Vector3)offset, smoothP);

                if (alsoFade) _canvasGroup.alpha = 1 - p;
                yield return null;
            }

            if (_rectTransform != null) _rectTransform.anchoredPosition = _originalAnchoredPosition;
            else transform.position = startPos;

            onComplete?.Invoke();
        }
    }
}
