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

        [Header("Timer")]
        public float timeLimitSeconds;
        public float bonusTimePerMatch;

        public int TotalCards => columns * rows;
    }

    [Header("Debug Stage Navigation")]
    [SerializeField] private int debugStageNumber = 1;

    [Header("References")]
    [SerializeField] private MemoryBoardSpawner boardSpawner;

    [Header("Save Progress")]
    [SerializeField] private bool loadSavedProgressOnStart = true;

    private const string SavedStageKey = "MemoryFlip_SavedStage";
    private const string GameCompletedKey = "MemoryFlip_GameCompleted";

    [Header("Stages")]
    [SerializeField]
    private StageLayout[] stages =
    {
        new StageLayout { columns = 3,  rows = 2, timeLimitSeconds = 30f,  bonusTimePerMatch = 3f },
        new StageLayout { columns = 4,  rows = 3, timeLimitSeconds = 45f,  bonusTimePerMatch = 3f },
        new StageLayout { columns = 6,  rows = 3, timeLimitSeconds = 65f,  bonusTimePerMatch = 4f },
        new StageLayout { columns = 6,  rows = 4, timeLimitSeconds = 85f,  bonusTimePerMatch = 4f },
        new StageLayout { columns = 10, rows = 3, timeLimitSeconds = 100f, bonusTimePerMatch = 5f },
        new StageLayout { columns = 9,  rows = 4, timeLimitSeconds = 120f, bonusTimePerMatch = 5f },
        new StageLayout { columns = 8,  rows = 5, timeLimitSeconds = 140f, bonusTimePerMatch = 6f }
    };

    [Header("Gameplay")]
    [SerializeField] private float wrongCardsDelay = 0.8f;
    [SerializeField] private float nextStageDelay = 1.2f;

    [Header("Sound Effects")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip flipSfx;
    [SerializeField] private AudioClip correctMatchSfx;
    [SerializeField] private AudioClip wrongMatchSfx;

    public ReactiveProperty<int> Moves { get; } = new ReactiveProperty<int>(0);
    public ReactiveProperty<int> MatchedPairs { get; } = new ReactiveProperty<int>(0);
    public ReactiveProperty<int> TotalPairs { get; } = new ReactiveProperty<int>(0);

    public ReactiveProperty<bool> IsGameFinished { get; } = new ReactiveProperty<bool>(false);
    public ReactiveProperty<bool> IsAllStagesFinished { get; } = new ReactiveProperty<bool>(false);

    public ReactiveProperty<int> CurrentStage { get; } = new ReactiveProperty<int>(1);

    public ReactiveProperty<float> TimeLeft { get; } = new ReactiveProperty<float>(0f);
    public ReactiveProperty<bool> IsTimeUp { get; } = new ReactiveProperty<bool>(false);

    public int TotalStages => stages != null ? stages.Length : 0;

    private readonly List<CardView> selectedCards = new List<CardView>();

    private bool isFlipping;
    private bool isCheckingPair;
    private bool isStageTransitioning;
    private bool isInputLocked;

    private int totalPairs;
    private int currentStageIndex;

    private CancellationTokenSource timerCts;

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

        StopStageTimer();

        if (loadSavedProgressOnStart)
        {
            currentStageIndex = GetSavedStageIndex();
        }
        else
        {
            currentStageIndex = 0;
        }

        IsAllStagesFinished.Value = false;
        IsTimeUp.Value = false;
        IsGameFinished.Value = false;

        LoadCurrentStage();
    }

    private void LoadCurrentStage()
    {
        StageLayout stage = stages[currentStageIndex];

        if (stage.TotalCards <= 0 || stage.TotalCards % 2 != 0)
        {
            Debug.LogError($"Stage {currentStageIndex + 1} لازم يكون عدد كروته زوجي.");
            return;
        }

        CurrentStage.Value = currentStageIndex + 1;

        Debug.Log(
            $"Starting Stage {CurrentStage.Value}: {stage.TotalCards} Cards | Time: {stage.timeLimitSeconds}"
        );

        boardSpawner.BuildBoard(stage.columns, stage.rows);
    }

    public void StartNewGame(int newTotalPairs)
    {
        StopStageTimer();

        totalPairs = newTotalPairs;

        Moves.Value = 0;
        MatchedPairs.Value = 0;
        TotalPairs.Value = newTotalPairs;

        IsGameFinished.Value = false;
        IsTimeUp.Value = false;

        StageLayout stage = stages[currentStageIndex];
        TimeLeft.Value = stage.timeLimitSeconds;

        selectedCards.Clear();

        isFlipping = false;
        isCheckingPair = false;
        isStageTransitioning = false;
        isInputLocked = false;
    }

    public void SetInputLocked(bool locked)
    {
        isInputLocked = locked;
    }

    public void TryFlip(CardView card)
    {
        if (card == null)
            return;

        if (isInputLocked)
            return;

        if (IsGameFinished.Value)
            return;

        if (IsTimeUp.Value)
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

    private async UniTaskVoid FlipCardAsync(CardView card, CancellationToken token)
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

    private async UniTaskVoid CheckPairAsync(CancellationToken token)
    {
        try
        {
            isCheckingPair = true;

            if (selectedCards.Count < 2)
                return;

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

                AddBonusTimeForMatch();

                selectedCards.Clear();

                if (MatchedPairs.Value >= totalPairs)
                {
                    IsGameFinished.Value = true;

                    StopStageTimer();

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

    private async UniTaskVoid AdvanceToNextStageAsync(CancellationToken token)
    {
        try
        {
            isStageTransitioning = true;
            isInputLocked = true;

            await UniTask.Delay(
                TimeSpan.FromSeconds(nextStageDelay),
                cancellationToken: token
            );

            bool isLastStage = currentStageIndex >= stages.Length - 1;

            if (isLastStage)
            {
                SaveGameCompleted();

                IsAllStagesFinished.Value = true;

                Debug.Log("Congratulations! You completed all stages.");

                return;
            }

            currentStageIndex++;

            SaveReachedStage(currentStageIndex + 1);

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

    public void StartStageTimer()
    {
        StopStageTimer();

        if (IsGameFinished.Value)
            return;

        if (IsTimeUp.Value)
            return;

        StageLayout stage = stages[currentStageIndex];

        if (stage.timeLimitSeconds <= 0f)
            return;

        timerCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy()
        );

        RunStageTimerAsync(timerCts.Token).Forget();
    }

    private async UniTaskVoid RunStageTimerAsync(CancellationToken token)
    {
        try
        {
            while (TimeLeft.Value > 0f && !IsGameFinished.Value && !IsTimeUp.Value)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);

                if (isInputLocked || isStageTransitioning)
                    continue;

                TimeLeft.Value -= Time.deltaTime;

                if (TimeLeft.Value < 0f)
                    TimeLeft.Value = 0f;
            }

            if (!IsGameFinished.Value && !IsTimeUp.Value && TimeLeft.Value <= 0f)
            {
                OnTimeUp();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void StopStageTimer()
    {
        if (timerCts == null)
            return;

        timerCts.Cancel();
        timerCts.Dispose();
        timerCts = null;
    }

    private void AddBonusTimeForMatch()
    {
        if (IsGameFinished.Value || IsTimeUp.Value)
            return;

        StageLayout stage = stages[currentStageIndex];

        if (stage.bonusTimePerMatch <= 0f)
            return;

        TimeLeft.Value += stage.bonusTimePerMatch;

        Debug.Log($"+{stage.bonusTimePerMatch} seconds");
    }

    private void OnTimeUp()
    {
        IsTimeUp.Value = true;
        IsGameFinished.Value = true;
        isInputLocked = true;

        selectedCards.Clear();

        StopStageTimer();

        Debug.Log("Time Up!");
    }

    private void PlaySfx(AudioClip clip)
    {
        if (sfxSource == null || clip == null)
            return;

        sfxSource.PlayOneShot(clip);
    }

    private int GetSavedStageIndex()
    {
        int savedStageNumber = PlayerPrefs.GetInt(SavedStageKey, 1);

        savedStageNumber = Mathf.Clamp(
            savedStageNumber,
            1,
            stages.Length
        );

        return savedStageNumber - 1;
    }

    private void SaveReachedStage(int stageNumber)
    {
        if (stages == null || stages.Length == 0)
            return;

        int clampedStageNumber = Mathf.Clamp(
            stageNumber,
            1,
            stages.Length
        );

        int savedStageNumber = PlayerPrefs.GetInt(SavedStageKey, 1);

        if (clampedStageNumber <= savedStageNumber)
            return;

        PlayerPrefs.SetInt(SavedStageKey, clampedStageNumber);
        PlayerPrefs.SetInt(GameCompletedKey, 0);
        PlayerPrefs.Save();

        Debug.Log($"Saved Progress: Stage {clampedStageNumber}");
    }

    private void SaveGameCompleted()
    {
        PlayerPrefs.SetInt(SavedStageKey, stages.Length);
        PlayerPrefs.SetInt(GameCompletedKey, 1);
        PlayerPrefs.Save();

        Debug.Log("Saved Progress: Game Completed");
    }

    [ContextMenu("Save/Reset Saved Progress")]
    public void ResetSavedProgress()
    {
        PlayerPrefs.DeleteKey(SavedStageKey);
        PlayerPrefs.DeleteKey(GameCompletedKey);
        PlayerPrefs.Save();

        Debug.Log("Saved Progress Reset");
    }

    [ContextMenu("Save/Restart From Stage 1")]
    public void RestartFromStageOne()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Restart From Stage 1 يشتغل فقط أثناء Play Mode.");
            return;
        }

        ResetSavedProgress();

        StopStageTimer();

        currentStageIndex = 0;

        IsAllStagesFinished.Value = false;
        IsTimeUp.Value = false;
        IsGameFinished.Value = false;

        LoadCurrentStage();
    }

    [ContextMenu("Debug/Load Selected Stage")]
    private void DebugLoadSelectedStage()
    {
        LoadStageByNumber(debugStageNumber);
    }

    [ContextMenu("Debug/Next Stage")]
    private void DebugNextStage()
    {
        LoadStageByNumber(CurrentStage.Value + 1);
    }

    [ContextMenu("Debug/Previous Stage")]
    private void DebugPreviousStage()
    {
        LoadStageByNumber(CurrentStage.Value - 1);
    }

    public void LoadStageByNumber(int stageNumber)
    {
        if (stages == null || stages.Length == 0)
        {
            Debug.LogError("ما في Stages معرفة داخل MemoryGameManager.");
            return;
        }

        StopStageTimer();

        int stageIndex = stageNumber - 1;

        stageIndex = Mathf.Clamp(
            stageIndex,
            0,
            stages.Length - 1
        );

        currentStageIndex = stageIndex;

        selectedCards.Clear();

        isFlipping = false;
        isCheckingPair = false;
        isStageTransitioning = false;
        isInputLocked = false;

        IsGameFinished.Value = false;
        IsTimeUp.Value = false;
        IsAllStagesFinished.Value = false;

        LoadCurrentStage();
    }

    private void OnDestroy()
    {
        StopStageTimer();

        Moves.Dispose();
        MatchedPairs.Dispose();
        TotalPairs.Dispose();

        IsGameFinished.Dispose();
        IsAllStagesFinished.Dispose();

        CurrentStage.Dispose();

        TimeLeft.Dispose();
        IsTimeUp.Dispose();
    }
}