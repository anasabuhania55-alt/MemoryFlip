using System;
using System.Collections.Generic;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StageCompletePanelView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MemoryGameManager gameManager;

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform panelTransform;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI stageText;
    [SerializeField] private TextMeshProUGUI movesText;
    [SerializeField] private TextMeshProUGUI pairsText;
    [SerializeField] private TextMeshProUGUI timeLeftText;

    [Header("Buttons")]
    [SerializeField] private Button nextStageButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private TextMeshProUGUI nextStageButtonText;

    [Header("Animation")]
    [SerializeField] private float showDuration = 0.3f;
    [SerializeField] private float startScale = 0.85f;

    private readonly List<IDisposable> subscriptions = new List<IDisposable>();

    private Tween scaleTween;

    private void Awake()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void Start()
    {
        if (gameManager == null)
        {
            Debug.LogError("اربط MemoryGameManager داخل StageCompletePanelView.");
            return;
        }

        if (nextStageButton != null)
        {
            nextStageButton.onClick.AddListener(gameManager.ContinueToNextStage);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(gameManager.ReturnToMainMenu);
        }

        subscriptions.Add(
            gameManager.IsStageComplete.Subscribe(
                new PanelObserver<bool>(OnStageCompleteChanged)
            )
        );
    }

    private void OnStageCompleteChanged(bool isComplete)
    {
        if (isComplete)
        {
            ShowPanel();
        }
        else
        {
            HidePanel();
        }
    }

    private void ShowPanel()
    {
        RefreshTexts();

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        if (panelTransform != null)
        {
            panelTransform.localScale = Vector3.one * startScale;

            scaleTween?.Kill();

            scaleTween = panelTransform
                .DOScale(Vector3.one, showDuration)
                .SetEase(Ease.OutBack);
        }
    }

    private void HidePanel()
    {
        scaleTween?.Kill();

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void RefreshTexts()
    {
        bool isLastStage = !gameManager.HasNextStage;

        if (titleText != null)
        {
            titleText.text = isLastStage
                ? "GAME COMPLETE!"
                : "STAGE COMPLETE!";
        }

        if (stageText != null)
        {
            stageText.text =
                $"Stage: {gameManager.CompletedStageNumber.Value}";
        }

        if (movesText != null)
        {
            movesText.text =
                $"Moves: {gameManager.StageCompleteMoves.Value}";
        }

        if (pairsText != null)
        {
            pairsText.text =
                $"Pairs: {gameManager.MatchedPairs.Value} / {gameManager.TotalPairs.Value}";
        }

        if (timeLeftText != null)
        {
            int totalSeconds = Mathf.CeilToInt(gameManager.StageCompleteTimeLeft.Value);

            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            timeLeftText.text =
                $"Time Left: {minutes:00}:{seconds:00}";
        }

        if (nextStageButton != null)
        {
            nextStageButton.gameObject.SetActive(!isLastStage);
        }

        if (nextStageButtonText != null)
        {
            nextStageButtonText.text = "NEXT STAGE";
        }
    }

    private void OnDestroy()
    {
        scaleTween?.Kill();

        if (gameManager != null)
        {
            if (nextStageButton != null)
            {
                nextStageButton.onClick.RemoveListener(gameManager.ContinueToNextStage);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.RemoveListener(gameManager.ReturnToMainMenu);
            }
        }

        foreach (IDisposable subscription in subscriptions)
        {
            subscription?.Dispose();
        }

        subscriptions.Clear();
    }

    private sealed class PanelObserver<T> : Observer<T>
    {
        private readonly Action<T> onNext;

        public PanelObserver(Action<T> onNext)
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