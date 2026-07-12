using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MemoryGameManager : MonoBehaviour
{
    #region Inspector Fields

    [Header("Debug Stage Navigation")]
    [SerializeField] private int debugStageNumber = 1;

    [Header("References")]
    [SerializeField] private MemoryBoardSpawner boardSpawner;

    [Header("Scenes")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Levels JSON")]
    [Tooltip("اسم ملف JSON داخل Resources بدون .json")]
    [SerializeField] private string levelsJsonResourcePath = "MemoryLevels";

    [Header("Save Progress")]
    [SerializeField] private bool loadSavedProgressOnStart = true;

    [Header("Gameplay")]
    [SerializeField] private float wrongCardsDelay = 0.8f;
    [SerializeField] private float stageCompletePanelDelay = 1.2f;

    [Header("Sound Effects")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip flipSfx;
    [SerializeField] private AudioClip correctMatchSfx;
    [SerializeField] private AudioClip wrongMatchSfx;

    #endregion


    #region Save Keys

    private const string SavedStageKey = "MemoryFlip_SavedStage";
    private const string GameCompletedKey = "MemoryFlip_GameCompleted";

    #endregion


    #region Reactive Properties - R3

    public ReactiveProperty<int> Moves { get; } = new ReactiveProperty<int>(0);
    public ReactiveProperty<int> MatchedPairs { get; } = new ReactiveProperty<int>(0);
    public ReactiveProperty<int> TotalPairs { get; } = new ReactiveProperty<int>(0);

    public ReactiveProperty<int> CurrentStage { get; } = new ReactiveProperty<int>(1);

    public ReactiveProperty<float> TimeLeft { get; } = new ReactiveProperty<float>(0f);
    public ReactiveProperty<float> StageTimeLimit { get; } = new ReactiveProperty<float>(0f);

    public ReactiveProperty<bool> IsGameFinished { get; } = new ReactiveProperty<bool>(false);
    public ReactiveProperty<bool> IsAllStagesFinished { get; } = new ReactiveProperty<bool>(false);
    public ReactiveProperty<bool> IsTimeUp { get; } = new ReactiveProperty<bool>(false);

    public ReactiveProperty<bool> IsStageComplete { get; } = new ReactiveProperty<bool>(false);
    public ReactiveProperty<int> CompletedStageNumber { get; } = new ReactiveProperty<int>(0);
    public ReactiveProperty<int> StageCompleteMoves { get; } = new ReactiveProperty<int>(0);
    public ReactiveProperty<float> StageCompleteTimeLeft { get; } = new ReactiveProperty<float>(0f);

    public int TotalStages => levels != null ? levels.Length : 0;

    public bool HasNextStage =>
        levels != null &&
        currentStageIndex < levels.Length - 1;

    #endregion


    #region Private Variables

    private MemoryLevelData[] levels;

    private readonly List<CardView> selectedCards = new List<CardView>();

    private bool isFlipping;
    private bool isCheckingPair;
    private bool isStageTransitioning;
    private bool isInputLocked;

    private int totalPairs;
    private int currentStageIndex;

    private CancellationTokenSource timerCts;

    #endregion


    #region Unity Lifecycle

    private void Start()
    {
        StartGame();
    }

    private void OnDestroy()
    {
        StopStageTimer();

        Moves.Dispose();
        MatchedPairs.Dispose();
        TotalPairs.Dispose();

        CurrentStage.Dispose();

        TimeLeft.Dispose();
        StageTimeLimit.Dispose();

        IsGameFinished.Dispose();
        IsAllStagesFinished.Dispose();
        IsTimeUp.Dispose();

        IsStageComplete.Dispose();
        CompletedStageNumber.Dispose();
        StageCompleteMoves.Dispose();
        StageCompleteTimeLeft.Dispose();
    }

    #endregion


    #region Game Flow

    public void StartGame()
    {
        if (boardSpawner == null)
        {
            Debug.LogError("اربط Board Spawner داخل MemoryGameManager.");
            return;
        }

        if (!LoadLevelsFromJson())
            return;

        StopStageTimer();

        if (loadSavedProgressOnStart)
        {
            currentStageIndex = GetSavedStageIndex();
        }
        else
        {
            currentStageIndex = 0;
        }

        ResetRoundState();

        LoadCurrentStage();
    }

    private void LoadCurrentStage()
    {
        if (!EnsureLevelsLoaded())
            return;

        currentStageIndex = Mathf.Clamp(
            currentStageIndex,
            0,
            levels.Length - 1
        );

        MemoryLevelData level = levels[currentStageIndex];

        if (level.TotalCards <= 0 || level.TotalCards % 2 != 0)
        {
            Debug.LogError($"Level {currentStageIndex + 1} لازم يكون عدد كروته زوجي.");
            return;
        }

        CurrentStage.Value = currentStageIndex + 1;

        Debug.Log(
            $"Starting Level {CurrentStage.Value}: {level.TotalCards} Cards | Time: {level.timeLimitSeconds}"
        );

        boardSpawner.BuildBoard(level.columns, level.rows);
    }

    public void StartNewGame(int newTotalPairs)
    {
        StopStageTimer();

        if (!EnsureLevelsLoaded())
            return;

        totalPairs = newTotalPairs;

        Moves.Value = 0;
        MatchedPairs.Value = 0;
        TotalPairs.Value = newTotalPairs;

        IsGameFinished.Value = false;
        IsTimeUp.Value = false;
        IsStageComplete.Value = false;

        MemoryLevelData level = levels[currentStageIndex];

        StageTimeLimit.Value = level.timeLimitSeconds;
        TimeLeft.Value = level.timeLimitSeconds;

        selectedCards.Clear();

        isFlipping = false;
        isCheckingPair = false;
        isStageTransitioning = false;
        isInputLocked = false;
    }

    private void CompleteCurrentStage(CancellationToken token)
    {
        IsGameFinished.Value = true;
        isInputLocked = true;

        StopStageTimer();

        CompletedStageNumber.Value = CurrentStage.Value;
        StageCompleteMoves.Value = Moves.Value;
        StageCompleteTimeLeft.Value = TimeLeft.Value;

        if (HasNextStage)
        {
            SaveReachedStage(currentStageIndex + 2);
        }
        else
        {
            SaveGameCompleted();
            IsAllStagesFinished.Value = true;
        }

        ShowStageCompletePanelAsync(token).Forget();
    }

    private async UniTaskVoid ShowStageCompletePanelAsync(CancellationToken token)
    {
        try
        {
            isStageTransitioning = true;

            await UniTask.Delay(
                TimeSpan.FromSeconds(stageCompletePanelDelay),
                cancellationToken: token
            );

            IsStageComplete.Value = true;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            isStageTransitioning = false;
        }
    }

    public void ContinueToNextStage()
    {
        if (!IsStageComplete.Value)
            return;

        if (!EnsureLevelsLoaded())
            return;

        if (!HasNextStage)
        {
            ReturnToMainMenu();
            return;
        }

        StopStageTimer();

        currentStageIndex++;

        selectedCards.Clear();

        isFlipping = false;
        isCheckingPair = false;
        isStageTransitioning = false;
        isInputLocked = false;

        IsStageComplete.Value = false;
        IsGameFinished.Value = false;
        IsTimeUp.Value = false;

        LoadCurrentStage();
    }

    public void ReturnToMainMenu()
    {
        StopStageTimer();

        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void ResetRoundState()
    {
        selectedCards.Clear();

        isFlipping = false;
        isCheckingPair = false;
        isStageTransitioning = false;
        isInputLocked = false;

        IsAllStagesFinished.Value = false;
        IsTimeUp.Value = false;
        IsGameFinished.Value = false;
        IsStageComplete.Value = false;
    }

    #endregion


    #region JSON Levels Loading

    private bool LoadLevelsFromJson()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>(levelsJsonResourcePath);

        if (jsonFile == null)
        {
            Debug.LogError($"ما لقينا ملف JSON داخل Resources باسم: {levelsJsonResourcePath}");
            return false;
        }

        MemoryLevelDatabase database =
            JsonUtility.FromJson<MemoryLevelDatabase>(jsonFile.text);

        if (database == null || database.levels == null || database.levels.Length == 0)
        {
            Debug.LogError("ملف الليفلات فاضي أو فيه مشكلة بالـJSON.");
            return false;
        }

        levels = database.levels;

        Debug.Log($"Loaded {levels.Length} levels from JSON.");

        return true;
    }

    private bool EnsureLevelsLoaded()
    {
        if (levels != null && levels.Length > 0)
            return true;

        return LoadLevelsFromJson();
    }

    #endregion


    #region Card Input And Flip

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
                HandleCorrectMatch(firstCard, secondCard, token);
                return;
            }

            await HandleWrongMatchAsync(firstCard, secondCard, token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            isCheckingPair = false;
        }
    }

    private void HandleCorrectMatch(
        CardView firstCard,
        CardView secondCard,
        CancellationToken token)
    {
        PlaySfx(correctMatchSfx);

        firstCard.MarkMatched();
        secondCard.MarkMatched();

        MatchedPairs.Value++;

        AddBonusTimeForMatch();

        selectedCards.Clear();

        if (MatchedPairs.Value >= totalPairs)
        {
            CompleteCurrentStage(token);
        }
    }

    private async UniTask HandleWrongMatchAsync(
        CardView firstCard,
        CardView secondCard,
        CancellationToken token)
    {
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

    public void SetInputLocked(bool locked)
    {
        isInputLocked = locked;
    }

    #endregion


    #region Timer System

    public void StartStageTimer()
    {
        StopStageTimer();

        if (!EnsureLevelsLoaded())
            return;

        if (IsGameFinished.Value)
            return;

        if (IsTimeUp.Value)
            return;

        MemoryLevelData level = levels[currentStageIndex];

        if (level.timeLimitSeconds <= 0f)
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
        if (!EnsureLevelsLoaded())
            return;

        if (IsGameFinished.Value || IsTimeUp.Value)
            return;

        MemoryLevelData level = levels[currentStageIndex];

        if (level.bonusTimePerMatch <= 0f)
            return;

        TimeLeft.Value += level.bonusTimePerMatch;

        Debug.Log($"+{level.bonusTimePerMatch} seconds");
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

    #endregion


    #region Save Progress

    private int GetSavedStageIndex()
    {
        if (!EnsureLevelsLoaded())
            return 0;

        int savedStageNumber = PlayerPrefs.GetInt(SavedStageKey, 1);

        savedStageNumber = Mathf.Clamp(
            savedStageNumber,
            1,
            levels.Length
        );

        return savedStageNumber - 1;
    }

    private void SaveReachedStage(int stageNumber)
    {
        if (!EnsureLevelsLoaded())
            return;

        int clampedStageNumber = Mathf.Clamp(
            stageNumber,
            1,
            levels.Length
        );

        int savedStageNumber = PlayerPrefs.GetInt(SavedStageKey, 1);

        if (clampedStageNumber <= savedStageNumber)
            return;

        PlayerPrefs.SetInt(SavedStageKey, clampedStageNumber);
        PlayerPrefs.SetInt(GameCompletedKey, 0);
        PlayerPrefs.Save();

        Debug.Log($"Saved Progress: Level {clampedStageNumber}");
    }

    private void SaveGameCompleted()
    {
        if (!EnsureLevelsLoaded())
            return;

        PlayerPrefs.SetInt(SavedStageKey, levels.Length);
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

        ResetRoundState();

        LoadCurrentStage();
    }

    #endregion


    #region Debug Stage Navigation

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
        if (!EnsureLevelsLoaded())
            return;

        StopStageTimer();

        int stageIndex = stageNumber - 1;

        stageIndex = Mathf.Clamp(
            stageIndex,
            0,
            levels.Length - 1
        );

        currentStageIndex = stageIndex;

        ResetRoundState();

        LoadCurrentStage();
    }

    #endregion


    #region Sound

    private void PlaySfx(AudioClip clip)
    {
        if (sfxSource == null || clip == null)
            return;

        sfxSource.PlayOneShot(clip);
    }

    #endregion
}