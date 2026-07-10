using System;
using System.Collections.Generic;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MemoryHUDView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MemoryGameManager gameManager;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI stageText;
    [SerializeField] private TextMeshProUGUI movesText;
    [SerializeField] private TextMeshProUGUI pairsText;

    [Header("Circle Timer")]
    [SerializeField] private Image timerFillImage;
    [SerializeField] private RectTransform timerRoot;

    [Header("DOTween Timer Animation")]
    [SerializeField] private float fillTweenDuration = 0.15f;
    [SerializeField] private Ease fillEase = Ease.OutQuad;

    [Header("Bonus Time Animation")]
    [SerializeField] private float bonusPunchPower = 0.18f;
    [SerializeField] private float bonusPunchDuration = 0.35f;
    [SerializeField] private int bonusPunchVibrato = 8;

    [Header("Low Time Warning")]
    [SerializeField] private bool useLowTimePulse = true;
    [Range(0f, 1f)]
    [SerializeField] private float lowTimePercent = 0.25f;
    [SerializeField] private float warningScale = 1.08f;
    [SerializeField] private float warningPulseDuration = 0.45f;

    private readonly List<IDisposable> subscriptions = new List<IDisposable>();

    private Vector3 timerOriginalScale;
    private float previousTimeLeft = -1f;

    private Tween fillTween;
    private Tween bonusTween;
    private Tween warningTween;

    private bool isWarningPulsePlaying;

    private void Start()
    {
        if (gameManager == null)
        {
            Debug.LogError("اربط MemoryGameManager داخل MemoryHUDView.");
            return;
        }

        if (timerRoot != null)
        {
            timerOriginalScale = timerRoot.localScale;
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
                new HudObserver<float>(_ => RefreshTimerCircle())
            )
        );

        subscriptions.Add(
            gameManager.StageTimeLimit.Subscribe(
                new HudObserver<float>(_ => RefreshTimerCircle())
            )
        );

        RefreshAll();
    }

    private void RefreshAll()
    {
        RefreshStageText();
        RefreshMovesText();
        RefreshPairsText();
        RefreshTimerCircle();
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

    private void RefreshTimerCircle()
    {
        if (timerFillImage == null)
            return;

        float maxTime = gameManager.StageTimeLimit.Value;

        if (maxTime <= 0f)
        {
            timerFillImage.fillAmount = 0f;
            previousTimeLeft = gameManager.TimeLeft.Value;
            StopWarningPulse();
            return;
        }

        float currentTime = Mathf.Clamp(
            gameManager.TimeLeft.Value,
            0f,
            maxTime
        );

        float targetFill = Mathf.Clamp01(currentTime / maxTime);

        bool gotBonusTime =
            previousTimeLeft >= 0f &&
            currentTime > previousTimeLeft + 0.05f;

        AnimateTimerFill(targetFill, gotBonusTime);

        if (gotBonusTime)
        {
            PlayBonusTimeAnimation();
        }

        UpdateLowTimeWarning(targetFill);

        previousTimeLeft = currentTime;
    }

    private void AnimateTimerFill(float targetFill, bool gotBonusTime)
    {
        if (timerFillImage == null)
            return;

        float difference = Mathf.Abs(timerFillImage.fillAmount - targetFill);

        if (!gotBonusTime && difference < 0.01f)
            return;

        fillTween?.Kill();

        fillTween = timerFillImage
            .DOFillAmount(targetFill, fillTweenDuration)
            .SetEase(fillEase);
    }

    private void PlayBonusTimeAnimation()
    {
        if (timerRoot == null)
            return;

        bonusTween?.Kill();

        timerRoot.localScale = timerOriginalScale;

        bonusTween = timerRoot
            .DOPunchScale(
                Vector3.one * bonusPunchPower,
                bonusPunchDuration,
                bonusPunchVibrato,
                0.8f
            )
            .SetEase(Ease.OutBack);
    }

    private void UpdateLowTimeWarning(float fillAmount)
    {
        if (!useLowTimePulse || timerRoot == null)
            return;

        bool shouldPulse =
            fillAmount > 0f &&
            fillAmount <= lowTimePercent &&
            !gameManager.IsGameFinished.Value &&
            !gameManager.IsTimeUp.Value;

        if (shouldPulse)
        {
            StartWarningPulse();
        }
        else
        {
            StopWarningPulse();
        }
    }

    private void StartWarningPulse()
    {
        if (isWarningPulsePlaying)
            return;

        isWarningPulsePlaying = true;

        warningTween?.Kill();

        warningTween = timerRoot
            .DOScale(timerOriginalScale * warningScale, warningPulseDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }

    private void StopWarningPulse()
    {
        if (!isWarningPulsePlaying)
            return;

        isWarningPulsePlaying = false;

        warningTween?.Kill();
        warningTween = null;

        if (timerRoot != null)
        {
            timerRoot.DOScale(timerOriginalScale, 0.15f)
                .SetEase(Ease.OutQuad);
        }
    }

    private void OnDestroy()
    {
        fillTween?.Kill();
        bonusTween?.Kill();
        warningTween?.Kill();

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