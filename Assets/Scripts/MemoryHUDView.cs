using System;
using System.Collections.Generic;
using R3;
using TMPro;
using UnityEngine;

public class MemoryHUDView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MemoryGameManager gameManager;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI stageText;
    [SerializeField] private TextMeshProUGUI movesText;
    [SerializeField] private TextMeshProUGUI pairsText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI timeUpText;

    private readonly List<IDisposable> subscriptions = new List<IDisposable>();

    private void Start()
    {
        if (gameManager == null)
        {
            Debug.LogError("اربط MemoryGameManager داخل MemoryHUDView.");
            return;
        }

        subscriptions.Add(
            gameManager.CurrentStage.Subscribe(
                new HudObserver<int>(_ => RefreshStageText())
            )
        );

        subscriptions.Add(
            gameManager.Moves.Subscribe(
                new HudObserver<int>(_ => RefreshMovesText())
            )
        );

        subscriptions.Add(
            gameManager.MatchedPairs.Subscribe(
                new HudObserver<int>(_ => RefreshPairsText())
            )
        );

        subscriptions.Add(
            gameManager.TotalPairs.Subscribe(
                new HudObserver<int>(_ => RefreshPairsText())
            )
        );

        subscriptions.Add(
            gameManager.TimeLeft.Subscribe(
                new HudObserver<float>(_ => RefreshTimerText())
            )
        );

        subscriptions.Add(
            gameManager.IsTimeUp.Subscribe(
                new HudObserver<bool>(_ => RefreshTimeUpText())
            )
        );

        RefreshAll();
    }

    private void RefreshAll()
    {
        RefreshStageText();
        RefreshMovesText();
        RefreshPairsText();
        RefreshTimerText();
        RefreshTimeUpText();
    }

    private void RefreshStageText()
    {
        if (stageText == null)
            return;

        stageText.text =
            $"Stage: {gameManager.CurrentStage.Value} / {gameManager.TotalStages}";
    }

    private void RefreshMovesText()
    {
        if (movesText == null)
            return;

        movesText.text =
            $"Moves: {gameManager.Moves.Value}";
    }

    private void RefreshPairsText()
    {
        if (pairsText == null)
            return;

        pairsText.text =
            $"Pairs: {gameManager.MatchedPairs.Value} / {gameManager.TotalPairs.Value}";
    }

    private void RefreshTimerText()
    {
        if (timerText == null)
            return;

        int totalSeconds = Mathf.CeilToInt(gameManager.TimeLeft.Value);

        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timerText.text =
            $"Time: {minutes:00}:{seconds:00}";
    }

    private void RefreshTimeUpText()
    {
        if (timeUpText == null)
            return;

        bool isTimeUp = gameManager.IsTimeUp.Value;

        timeUpText.gameObject.SetActive(isTimeUp);

        if (isTimeUp)
        {
            timeUpText.text = "TIME UP!";
        }
    }

    private void OnDestroy()
    {
        foreach (IDisposable subscription in subscriptions)
        {
            subscription?.Dispose();
        }

        subscriptions.Clear();
    }

    private sealed class HudObserver<T> : Observer<T>
    {
        private readonly Action<T> onNext;

        public HudObserver(Action<T> onNext)
        {
            this.onNext = onNext;
        }

        protected override void OnNextCore(T value)
        {
            onNext?.Invoke(value);
        }

        protected override void OnErrorResumeCore(Exception error)
        {
            Debug.LogException(error);
        }

        protected override void OnCompletedCore(R3.Result result)
        {
        }
    }
}