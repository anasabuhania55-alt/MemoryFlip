using System;
using System.Collections.Generic;
using DG.Tweening;
using R3;
using UnityEngine;
using UnityEngine.UI;

public class PausePanelView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MemoryGameManager gameManager;

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform panelTransform;

    [Header("Buttons")]
    [SerializeField] private Button openPauseButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Animation")]
    [SerializeField] private float showDuration = 0.25f;
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
            Debug.LogError("اربط MemoryGameManager داخل PausePanelView.");
            return;
        }

        if (openPauseButton != null)
        {
            openPauseButton.onClick.AddListener(gameManager.PauseGameFromButton);
        }

        if (resumeButton != null)
        {
            resumeButton.onClick.AddListener(gameManager.ResumeGameFromButton);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(gameManager.ReturnToMainMenuFromButton);
        }

        subscriptions.Add(
            gameManager.IsPaused.Subscribe(
                new PauseObserver<bool>(OnPauseChanged)
            )
        );
    }

    private void OnPauseChanged(bool isPaused)
    {
        if (isPaused)
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
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        if (panelTransform == null)
            return;

        scaleTween?.Kill();

        panelTransform.localScale = Vector3.one * startScale;

        scaleTween = panelTransform
            .DOScale(Vector3.one, showDuration)
            .SetEase(Ease.OutBack)
            .SetUpdate(true);
    }

    private void HidePanel()
    {
        scaleTween?.Kill();

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        scaleTween?.Kill();

        if (gameManager != null)
        {
            if (openPauseButton != null)
            {
                openPauseButton.onClick.RemoveListener(gameManager.PauseGameFromButton);
            }

            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(gameManager.ResumeGameFromButton);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.RemoveListener(gameManager.ReturnToMainMenuFromButton);
            }
        }

        foreach (IDisposable subscription in subscriptions)
        {
            subscription?.Dispose();
        }

        subscriptions.Clear();
    }

    private sealed class PauseObserver<T> : Observer<T>
    {
        private readonly Action<T> onNext;

        public PauseObserver(Action<T> onNext)
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