using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Scene Settings")]
    [SerializeField] private string gameplaySceneName = "Game";

    [Header("Button Sound")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip buttonClickSfx;

    [Header("Timing")]
    [SerializeField] private float buttonClickDelay = 0.12f;

    private bool isLoading;

    public void PlayGame()
    {
        if (isLoading)
            return;

        LoadGameAsync(
            this.GetCancellationTokenOnDestroy()
        ).Forget();
    }

    public void QuitGame()
    {
        if (isLoading)
            return;

        QuitGameAsync(
            this.GetCancellationTokenOnDestroy()
        ).Forget();
    }

    private async UniTaskVoid LoadGameAsync(CancellationToken token)
    {
        isLoading = true;

        PlayButtonSound();

        try
        {
            // بننتظر شوي حتى اللاعب يسمع صوت الزر قبل تغيير الـScene.
            await UniTask.Delay(
                TimeSpan.FromSeconds(buttonClickDelay),
                cancellationToken: token
            );

            SceneManager.LoadScene(gameplaySceneName);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async UniTaskVoid QuitGameAsync(CancellationToken token)
    {
        isLoading = true;

        PlayButtonSound();

        try
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(buttonClickDelay),
                cancellationToken: token
            );

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void PlayButtonSound()
    {
        if (sfxSource == null || buttonClickSfx == null)
            return;

        sfxSource.PlayOneShot(buttonClickSfx);
    }
}