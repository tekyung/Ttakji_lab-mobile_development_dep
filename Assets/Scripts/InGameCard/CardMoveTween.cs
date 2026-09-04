using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CardMoveTween
{
    // 카드 이동 시간. 눈으로 따라갈 수 있도록 0.18 → 0.36으로 늘렸다(이동 속도 절반, 2026-08-17).
    private const float Duration = 0.36f;
    private static CardMoveTweenRunner _runner;

    public static void Play(Transform card, Vector2 fromAnchored, Vector2 toAnchored, Action onComplete = null)
    {
        if (card == null)
        {
            onComplete?.Invoke();
            return;
        }

        EnsureRunner();
        _runner.Play(card, fromAnchored, toAnchored, onComplete);
    }

    public static void PlayFromCaptured(Transform card, Vector2 fromAnchored, Action onComplete = null)
    {
        RectTransform rect = card as RectTransform;
        if (rect == null)
        {
            onComplete?.Invoke();
            return;
        }

        Play(card, fromAnchored, rect.anchoredPosition, onComplete);
    }

    public static void PlayFromWorld(Transform card, Vector3 fromWorld, Action onComplete = null)
    {
        RectTransform rect = card as RectTransform;
        if (rect == null)
        {
            onComplete?.Invoke();
            return;
        }

        Play(card, WorldToAnchored(rect, fromWorld), rect.anchoredPosition, onComplete);
    }

    public static void Complete(Transform card)
    {
        if (_runner != null)
            _runner.Complete(card);
    }

    /// <summary>지금 이 카드가 날아가는 중인가. 남의 이동을 잘라먹지 않으려면 먼저 물어본다.</summary>
    public static bool IsPlaying(Transform card)
    {
        return card != null && _runner != null && _runner.IsPlaying(card);
    }

    public static void CompleteChildren(Transform root, Transform except = null)
    {
        if (_runner == null || root == null)
            return;

        var children = new List<Transform>();
        foreach (Transform child in root)
        {
            if (child != except)
                children.Add(child);
        }

        foreach (Transform child in children)
            _runner.Complete(child);
    }

    public static Vector2 WorldToAnchored(RectTransform rect, Vector3 world)
    {
        if (rect == null)
            return world;

        RectTransform parent = rect.parent as RectTransform;
        if (parent == null)
            return new Vector2(world.x, world.y);

        Camera cam = GetCanvasCamera(parent);
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, cam, out Vector2 local))
        {
            Vector3 fallback = parent.InverseTransformPoint(world);
            local = new Vector2(fallback.x, fallback.y);
        }

        Vector2 parentSize = parent.rect.size;
        Vector2 parentMin = parent.rect.min;
        Vector2 anchorMinPos = parentMin + Vector2.Scale(parentSize, rect.anchorMin);
        Vector2 anchorMaxPos = parentMin + Vector2.Scale(parentSize, rect.anchorMax);
        Vector2 anchorCenter = (anchorMinPos + anchorMaxPos) * 0.5f;
        return local - anchorCenter;
    }

    private static Camera GetCanvasCamera(Transform from)
    {
        Canvas canvas = from.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera;
    }

    private static void EnsureRunner()
    {
        if (_runner != null)
            return;

        var go = new GameObject("CardMoveTweenRunner");
        UnityEngine.Object.DontDestroyOnLoad(go);
        _runner = go.AddComponent<CardMoveTweenRunner>();
    }

    private sealed class CardMoveTweenRunner : MonoBehaviour
    {
        private readonly Dictionary<Transform, Coroutine> _running = new Dictionary<Transform, Coroutine>();
        private readonly Dictionary<Transform, Vector2> _targets = new Dictionary<Transform, Vector2>();
        private readonly Dictionary<Transform, Action> _onComplete = new Dictionary<Transform, Action>();

        public void Play(Transform card, Vector2 fromAnchored, Vector2 toAnchored, Action onComplete)
        {
            if (card == null)
            {
                onComplete?.Invoke();
                return;
            }

            Complete(card);
            if (onComplete != null)
                _onComplete[card] = onComplete;

            _targets[card] = toAnchored;
            Coroutine routine = StartCoroutine(Animate(card, fromAnchored, toAnchored));
            _running[card] = routine;
        }

        public bool IsPlaying(Transform card) => card != null && _running.ContainsKey(card);

        public void Complete(Transform card)
        {
            if (card == null)
                return;

            if (_running.TryGetValue(card, out Coroutine routine) && routine != null)
                StopCoroutine(routine);

            _running.Remove(card);

            if (card is RectTransform rect && _targets.TryGetValue(card, out Vector2 target))
                rect.anchoredPosition = target;

            _targets.Remove(card);
            InvokeComplete(card);
        }

        private IEnumerator Animate(Transform card, Vector2 fromAnchored, Vector2 toAnchored)
        {
            RectTransform rect = card as RectTransform;
            if (rect == null)
            {
                InvokeComplete(card);
                yield break;
            }

            rect.anchoredPosition = fromAnchored;
            yield return null;

            float elapsed = 0f;
            while (elapsed < Duration && rect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Duration);
                t = 1f - (1f - t) * (1f - t);
                rect.anchoredPosition = Vector2.LerpUnclamped(fromAnchored, toAnchored, t);
                yield return null;
            }

            if (rect != null)
                rect.anchoredPosition = toAnchored;

            _running.Remove(card);
            _targets.Remove(card);
            InvokeComplete(card);
        }

        private void InvokeComplete(Transform card)
        {
            if (!_onComplete.TryGetValue(card, out Action callback))
                return;

            _onComplete.Remove(card);
            callback?.Invoke();
        }
    }
}
