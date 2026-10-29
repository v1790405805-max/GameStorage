using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 手牌弧形排布与回合换手动画控制器。
/// </summary>
public class HandCardArcLayout : MonoBehaviour
{
    private sealed class RuntimeDrawRequest
    {
        public CardData CardData;
        public Action OnArrived;
    }

    public static HandCardArcLayout Instance { get; private set; }

    [Header("--- 1. 空间与安全区约束 ---")]
    [Tooltip("手牌允许占用的最大总宽度")]
    [SerializeField] private float maxSafeVisualWidth = 720f;

    [Tooltip("单张卡牌的大致视觉宽度")]
    [SerializeField] private float cardVisualWidth = 140f;

    [Tooltip("手牌较少时的自然基础间距")]
    [SerializeField] private float naturalSpacing = 135f;

    [Tooltip("极端多牌时允许压缩到的最小间距")]
    [SerializeField] private float minAllowedSpacing = 45f;

    [Header("--- 2. 弧形几何参数 ---")]
    [Tooltip("抛物线拱起高度系数")]
    [SerializeField] private float curveHeightFactor = 10f;

    [Tooltip("卡牌旋转倾角步长")]
    [SerializeField] private float baseAngleStep = 3.2f;

    [Header("--- 3. 补间动画配置 ---")]
    [Tooltip("卡牌归位动画时长")]
    [SerializeField] private float arrangeDuration = 0.22f;

    [Tooltip("排布缓动曲线")]
    [SerializeField] private Ease arrangeEase = Ease.OutCubic;

    [Header("--- 4. 回合换手动画 ---")]
    [Tooltip("抽牌堆的 UI 目标；留空时自动查找名为 DrawPile 的对象")]
    [SerializeField] private RectTransform drawPileTarget;

    [Tooltip("弃牌堆的 UI 目标；留空时自动查找名为 DiscardPile 的对象")]
    [SerializeField] private RectTransform discardPileTarget;

    [SerializeField, Min(0.01f)] private float handGatherDuration = 0.2f;
    [SerializeField, Min(0.01f)] private float pileShrinkDuration = 0.13f;
    [SerializeField, Min(0.01f)] private float discardArcDuration = 0.35f;
    [SerializeField, Min(0f)] private float discardArcInterval = 0.035f;
    [SerializeField, Min(0.01f)] private float drawMoveDuration = 0.3f;
    [SerializeField, Min(0f)] private float drawPileHoldDuration = 0.08f;
    [Tooltip("新卡逐张从 DrawPile 移动到中心的间隔")]
    [SerializeField, Min(0f)] private float drawLaunchInterval = 0.038f;
    [SerializeField, Min(1f)] private float revealSpacing = 135f;
    [SerializeField, Min(1f)] private float maxRevealWidth = 900f;
    [SerializeField, Min(0.01f)] private float lineSpreadDuration = 0.33f;
    [SerializeField, Min(0f)] private float arcHeight = 180f;
    [SerializeField, Min(0.01f)] private float finalSpreadDuration = 0.28f;
    [Tooltip("换手动画期间弧形归位使用的固定时长")]
    [SerializeField, Min(0.01f)] private float drawArrangeDuration = 0.24f;

    [Header("--- 5. 出牌离场动画 ---")]
    [SerializeField, Min(0.01f)] private float playedCardGatherDuration = 0.3f;
    [SerializeField, Min(0f)] private float playedCardCenterHoldDuration = 0.7f;
    [SerializeField, Min(0.01f)] private float playedCardDiscardDuration = 0.36f;
    [Tooltip("出牌聚拢点在画面宽度上的归一化位置")]
    [SerializeField, Range(0f, 1f)] private float playedCardFocusScreenX = 0.88f;
    [Tooltip("出牌聚拢点在画面高度上的归一化位置")]
    [SerializeField, Range(0f, 1f)] private float playedCardFocusScreenY = 0.63f;
    [SerializeField] private Ease pileArcEase = Ease.InOutCubic;
    [SerializeField] private Ease drawMoveEase = Ease.OutCubic;
    [SerializeField] private Ease lineSpreadEase = Ease.OutCubic;

