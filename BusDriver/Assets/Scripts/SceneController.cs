using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class SceneController : MonoBehaviour {
    // Declare any public variables that you want to be able 
    // to access throughout your scene
    [SerializeField] public GameObject loadingScreen; 
    [SerializeField] public OptionsMenu optionsMenu;
    [SerializeField] public Slider loadingSlider;
    [SerializeField] public TextMeshProUGUI progressText;
    [SerializeField] public string ambienceSound;

    [Header("Singletons")]
    public GameObject canvas;
    public SoundController soundController;
    public DialogueController dialogueController;
    public DialogueController objectivesController;

    public static int sceneIndex;
    public static SceneController Instance { get; private set; } // static singleton

    void Awake() {
        // Singleton Stuff
        if (Instance == null) { 
            Instance = this;
        }else { 
            Destroy(gameObject);
        }

        // Cache references to all desired variables
        canvas = GameObject.Find("Canvas");
        soundController = FindObjectOfType<SoundController>();
        dialogueController = GameObject.Find("Dialogue Controller").GetComponent<DialogueController>();
        objectivesController = GameObject.Find("Objectives Controller").GetComponent<DialogueController>();

        // Point and click, so the cursor stays free
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        sceneIndex = SceneManager.GetActiveScene().buildIndex;
        foreach (Transform child in canvas.transform) {
            switch (child.name) {
                case "Loading Screen":
                    loadingScreen = child.gameObject;
                    loadingSlider = loadingScreen.transform.GetComponentInChildren<Slider>();
                    progressText = loadingScreen.transform.GetComponentInChildren<TextMeshProUGUI>();
                    break;
                case "Options Menu":
                    optionsMenu = child.GetComponent<OptionsMenu>();
                    break;
                default:
                    break;
            }
        }
        optionsMenu.InitializeSettings();
    }

    void Start() {
        soundController.Play(ambienceSound);
    }

    IEnumerator LoadAsyncronously (int sceneIndex) {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneIndex);
        loadingScreen.SetActive(true);
        loadingSlider.value = 0;
        float time = 0;
        operation.allowSceneActivation = false;
        while (loadingSlider.value < 1f || time < 1f) {
            float progress = Mathf.Clamp01(operation.progress / .9f);
            loadingSlider.value = Mathf.Lerp(loadingSlider.value, progress, time);
            time += Time.unscaledDeltaTime;
            progressText.SetText($"{(loadingSlider.value * 100).ToString("N2")}%");
            yield return null;
        }
        operation.allowSceneActivation = true;
    }

    public void NextLevel() {
        sceneIndex += 1;
        if (sceneIndex >= SceneManager.sceneCountInBuildSettings) {
            SceneManager.LoadScene(0);
        }else {
            StartCoroutine(LoadAsyncronously(sceneIndex));
        }
    }
}
