using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class CardView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer cardRenderer;

    [Header("Flip Settings")]
    [SerializeField] private float flipDuration = 0.15f;

    public bool IsFaceUp => isFaceUp;
    public bool IsMatched => isMatched;

    private Sprite frontSprite;
    private Sprite backSprite;

    private int pairId;

    private bool isFaceUp;
    private bool isMatched;

    private MemoryGameManager gameManager;

    public void Initialize(
        int newPairId,
        Sprite newFrontSprite,
        Sprite newBackSprite,
        MemoryGameManager newGameManager)
    {
        transform.DOKill();

        pairId = newPairId;
        frontSprite = newFrontSprite;
        backSprite = newBackSprite;
        gameManager = newGameManager;

        isFaceUp = false;
        isMatched = false;

        cardRenderer.sprite = backSprite;
        cardRenderer.color = Color.white;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x);
        transform.localScale = scale;
    }

    private void OnMouseDown()
    {
        if (gameManager != null)
        {
            gameManager.TryFlip(this);
        }
    }

    public async UniTask FlipUpAsync(CancellationToken token)
    {
        if (isFaceUp || isMatched)
            return;

        await PlayFlipAnimationAsync(frontSprite, token);

        isFaceUp = true;
    }

    public async UniTask FlipDownAsync(CancellationToken token)
    {
        if (!isFaceUp || isMatched)
            return;

        await PlayFlipAnimationAsync(backSprite, token);

        isFaceUp = false;
    }

    public bool IsSamePair(CardView otherCard)
    {
        return otherCard != null && pairId == otherCard.pairId;
    }

    public void MarkMatched()
    {
        isMatched = true;

        transform.DOKill();

        transform
            .DOPunchScale(Vector3.one * 0.12f, 0.25f, 5, 0.5f)
            .SetLink(gameObject);
    }

    private async UniTask PlayFlipAnimationAsync(
        Sprite targetSprite,
        CancellationToken token)
    {
        transform.DOKill();

        float originalScaleX = Mathf.Abs(transform.localScale.x);

        UniTaskCompletionSource completionSource = new UniTaskCompletionSource();

        Sequence sequence = DOTween.Sequence()
            .Append(transform.DOScaleX(0f, flipDuration).SetEase(Ease.InQuad))
            .AppendCallback(() =>
            {
                cardRenderer.sprite = targetSprite;
            })
            .Append(transform.DOScaleX(originalScaleX, flipDuration).SetEase(Ease.OutQuad))
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                completionSource.TrySetResult();
            });

        using (token.Register(() =>
        {
            if (sequence.IsActive())
            {
                sequence.Kill();
            }

            completionSource.TrySetCanceled(token);
        }))
        {
            await completionSource.Task;
        }
    }

    private void OnDestroy()
    {
        transform.DOKill();
    }
}