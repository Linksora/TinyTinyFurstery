#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Play Mode shortcut only: no component or UI is compiled into players.</summary>
public sealed class EditorSceneDropdown : MonoBehaviour
{
    private Dropdown dropdown;
    private string[] scenePaths;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindFirstObjectByType<EditorSceneDropdown>() != null) return;
        var root = new GameObject("Editor Scene Jumper", typeof(RectTransform));
        DontDestroyOnLoad(root);
        root.AddComponent<EditorSceneDropdown>();
    }

    private void Awake()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        GameObject control = DefaultControls.CreateDropdown(new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
        });
        control.name = "Scene Dropdown (Editor Only)";
        control.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)control.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(12, -12);
        rect.sizeDelta = new Vector2(260, 36);
        dropdown = control.GetComponent<Dropdown>();
        dropdown.navigation = new Navigation { mode = Navigation.Mode.None };
        dropdown.template.sizeDelta = new Vector2(0, 240);
        // DefaultControls leaves only 17px for item text, which clips this 18pt font entirely.
        RectTransform item = (RectTransform)dropdown.itemText.transform.parent;
        item.sizeDelta = new Vector2(0, 36);
        ((RectTransform)item.parent).sizeDelta = new Vector2(0, 44);
        foreach (Text text in control.GetComponentsInChildren<Text>(true))
        {
            text.font = GameFont.Primary != null ? GameFont.Primary : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.color = new Color(0.12f, 0.12f, 0.12f);
        }
        dropdown.onValueChanged.AddListener(JumpToScene);
        RefreshScenes();
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshScenes();

    private void RefreshScenes()
    {
        // Include disabled entries for testing without changing which scenes ship.
        scenePaths = EditorBuildSettings.scenes.Select(scene => scene.path)
            .Where(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null).Distinct().ToArray();
        dropdown.ClearOptions();
        dropdown.options.Add(new Dropdown.OptionData($"Scene: {SceneManager.GetActiveScene().name}"));
        foreach (string path in scenePaths)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            bool duplicate = scenePaths.Count(other => Path.GetFileNameWithoutExtension(other) == name) > 1;
            dropdown.options.Add(new Dropdown.OptionData(duplicate ? path : name));
        }
        dropdown.SetValueWithoutNotify(0);
        dropdown.RefreshShownValue();

        // A scene without its own EventSystem still needs to accept dropdown clicks.
        // Keep the fallback in that scene, so it goes away when loading the next one.
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("Editor Scene Jumper EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private void JumpToScene(int index)
    {
        if (index <= 0 || index > scenePaths.Length) return;
        string path = scenePaths[index - 1];
        dropdown.SetValueWithoutNotify(0);
        StartCoroutine(LoadScene(path));
    }

    private IEnumerator LoadScene(string path)
    {
        // Let the dropdown finish handling its click before destroying the old scene's UI.
        yield return null;
        float previousTimeScale = Time.timeScale;
        try
        {
            Time.timeScale = 1f;
            EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
        }
        catch (System.Exception exception)
        {
            Time.timeScale = previousTimeScale;
            Debug.LogException(exception);
        }
    }
}
#endif