    private readonly List<CardUI> trackedCards = new List<CardUI>();
    private readonly Queue<RuntimeDrawRequest> runtimeDrawQueue =
        new Queue<RuntimeDrawRequest>();
    private int lastChildCount = -1;

    private CardManager cardManager;
    private RectTransform flightLayer;
    private Coroutine runtimeDrawCoroutine;
    private bool isPlaying;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        RefreshLayout();
    }

    private void OnTransformChildrenChanged()
    {
        if (transform.childCount != lastChildCount)
        {
            lastChildCount = transform.childCount;
            RefreshLayout();
        }
    }

    private void Update()
    {
        if (transform.childCount != lastChildCount)
        {
            lastChildCount = transform.childCount;
            RefreshLayout();
        }
    }

    private void SyncTrackedCards()
    {
        for (int i = trackedCards.Count - 1; i >= 0; i--)
        {
            if (trackedCards[i] == null ||
                !trackedCards[i].gameObject.activeSelf ||
                trackedCards[i].transform.parent != transform)
            {
                trackedCards.RemoveAt(i);
            }
        }

        foreach (Transform child in transform)
        {
            CardUI card = child.GetComponent<CardUI>();
            if (card != null && child.gameObject.activeSelf && !trackedCards.Contains(card))
            {
                trackedCards.Add(card);
            }
        }
    }

    public void BringCardToFront(CardUI card)
    {
        if (card == null || card.transform.parent != transform) return;
        card.transform.SetAsLastSibling();
    }

    public void RestoreCardOrder(CardUI card)
    {
        if (card == null || card.transform.parent != transform) return;
        int index = trackedCards.IndexOf(card);
        if (index >= 0)
        {
            card.transform.SetSiblingIndex(index);
        }
    }

    /// <summary>
    /// 刷新全部手牌的弧形位置。
    /// </summary>
    public void RefreshLayout()
    {
        SyncTrackedCards();

        int count = trackedCards.Count;
        if (count == 0) return;
        float layoutDuration = isPlaying ? drawArrangeDuration : arrangeDuration;

        if (count == 1)
        {
            CardUI singleCard = trackedCards[0];
            if (singleCard != null && !singleCard.IsDragging)
            {
                singleCard.UpdateHomeTransform(Vector3.zero, Vector3.zero);
                singleCard.transform.DOKill();
                singleCard.transform.DOLocalMove(Vector3.zero, layoutDuration).SetEase(arrangeEase);
                singleCard.transform.DOLocalRotate(Vector3.zero, layoutDuration).SetEase(arrangeEase);
            }
            return;
        }

        float maxCenterSpan = Mathf.Max(0f, maxSafeVisualWidth - cardVisualWidth);
        float autoSpacing = maxCenterSpan / (count - 1);
        float effectiveSpacing = Mathf.Clamp(
            Mathf.Min(naturalSpacing, autoSpacing),
            minAllowedSpacing,
            naturalSpacing);

        float compressFactor = Mathf.Clamp01(effectiveSpacing / naturalSpacing);
        float currentAngleStep = baseAngleStep * Mathf.Lerp(0.65f, 1f, compressFactor);
        float currentHeightFactor = curveHeightFactor * Mathf.Lerp(0.7f, 1f, compressFactor);
        float midIndex = (count - 1) / 2f;

        for (int i = 0; i < count; i++)
        {
            CardUI card = trackedCards[i];
            if (card == null || card.IsDragging) continue;

            float offset = i - midIndex;
            float targetX = offset * effectiveSpacing;
            float targetY = -Mathf.Pow(offset, 2f) * currentHeightFactor;
            float targetAngle = -offset * currentAngleStep;

            Vector3 targetPos = new Vector3(targetX, targetY, 0f);
            Vector3 targetRot = new Vector3(0f, 0f, targetAngle);

            card.transform.SetSiblingIndex(i);
            card.UpdateHomeTransform(targetPos, targetRot);
            card.transform.DOKill();
            card.transform.DOLocalMove(targetPos, layoutDuration).SetEase(arrangeEase);
            card.transform.DOLocalRotate(targetRot, layoutDuration).SetEase(arrangeEase);
        }
    }

    /// <summary>
    /// 出牌确认后的离场动画：聚拢、缩小、沿弧线进入弃牌堆并销毁。
    /// </summary>
    public void PlayCardDiscardAnimation(CardUI card)
    {
        if (card == null)
        {
            return;
        }

        if (cardManager == null)
        {
            cardManager = CardManager.Instance;
        }

        ResolvePileTargets();
        StartCoroutine(PlayCardDiscardRoutine(card));
    }

    /// <summary>
    /// 回合中抽牌：从 DrawPile 原位生成，直线进入手牌区域最右侧。
    /// </summary>
    public bool PlayDrawCardFromPile(CardData cardData, Action onArrived = null)
    {
        if (cardData == null)
        {
            return false;
        }

        if (cardManager == null)
        {
            cardManager = CardManager.Instance;
        }

        if (cardManager == null ||
            cardManager.cardPrefab == null ||
            cardManager.handUIContainer == null)
        {
            return false;
        }

        ResolvePileTargets();
        runtimeDrawQueue.Enqueue(new RuntimeDrawRequest
        {
            CardData = cardData,
            OnArrived = onArrived
        });
        if (runtimeDrawCoroutine == null)
        {
            runtimeDrawCoroutine = StartCoroutine(ProcessRuntimeDrawQueue());
        }

        return true;
    }

    private IEnumerator ProcessRuntimeDrawQueue()
    {
        while (runtimeDrawQueue.Count > 0)
        {
            RuntimeDrawRequest request = runtimeDrawQueue.Dequeue();
            if (request.CardData != null)
            {
                yield return AnimateRuntimeDrawCard(request);
            }
        }

        runtimeDrawCoroutine = null;
    }

    private IEnumerator AnimateRuntimeDrawCard(RuntimeDrawRequest request)
    {
        CardData cardData = request.CardData;
        RectTransform animationLayer = CreateCardAnimationLayer("RuntimeDrawCardFlightLayer");
        Vector3 drawWorldPosition = GetPileWorldPosition(
            drawPileTarget,
            Vector3.zero);
        float pileScale = GetPileCardScale();

        CardUI card = InstantiateCardView(
            cardData,
            drawWorldPosition,
            pileScale,
            animationLayer);
        if (card == null)
        {
            if (animationLayer != null && animationLayer != transform)
            {
                Destroy(animationLayer.gameObject);
            }
            yield break;
        }

        SetCardRaycasts(card, false);
        GetCardCanvasGroup(card).alpha = 1f;
        yield return null;

        Vector3 entryPosition = GetHandRightEntryLocalPosition(animationLayer);
        Sequence moveSequence = DOTween.Sequence();
        moveSequence.Append(
            card.transform.DOLocalMove(entryPosition, drawMoveDuration)
                .SetEase(drawMoveEase));
        moveSequence.Join(
            card.transform.DOScale(Vector3.one * cardManager.cardSpawnScale, drawMoveDuration)
                .SetEase(drawMoveEase));
        moveSequence.Join(
            card.transform.DOLocalRotate(Vector3.zero, drawMoveDuration)
                .SetEase(drawMoveEase));
        yield return moveSequence.WaitForCompletion();

        if (card == null)
        {
            if (animationLayer != null && animationLayer != transform)
            {
                Destroy(animationLayer.gameObject);
            }
            yield break;
        }

        card.transform.SetParent(cardManager.handUIContainer, false);
        RectTransform rect = card.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.localPosition = entryPosition;
            rect.localScale = Vector3.one * cardManager.cardSpawnScale;
        }

        request.OnArrived?.Invoke();
        card.handAreaRect = cardManager.handUIContainer as RectTransform;
        RefreshLayout();

        if (arrangeDuration > 0f)
        {
            yield return new WaitForSeconds(arrangeDuration);
        }

        SetCardRaycasts(card, true);
        if (animationLayer != null && animationLayer != transform)
        {
            Destroy(animationLayer.gameObject);
        }
    }

    private Vector3 GetHandRightEntryLocalPosition(RectTransform coordinateSpace)
    {
        RectTransform handRect = cardManager.handUIContainer as RectTransform;
        float halfCardWidth = cardVisualWidth * 0.5f;
        float rightX = handRect != null && handRect.rect.width > 0f
            ? handRect.rect.xMax - halfCardWidth
            : maxSafeVisualWidth * 0.5f - halfCardWidth;

        if (coordinateSpace == null)
        {
            return new Vector3(rightX, 0f, 0f);
        }

        Vector3 worldPosition = cardManager.handUIContainer.TransformPoint(
            new Vector3(rightX, 0f, 0f));
        return coordinateSpace.InverseTransformPoint(worldPosition);
    }

    private IEnumerator PlayCardDiscardRoutine(CardUI card)
    {
        RectTransform animationLayer = CreateCardAnimationLayer("PlayedCardFlightLayer");
        card.transform.DOKill();
        card.ClearHighlightForPlayedAnimation();
        SetCardRaycasts(card, false);

        if (animationLayer != null && animationLayer != transform)
        {
            card.transform.SetParent(animationLayer, true);
        }

        RefreshLayout();

        Vector3 centerPosition = GetPlayedCardFocusLocalPosition(animationLayer);
        float pileScale = GetPileCardScale();
        float normalScale = cardManager != null ? cardManager.cardSpawnScale : 1f;
        card.transform.localScale = Vector3.one * normalScale;

        Sequence gatherSequence = DOTween.Sequence();
        gatherSequence.Append(
            card.transform.DOLocalMove(centerPosition, playedCardGatherDuration)
                .SetEase(arrangeEase));
        gatherSequence.Join(
            card.transform.DOLocalRotate(Vector3.zero, playedCardGatherDuration)
                .SetEase(arrangeEase));
        gatherSequence.Join(
            card.transform.DOScale(Vector3.one * normalScale * 1.5f, playedCardGatherDuration)
                .SetEase(Ease.OutCubic));
        yield return gatherSequence.WaitForCompletion();

        if (playedCardCenterHoldDuration > 0f)
        {
            yield return new WaitForSeconds(playedCardCenterHoldDuration);
        }

        RectTransform coordinateSpace = animationLayer != null
            ? animationLayer
            : transform as RectTransform;
        Vector3 discardPosition = GetPileLocalPosition(
            discardPileTarget,
            coordinateSpace,
            centerPosition);
        Vector3[] path = BuildArcPath(centerPosition, discardPosition);

        CanvasGroup canvasGroup = GetCardCanvasGroup(card);
        canvasGroup.alpha = 1f;

        Sequence discardSequence = DOTween.Sequence();
        discardSequence.Append(
            card.transform.DOLocalPath(
                    path,
                    playedCardDiscardDuration,
                    PathType.CatmullRom)
                .SetEase(pileArcEase));
        discardSequence.Join(
            card.transform.DOLocalRotate(Vector3.zero, playedCardDiscardDuration)
                .SetEase(pileArcEase));
        discardSequence.Join(
            card.transform.DOScale(Vector3.one * pileScale, playedCardDiscardDuration)
                .SetEase(Ease.InOutQuad));

        float fadeDuration = Mathf.Min(0.12f, playedCardDiscardDuration * 0.3f);
        discardSequence.Join(
            canvasGroup.DOFade(0f, fadeDuration)
                .SetDelay(Mathf.Max(0f, playedCardDiscardDuration - fadeDuration)));
        yield return discardSequence.WaitForCompletion();

        if (card != null)
        {
            Destroy(card.gameObject);
        }

        if (animationLayer != null && animationLayer != transform)
        {
            Destroy(animationLayer.gameObject);
        }
    }

    private Vector3 GetPlayedCardFocusLocalPosition(RectTransform coordinateSpace)
    {
        if (cardManager == null || cardManager.handUIContainer == null)
        {
            return Vector3.zero;
        }

        RectTransform targetSpace = coordinateSpace != null
            ? coordinateSpace
            : cardManager.handUIContainer as RectTransform;
        if (targetSpace == null)
        {
            return Vector3.zero;
        }

        Canvas canvas = cardManager.handUIContainer.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector2 screenPoint = new Vector2(
            Screen.width * playedCardFocusScreenX,
            Screen.height * playedCardFocusScreenY);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                targetSpace,
                screenPoint,
                uiCamera,
                out Vector2 localPoint))
        {
            return new Vector3(localPoint.x, localPoint.y, 0f);
        }

        return Vector3.zero;
    }

    /// <summary>
    /// 尝试播放敌方回合结束后的完整换手动画。
    /// </summary>
    public bool TryPlay(CardManager manager)
    {
        if (isPlaying)
        {
            return true;
        }

        cardManager = manager;
        if (cardManager == null ||
            cardManager.handUIContainer == null ||
            cardManager.cardPrefab == null)
        {
            return false;
        }

        isPlaying = true;
        StartCoroutine(PlayRoutine());
        return true;
    }

    private IEnumerator PlayRoutine()
    {
        try
        {
            cardManager.ResetCardsPlayedThisTurn();
            ResolvePileTargets();
            flightLayer = CreateCardAnimationLayer("CardDealFlightLayer");
            Vector3 handCenter = new Vector3(0f, cardManager.cardHeightOffset, 0f);

            yield return AnimateDiscardHandToPile(handCenter);
            yield return AnimateDrawNewHandFromPile(handCenter);

            foreach (CardUI card in GetHandCardViews())
            {
                SetCardRaycasts(card, true);
                GetCardCanvasGroup(card).alpha = 1f;
            }

            cardManager.NotifyPileStateChanged();
        }
        finally
        {
            if (flightLayer != null)
            {
                Destroy(flightLayer.gameObject);
                flightLayer = null;
            }

            isPlaying = false;
        }
    }

    private IEnumerator AnimateDiscardHandToPile(Vector3 handCenter)
    {
        List<CardUI> oldCards = GetHandCardViews();
        if (oldCards.Count == 0)
        {
            cardManager.hand.Clear();
            cardManager.NotifyPileStateChanged();
            yield break;
        }

        foreach (CardUI card in oldCards)
        {
            if (card == null) continue;
            card.transform.DOKill();
            SetCardRaycasts(card, false);
            if (flightLayer != null)
            {
                card.transform.SetParent(flightLayer, true);
            }
        }

        Sequence gatherSequence = DOTween.Sequence();
        foreach (CardUI card in oldCards)
        {
            if (card == null) continue;
            gatherSequence.Join(
                card.transform.DOLocalMove(handCenter, handGatherDuration)
                    .SetEase(Ease.OutCubic));
            gatherSequence.Join(
                card.transform.DOLocalRotate(Vector3.zero, handGatherDuration)
                    .SetEase(Ease.OutCubic));
        }

        if (gatherSequence.IsActive())
        {
            yield return gatherSequence.WaitForCompletion();
        }

        float pileScale = GetPileCardScale();
        Sequence shrinkSequence = DOTween.Sequence();
        foreach (CardUI card in oldCards)
        {
            if (card == null) continue;
            shrinkSequence.Join(
                card.transform.DOScale(Vector3.one * pileScale, pileShrinkDuration)
                    .SetEase(Ease.InOutQuad));
        }

        if (shrinkSequence.IsActive())
        {
            yield return shrinkSequence.WaitForCompletion();
        }

        Vector3 discardPosition = GetPileLocalPosition(discardPileTarget, handCenter);
        List<Tween> discardTweens = new List<Tween>();

        for (int i = 0; i < oldCards.Count; i++)
        {
            CardUI card = oldCards[i];
            if (card == null) continue;

            CardUI cardToDiscard = card;
            Vector3 startPosition = card.transform.localPosition;
            Vector3[] path = BuildArcPath(startPosition, discardPosition);
            float targetAngle = Mathf.Lerp(
                -12f,
                12f,
                oldCards.Count > 1 ? (float)i / (oldCards.Count - 1) : 0.5f);

            CanvasGroup canvasGroup = GetCardCanvasGroup(card);
            canvasGroup.alpha = 1f;

            Sequence cardSequence = DOTween.Sequence();
            cardSequence.Append(
                card.transform.DOLocalPath(path, discardArcDuration, PathType.CatmullRom)
                    .SetEase(pileArcEase));
            cardSequence.Join(
                card.transform.DOLocalRotate(new Vector3(0f, 0f, targetAngle), discardArcDuration)
                    .SetEase(pileArcEase));

            float fadeDuration = Mathf.Min(0.12f, discardArcDuration * 0.3f);
            cardSequence.Join(
                canvasGroup.DOFade(0f, fadeDuration)
                    .SetDelay(Mathf.Max(0f, discardArcDuration - fadeDuration)));
            cardSequence.SetDelay(i * discardArcInterval);
            cardSequence.OnComplete(() => CompleteDiscardedCard(cardToDiscard));
            discardTweens.Add(cardSequence);
        }

        yield return WaitForTweens(discardTweens);
        cardManager.hand.Clear();
        cardManager.NotifyPileStateChanged();
    }

    private IEnumerator AnimateDrawNewHandFromPile(Vector3 handCenter)
    {
        Vector3 drawPosition = GetPileWorldPosition(drawPileTarget, handCenter);
        float pileScale = GetPileCardScale();
        List<CardUI> newCards = new List<CardUI>();
        int visualIndex = 0;

        foreach (CardData specialCard in cardManager.specialCards)
        {
            if (specialCard == null) continue;

            CardData cardForHand = specialCard.Clone();
            cardManager.hand.Add(cardForHand);
            CardUI cardUI = CreateDrawCardView(
                cardForHand,
                drawPosition,
                pileScale,
                visualIndex++);
            if (cardUI != null)
            {
                newCards.Add(cardUI);
            }
        }

        for (int i = 0; i < cardManager.cardsDrawnEachTurn; i++)
        {
            if (!cardManager.TryTakeNextDrawCard(out CardData drawnCard))
            {
                break;
            }

            cardManager.hand.Add(drawnCard);
            CardUI cardUI = CreateDrawCardView(
                drawnCard,
                drawPosition,
                pileScale,
                visualIndex++);
            if (cardUI != null)
            {
                newCards.Add(cardUI);
            }
        }

        yield return null;
        if (drawPileHoldDuration > 0f)
        {
            yield return new WaitForSeconds(drawPileHoldDuration);
        }

        List<Tween> centerTweens = new List<Tween>();
        for (int i = 0; i < newCards.Count; i++)
        {
            StartCenterMove(newCards[i], centerTweens);
            if (drawLaunchInterval > 0f && i < newCards.Count - 1)
            {
                yield return new WaitForSeconds(drawLaunchInterval);
            }
        }
        yield return WaitForTweens(centerTweens);

        List<Tween> lineTweens = new List<Tween>();
        for (int i = 0; i < newCards.Count; i++)
        {
            StartLineSpread(newCards[i], GetRevealPosition(i, newCards.Count), lineTweens);
        }
        yield return WaitForTweens(lineTweens);

        for (int i = 0; i < newCards.Count; i++)
        {
            CardUI card = newCards[i];
            if (card == null) continue;

            card.transform.SetParent(cardManager.handUIContainer, false);
            RectTransform rect = card.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.localPosition = GetRevealPosition(i, newCards.Count);
                rect.localScale = Vector3.one * cardManager.cardSpawnScale;
                rect.localRotation = Quaternion.identity;
            }

            card.handAreaRect = cardManager.handUIContainer as RectTransform;
        }

        RefreshLayout();
        if (finalSpreadDuration > 0f)
        {
            yield return new WaitForSeconds(finalSpreadDuration);
        }

        cardManager.NotifyPileStateChanged();
    }

    private void StartCenterMove(CardUI card, List<Tween> tweens)
    {
        if (card == null)
        {
            return;
        }

        RectTransform rect = card.GetComponent<RectTransform>();
        if (rect == null)
        {
            return;
        }

        Sequence sequence = DOTween.Sequence();
        sequence.Append(rect.DOLocalMove(Vector3.zero, drawMoveDuration).SetEase(drawMoveEase));
        sequence.Join(
            rect.DOScale(Vector3.one * cardManager.cardSpawnScale, drawMoveDuration)
                .SetEase(drawMoveEase));
        sequence.Join(
            rect.DOLocalRotate(Vector3.zero, drawMoveDuration)
                .SetEase(drawMoveEase));
        tweens.Add(sequence);
    }

    private void StartLineSpread(CardUI card, Vector3 targetPosition, List<Tween> tweens)
    {
        if (card == null)
        {
            return;
        }

        tweens.Add(
            card.transform.DOLocalMove(targetPosition, lineSpreadDuration)
                .SetEase(lineSpreadEase));
    }

    private Vector3 GetRevealPosition(int index, int count)
    {
        if (count <= 1)
        {
            return Vector3.zero;
        }

        float availableSpacing = maxRevealWidth / (count - 1);
        float spacing = Mathf.Min(revealSpacing, availableSpacing);
        float midpoint = (count - 1) * 0.5f;
        return new Vector3((index - midpoint) * spacing, 0f, 0f);
    }

    private CardUI CreateDrawCardView(
        CardData cardData,
        Vector3 drawPosition,
        float pileScale,
        int visualIndex)
    {
        CardUI cardUI = InstantiateCardView(cardData, drawPosition, pileScale);
        if (cardUI == null)
        {
            return null;
        }

        SetCardRaycasts(cardUI, false);
        GetCardCanvasGroup(cardUI).alpha = 1f;

        RectTransform rect = cardUI.GetComponent<RectTransform>();
        if (rect != null)
        {
            float startAngle = visualIndex % 2 == 0 ? -8f : 8f;
            rect.localRotation = Quaternion.Euler(0f, 0f, startAngle);
        }

        return cardUI;
    }

    private CardUI InstantiateCardView(
        CardData data,
        Vector3 worldPosition,
        float scale,
        Transform parentOverride = null)
    {
        Transform parent = parentOverride != null
            ? parentOverride
            : flightLayer != null
            ? flightLayer
            : cardManager.handUIContainer;
        GameObject cardObject = Instantiate(cardManager.cardPrefab, parent);
        RectTransform rect = cardObject.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.position = worldPosition;
            rect.localScale = Vector3.one * scale;
        }

        CardUI cardUI = cardObject.GetComponent<CardUI>();
        if (cardUI != null)
        {
            cardUI.Setup(data);
        }

        return cardUI;
    }

    private void CompleteDiscardedCard(CardUI card)
    {
        if (card == null)
        {
            return;
        }

        CardData cardData = card.CurrentCardData;
        if (cardData != null)
        {
            cardManager.hand.Remove(cardData);
            if (cardData.type != CardType.Special)
            {
                cardManager.discardPile.Add(cardData);
            }
        }

        Destroy(card.gameObject);
        cardManager.NotifyPileStateChanged();
    }

    private List<CardUI> GetHandCardViews()
    {
        List<CardUI> cards = new List<CardUI>();
        if (cardManager == null || cardManager.handUIContainer == null)
        {
            return cards;
        }

        foreach (Transform child in cardManager.handUIContainer)
        {
            CardUI card = child.GetComponent<CardUI>();
            if (card != null)
            {
                cards.Add(card);
            }
        }

        return cards;
    }

    private RectTransform CreateCardAnimationLayer(string layerName)
    {
        RectTransform source = cardManager.handUIContainer as RectTransform;
        if (source == null || source.parent == null)
        {
            return source;
        }

        GameObject layerObject = new GameObject(layerName, typeof(RectTransform));
        RectTransform layer = layerObject.GetComponent<RectTransform>();
        layer.SetParent(source.parent, false);
        layer.anchorMin = source.anchorMin;
        layer.anchorMax = source.anchorMax;
        layer.pivot = source.pivot;
        layer.anchoredPosition = source.anchoredPosition;
        layer.sizeDelta = source.sizeDelta;
        layer.localRotation = source.localRotation;
        layer.localScale = source.localScale;
        layer.SetAsLastSibling();
        return layer;
    }

    private void ResolvePileTargets()
    {
        if (drawPileTarget == null)
        {
            drawPileTarget = FindRectTransform("DrawPile");
        }

        if (discardPileTarget == null)
        {
            discardPileTarget = FindRectTransform("DiscardPile");
        }
    }

    private static RectTransform FindRectTransform(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        return target != null ? target.GetComponent<RectTransform>() : null;
    }

    private Vector3 GetPileLocalPosition(RectTransform pile, Vector3 fallback)
    {
        RectTransform coordinateSpace = flightLayer != null
            ? flightLayer
            : cardManager.handUIContainer as RectTransform;
        return GetPileLocalPosition(pile, coordinateSpace, fallback);
    }

    private Vector3 GetPileLocalPosition(
        RectTransform pile,
        RectTransform coordinateSpace,
        Vector3 fallback)
    {
        if (pile == null || coordinateSpace == null)
        {
            return fallback;
        }

        Vector3 pileWorldCenter = pile.TransformPoint(pile.rect.center);
        return coordinateSpace.InverseTransformPoint(pileWorldCenter);
    }

    private Vector3 GetPileWorldPosition(RectTransform pile, Vector3 fallbackLocalPosition)
    {
        if (pile != null)
        {
            return pile.TransformPoint(pile.rect.center);
        }

        RectTransform coordinateSpace = flightLayer != null
            ? flightLayer
            : cardManager.handUIContainer as RectTransform;
        return coordinateSpace != null
            ? coordinateSpace.TransformPoint(fallbackLocalPosition)
            : fallbackLocalPosition;
    }

    private float GetPileCardScale()
    {
        RectTransform prefabRect = cardManager.cardPrefab != null
            ? cardManager.cardPrefab.GetComponent<RectTransform>()
            : null;
        if (discardPileTarget != null &&
            prefabRect != null &&
            prefabRect.rect.width > 0f &&
            prefabRect.rect.height > 0f)
        {
            float scaleX = discardPileTarget.rect.width / prefabRect.rect.width;
            float scaleY = discardPileTarget.rect.height / prefabRect.rect.height;
            return Mathf.Clamp(Mathf.Min(scaleX, scaleY), 0.05f, 1f) *
                   cardManager.cardSpawnScale;
        }

        return 0.35f * cardManager.cardSpawnScale;
    }

    private Vector3[] BuildArcPath(Vector3 start, Vector3 end)
    {
        Vector3 midpoint = (start + end) * 0.5f;
        midpoint.y += arcHeight;
        return new[] { start, midpoint, end };
    }

    private IEnumerator WaitForTweens(List<Tween> tweens)
    {
        foreach (Tween tween in tweens)
        {
            if (tween != null && tween.IsActive())
            {
                yield return tween.WaitForCompletion();
            }
        }
    }

    private static CanvasGroup GetCardCanvasGroup(CardUI card)
    {
        CanvasGroup canvasGroup = card.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = card.gameObject.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }

    private static void SetCardRaycasts(CardUI card, bool enabled)
    {
        if (card == null)
        {
            return;
        }

        CanvasGroup canvasGroup = GetCardCanvasGroup(card);
        canvasGroup.blocksRaycasts = enabled;
        canvasGroup.interactable = enabled;
    }

    private void OnDisable()
    {
        if (runtimeDrawCoroutine != null)
        {
            StopCoroutine(runtimeDrawCoroutine);
            runtimeDrawCoroutine = null;
        }

        runtimeDrawQueue.Clear();

        if (flightLayer != null)
        {
            Destroy(flightLayer.gameObject);
            flightLayer = null;
        }

        isPlaying = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        float halfW = maxSafeVisualWidth * 0.5f;

        Vector3 left = transform.TransformPoint(new Vector3(-halfW, 0f, 0f));
        Vector3 right = transform.TransformPoint(new Vector3(halfW, 0f, 0f));

        Gizmos.DrawLine(left, right);
        Gizmos.DrawLine(
            transform.TransformPoint(new Vector3(-halfW, -60f, 0f)),
            transform.TransformPoint(new Vector3(-halfW, 60f, 0f)));
        Gizmos.DrawLine(
            transform.TransformPoint(new Vector3(halfW, -60f, 0f)),
            transform.TransformPoint(new Vector3(halfW, 60f, 0f)));
    }
#endif
}
