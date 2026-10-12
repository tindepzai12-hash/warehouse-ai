using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using JarVis.Warehouse.UI;

namespace JarVis.Warehouse
{
    /// <summary>Android-first JarVis splash controller for the initial scene.</summary>
    public sealed class JarvisSplashController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup splashGroup;
        [SerializeField] private Text jarvisLabel;
        [SerializeField] private string nextScene = "LoginScene";
        [SerializeField, Min(0.5f)] private float totalSeconds = 1.8f;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.35f;

        private bool isLoading;

        private IEnumerator Start()
        {
            MobileTextStyle.Apply(transform.Find("Safe Area"));
            if (jarvisLabel != null)
                jarvisLabel.text = "JarVis";

            if (splashGroup == null)
            {
                Debug.LogError("[JarvisSplashController] Splash CanvasGroup is not assigned.", this);
                LoadNextScene();
                yield break;
            }

            splashGroup.alpha = 0f;
            splashGroup.interactable = false;
            splashGroup.blocksRaycasts = false;

            float fadeDuration = Mathf.Min(fadeSeconds, totalSeconds * 0.5f);
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                splashGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }

            splashGroup.alpha = 1f;
            float holdSeconds = Mathf.Max(0f, totalSeconds - (fadeDuration * 2f));
            if (holdSeconds > 0f)
                yield return new WaitForSecondsRealtime(holdSeconds);

            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                splashGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }

            splashGroup.alpha = 0f;
            splashGroup.interactable = false;
            splashGroup.blocksRaycasts = false;
            LoadNextScene();
        }

        private void LoadNextScene()
        {
            if (isLoading)
                return;

            isLoading = true;
            if (!string.IsNullOrWhiteSpace(nextScene)
                && nextScene != SceneManager.GetActiveScene().name
                && Application.CanStreamedLevelBeLoaded(nextScene))
            {
                SceneManager.LoadScene(nextScene);
                return;
            }

            if (splashGroup != null)
                splashGroup.gameObject.SetActive(false);

            isLoading = false;
        }
    }
}
