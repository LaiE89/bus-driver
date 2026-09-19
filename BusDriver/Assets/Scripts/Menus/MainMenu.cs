using System.Collections;
using UnityEngine.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour {
    [SerializeField] public OptionsMenu options;
    [SerializeField] public GameObject loadingScreen; 
    [SerializeField] public Slider slider;
    [SerializeField] public TextMeshProUGUI progressText;
    [SerializeField] int sceneIndex = 1;

    public static SoundController soundController;

    private void Awake() {
        soundController = GameObject.Find("Sound Controller").GetComponent<SoundController>();
    }

    private void Start() {
        // soundController.Play("Menu Song");
        options.InitializeSettings();
    }

    public void PlayGame() {
        soundController.Play("UI Click");
        StartCoroutine(LoadAsyncronously(sceneIndex));
    }

    IEnumerator LoadAsyncronously (int sceneIndex) {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneIndex);
        loadingScreen.SetActive(true);
        slider.value = 0;
        float time = 0;
        operation.allowSceneActivation = false;
        while (slider.value < 1f || time < 1f) {
            float progress = Mathf.Clamp01(operation.progress / .9f);
            slider.value = Mathf.Lerp(slider.value, progress, time);
            time += Time.unscaledDeltaTime;
            progressText.SetText($"{(slider.value * 100).ToString("N2")}%");
            yield return null;
        }
        operation.allowSceneActivation = true;
    }

    public void QuitGame() {
        print("Quit!");
        Application.Quit();
    }

    public void PlayUISound() {
        soundController.Play("UI Click");
    }
}
