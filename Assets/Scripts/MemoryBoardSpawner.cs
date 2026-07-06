using System.Collections.Generic;
using UnityEngine;

public class MemoryBoardSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MemoryGameManager gameManager;
    [SerializeField] private CardView cardPrefab;

    [Header("Resources")]
    [Tooltip("اسم ملف الـSprite Sheet داخل Resources بدون الامتداد")]
    [SerializeField] private string pokerCardsResourcePath = "1.2 Poker cards";

    [Header("Card Back Settings")]
    [Tooltip("ظهر الكرت الذي يظهر على كل الكروت عند بداية المرحلة")]
    [SerializeField] private Sprite selectedBackSprite;

    [Tooltip("حط كل الكروت الخلفية الملونة هنا حتى لا تظهر كوجوه")]
    [SerializeField] private Sprite[] excludedBackSprites;

    [Header("Current Stage Grid")]
    [SerializeField] private int columns = 3;
    [SerializeField] private int rows = 2;

    [Header("Auto Layout")]
    [SerializeField] private Camera gameplayCamera;

    [Range(0.3f, 0.95f)]
    [SerializeField] private float boardWidthFill = 0.72f;

    [Range(0.3f, 0.95f)]
    [SerializeField] private float boardHeightFill = 0.75f;

    [Range(0f, 1f)]
    [SerializeField] private float horizontalGapRatio = 0.18f;

    [Range(0f, 1f)]
    [SerializeField] private float verticalGapRatio = 0.16f;

    [SerializeField] private float minCardScale = 0.75f;
    [SerializeField] private float maxCardScale = 2.5f;

    private Sprite[] allSprites;

    private readonly List<CardView> spawnedCards =
        new List<CardView>();

    private void Awake()
    {
        LoadSpritesFromResources();
    }

    public void BuildBoard(int newColumns, int newRows)
    {
        columns = newColumns;
        rows = newRows;

        BuildBoard();
    }

    [ContextMenu("Build Board")]
    public void BuildBoard()
    {
        int totalCards = columns * rows;

        if (totalCards % 2 != 0)
        {
            Debug.LogError("Columns × Rows لازم يكون رقم زوجي.");
            return;
        }

        if (gameManager == null)
        {
            Debug.LogError("اربط Game Manager داخل MemoryBoardSpawner.");
            return;
        }

        if (cardPrefab == null)
        {
            Debug.LogError("اربط Card Prefab داخل MemoryBoardSpawner.");
            return;
        }

        if (selectedBackSprite == null)
        {
            Debug.LogError("اسحب ظهر كرت إلى Selected Back Sprite.");
            return;
        }

        if (allSprites == null || allSprites.Length == 0)
        {
            LoadSpritesFromResources();

            if (allSprites == null || allSprites.Length == 0)
                return;
        }

        int pairCount = totalCards / 2;

        List<Sprite> availableFaces = GetAvailableFaceSprites();

        if (availableFaces.Count < pairCount)
        {
            Debug.LogError(
                $"المرحلة تحتاج {pairCount} وجوه مختلفة، لكن المتاح {availableFaces.Count} فقط."
            );
            return;
        }

        ClearBoard();

        gameManager.StartNewGame(pairCount);

        Shuffle(availableFaces);

        List<Sprite> selectedFaces =
            availableFaces.GetRange(0, pairCount);

        List<int> shuffledPairIds =
            CreateShuffledPairIds(pairCount);

        CalculateBoardLayout(
            out float cardScale,
            out float horizontalSpacing,
            out float verticalSpacing
        );

        float boardWidth = (columns - 1) * horizontalSpacing;
        float boardHeight = (rows - 1) * verticalSpacing;

        for (int i = 0; i < totalCards; i++)
        {
            int column = i % columns;
            int row = i / columns;

            float x = column * horizontalSpacing - boardWidth * 0.5f;
            float y = -(row * verticalSpacing) + boardHeight * 0.5f;

            int pairId = shuffledPairIds[i];

            CardView card = Instantiate(cardPrefab, transform);

            card.transform.localPosition = new Vector3(x, y, 0f);
            card.transform.localScale = Vector3.one * cardScale;

            card.Initialize(
                pairId,
                selectedFaces[pairId],
                selectedBackSprite,
                gameManager
            );

            spawnedCards.Add(card);
        }
    }

    private void CalculateBoardLayout(
        out float cardScale,
        out float horizontalSpacing,
        out float verticalSpacing)
    {
        cardScale = 1f;
        horizontalSpacing = 1.2f;
        verticalSpacing = 1.6f;

        Camera targetCamera = gameplayCamera != null
            ? gameplayCamera
            : Camera.main;

        SpriteRenderer prefabRenderer =
            cardPrefab.GetComponent<SpriteRenderer>();

        if (targetCamera == null)
        {
            Debug.LogWarning(
                "ما لقينا Camera. اربط Main Camera داخل Gameplay Camera."
            );

            return;
        }

        if (!targetCamera.orthographic)
        {
            Debug.LogWarning(
                "لازم الكاميرا تكون Orthographic حتى الـAuto Layout يشتغل بدقة."
            );

            return;
        }

        if (prefabRenderer == null || prefabRenderer.sprite == null)
        {
            Debug.LogWarning(
                "ما لقينا SpriteRenderer أو Sprite داخل Card Prefab."
            );

            return;
        }

        Vector2 baseCardSize = prefabRenderer.sprite.bounds.size;

        float cameraHeight = targetCamera.orthographicSize * 2f;
        float cameraWidth = cameraHeight * targetCamera.aspect;

        float usableWidth = cameraWidth * boardWidthFill;
        float usableHeight = cameraHeight * boardHeightFill;

        float gridWidthAtScaleOne =
            columns * baseCardSize.x +
            (columns - 1) * baseCardSize.x * horizontalGapRatio;

        float gridHeightAtScaleOne =
            rows * baseCardSize.y +
            (rows - 1) * baseCardSize.y * verticalGapRatio;

        float scaleByWidth = usableWidth / gridWidthAtScaleOne;
        float scaleByHeight = usableHeight / gridHeightAtScaleOne;

        cardScale = Mathf.Min(scaleByWidth, scaleByHeight);
        cardScale = Mathf.Clamp(
            cardScale,
            minCardScale,
            maxCardScale
        );

        horizontalSpacing =
            baseCardSize.x * cardScale * (1f + horizontalGapRatio);

        verticalSpacing =
            baseCardSize.y * cardScale * (1f + verticalGapRatio);
    }

    private void LoadSpritesFromResources()
    {
        allSprites = Resources.LoadAll<Sprite>(pokerCardsResourcePath);

        if (allSprites == null || allSprites.Length == 0)
        {
            Debug.LogError(
                $"ما لقينا Sprites داخل Resources باسم: {pokerCardsResourcePath}"
            );
        }
    }

    private List<Sprite> GetAvailableFaceSprites()
    {
        List<Sprite> faces = new List<Sprite>();

        HashSet<Sprite> allBackSprites = new HashSet<Sprite>();

        if (selectedBackSprite != null)
        {
            allBackSprites.Add(selectedBackSprite);
        }

        foreach (Sprite backSprite in excludedBackSprites)
        {
            if (backSprite != null)
            {
                allBackSprites.Add(backSprite);
            }
        }

        // حماية من الـSlices المكررة لنفس الكرت.
        HashSet<string> usedSpriteRects = new HashSet<string>();

        foreach (Sprite sprite in allSprites)
        {
            if (sprite == null)
                continue;

            // يمنع أي Back Card من الدخول كوجه أمامي.
            if (allBackSprites.Contains(sprite))
                continue;

            Rect rect = sprite.rect;

            string rectKey =
                $"{rect.x}_{rect.y}_{rect.width}_{rect.height}";

            // يمنع الـSlice المكرر لنفس مكان الكرت.
            if (!usedSpriteRects.Add(rectKey))
                continue;

            faces.Add(sprite);
        }

        return faces;
    }

    private List<int> CreateShuffledPairIds(int pairCount)
    {
        List<int> pairIds = new List<int>();

        for (int i = 0; i < pairCount; i++)
        {
            pairIds.Add(i);
            pairIds.Add(i);
        }

        Shuffle(pairIds);

        return pairIds;
    }

    private void ClearBoard()
    {
        foreach (CardView card in spawnedCards)
        {
            if (card == null)
                continue;

            // يخفي الكروت مباشرة قبل Destroy.
            card.gameObject.SetActive(false);
            Destroy(card.gameObject);
        }

        spawnedCards.Clear();
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);

            T temporary = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temporary;
        }
    }
}