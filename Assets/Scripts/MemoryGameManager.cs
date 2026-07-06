using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

public class MemoryGameManager : MonoBehaviour
{
    [Serializable]
    public struct StageLayout
    {
        public int columns;
        public int rows;

        public int TotalCards => columns * rows;
    }

    [Header("References")]
    [SerializeField] private MemoryBoardSpawner boardSpawner;

    [Header("Stages")]
    [SerializeField] private StageLayout[] stages =
    {
        new StageLayout { columns = 3, rows = 2 }, // 6 Cards
        new StageLayout { columns = 4, rows = 3 }, // 12 Cards
        new StageLayout { columns = 6, rows = 3 }, // 18 Cards
        new StageLayout { columns = 6, rows = 4 }, // 24 Cards
        new StageLayout { columns = 6, rows = 5 }, // 30 Cards
        new StageLayout { columns = 6, rows = 6 }  // 36 Cards
    };

    [Header("Sound Effects")]
    [SerializeField] private AudioSource sfxSource;

    [SerializeField] private AudioClip flipSfx;
    [SerializeField] private AudioClip correctMatchSfx;
    [SerializeField] private AudioClip wrongMatchSfx;

    [Header("Gameplay")]
    [SerializeField] private float wrongCardsDelay = 0.8f;
    [SerializeField] private float nextStageDelay = 1.2f;

    // R3 Game State
    public ReactiveProperty<int> Moves { get; } = new ReactiveProperty<int>(0);
    public ReactiveProperty<int> MatchedPairs { get; } = new ReactiveProperty<int>(0);
    public ReactiveProperty<bool> IsGameFinished { get; } = new ReactiveProperty<bool>(false);

    public ReactiveProperty<int> CurrentStage { get; } = new ReactiveProperty<int>(1);
    public ReactiveProperty<bool> IsAllStagesFinished { get; } = new ReactiveProperty<bool>(false);

    private readonly List<CardView> selectedCards = new List<CardView>();

    private bool isFlipping;
    private bool isCheckingPair;
    private bool isStageTransitioning;

    private int totalPairs;
    private int currentStageIndex;

    private void Start()
    {
        StartGame();
    }

    public void StartGame()
    {
        if (boardSpawner == null)
        {
            Debug.LogError("اربط Board Spawner داخل MemoryGameManager.");
            return;
        }

        if (stages == null || stages.Length == 0)
        {
            Debug.LogError("ما في مراحل داخل Stages.");
            return;
        }

        currentStageIndex = 0;
        IsAllStagesFinished.Value = false;

        LoadCurrentStage();
    }

    private void LoadCurrentStage()
    {
        StageLayout stage = stages[currentStageIndex];

        if (stage.TotalCards <= 0 || stage.TotalCards % 2 != 0)
        {
            Debug.LogError(
                $"Stage {currentStageIndex + 1} لازم يحتوي عدد كروت زوجي."
            );
            return;
        }

        CurrentStage.Value = currentStageIndex + 1;

        Debug.Log(
            $"Starting Stage {CurrentStage.Value}: {stage.TotalCards} Cards"
        );

        boardSpawner.BuildBoard(stage.columns, stage.rows);
    }

    public void StartNewGame(int newTotalPairs)
    {
        totalPairs = newTotalPairs;

        Moves.Value = 0;
        MatchedPairs.Value = 0;
        IsGameFinished.Value = false;

        selectedCards.Clear();

        isFlipping = false;
        isCheckingPair = false;
    }

    public void TryFlip(CardView card)
    {
        if (card == null)
            return;

        if (IsGameFinished.Value)
            return;

        if (isFlipping || isCheckingPair || isStageTransitioning)
            return;

        if (card.IsFaceUp || card.IsMatched)
            return;

        FlipCardAsync(
            card,
            this.GetCancellationTokenOnDestroy()
        ).Forget();
    }

    private async UniTask FlipCardAsync(
        CardView card,
        CancellationToken token)
    {
        try
        {
            isFlipping = true;

            PlaySfx(flipSfx);

            await card.FlipUpAsync(token);

            selectedCards.Add(card);

            if (selectedCards.Count == 2)
            {
                CheckPairAsync(token).Forget();
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            isFlipping = false;
        }
    }

    private async UniTask CheckPairAsync(CancellationToken token)
    {
        try
        {
            isCheckingPair = true;

            CardView firstCard = selectedCards[0];
            CardView secondCard = selectedCards[1];

            Moves.Value++;

            bool isMatch = firstCard.IsSamePair(secondCard);

            if (isMatch)
            {
                 PlaySfx(correctMatchSfx);

                firstCard.MarkMatched();
                secondCard.MarkMatched();

                MatchedPairs.Value++;

                selectedCards.Clear();

                if (MatchedPairs.Value >= totalPairs)
                {
                    IsGameFinished.Value = true;

                    AdvanceToNextStageAsync(token).Forget();
                }

                return;
            }
            
            PlaySfx(wrongMatchSfx);

            await UniTask.Delay(
                TimeSpan.FromSeconds(wrongCardsDelay),
                cancellationToken: token
            );

            await UniTask.WhenAll(
                firstCard.FlipDownAsync(token),
                secondCard.FlipDownAsync(token)
            );

            selectedCards.Clear();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            isCheckingPair = false;
        }
    }

    private async UniTask AdvanceToNextStageAsync(CancellationToken token)
    {
        try
        {
            isStageTransitioning = true;

            await UniTask.Delay(
                TimeSpan.FromSeconds(nextStageDelay),
                cancellationToken: token
            );

            bool isLastStage = currentStageIndex >= stages.Length - 1;

            if (isLastStage)
            {
                IsAllStagesFinished.Value = true;

                Debug.Log("Congratulations! You completed all stages.");

                return;
            }

            currentStageIndex++;

            LoadCurrentStage();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            isStageTransitioning = false;
        }
    }

    private void OnDestroy()
    {
        Moves.Dispose();
        MatchedPairs.Dispose();
        IsGameFinished.Dispose();
        CurrentStage.Dispose();
        IsAllStagesFinished.Dispose();
    }

    private void PlaySfx(AudioClip clip)
    {
        if (sfxSource == null || clip == null)
        return;

        sfxSource.PlayOneShot(clip);
    }
}