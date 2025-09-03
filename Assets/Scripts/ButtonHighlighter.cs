using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;

public class ButtonHighlighter : MonoBehaviour
{
    [Header("Highlight Prefabs")]
    public GameObject highlightMenuButtonPrefab;
    public GameObject highlightBackPrefab;
    public GameObject highlightRomboPrefab;
    public GameObject highlightMirrorPrefab;
    public GameObject highlightPaperPrefab;

    [Header("Audio Settings")]
    [SerializeField] private bool enableAudioFeedback = true;

    private Dictionary<string, GameObject> tagToHighlight;

    //Lista di tag che devono avere solo audio, senza highlight visivo
    private HashSet<string> audioOnlyTags = new HashSet<string> { "LanguageButton" };

    private void Awake()
    {
        tagToHighlight = new Dictionary<string, GameObject>
        {
            { "MenuButton", highlightMenuButtonPrefab },
            { "BackButton", highlightBackPrefab },
            { "RomboButton", highlightRomboPrefab },
            { "MirrorButton", highlightMirrorPrefab },
            { "PaperButton", highlightPaperPrefab }
        };
    }

    private async void Start()
    {
        await Task.Delay(100);
        Button[] allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Button btn in allButtons)
        {
            string tag = btn.tag;

            if (btn.GetComponent<HighlightHandler>() != null)
                continue;

            bool audioOnly = audioOnlyTags.Contains(tag);

            if (audioOnly || tagToHighlight.ContainsKey(tag))
            {
                GameObject highlight = null;

                if (!audioOnly && tagToHighlight.TryGetValue(tag, out GameObject prefab) && prefab != null)
                {
                    highlight = Instantiate(prefab, btn.transform);
                    highlight.name = "HighlightImage";
                    highlight.SetActive(false);

                    RectTransform rect = highlight.GetComponent<RectTransform>();
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }

                HighlightHandler handler = btn.gameObject.AddComponent<HighlightHandler>();
                handler.highlightImage = highlight;
                handler.buttonHighlighter = this;
                handler.audioOnly = audioOnly;
            }
        }
    }

    public void PlayHoverSound(string buttonTag)
    {
        if (AudioManager.instance == null || !enableAudioFeedback) return;

        if (buttonTag == "LanguageButton")
        {
            AudioManager.instance.PlaySFXOneShot("button_hover");
        }
        else if (buttonTag == "PaperButton")
        {
            AudioManager.instance.PlaySFXOneShot("paper_hover");
        }
        else
        {
            AudioManager.instance.PlaySFXOneShot("button_hover");
        }
    }

    public void PlayClickSound(string buttonTag)
    {
        if (AudioManager.instance == null || !enableAudioFeedback) return;

        if (buttonTag == "LanguageButton")
        {
            AudioManager.instance.PlaySFXOneShot("button_click");
        }
        else if (buttonTag == "PaperButton")
        {
            AudioManager.instance.PlaySFXOneShot("paper_click");
        }
        else
        {
            AudioManager.instance.PlaySFXOneShot("button_click");
        }
    }

    private class HighlightHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public GameObject highlightImage;
        public ButtonHighlighter buttonHighlighter;
        public bool audioOnly = false;

        private bool isSelected = false;
        private Coroutine blinkCoroutine;

        private static HighlightHandler currentSelectedMirror = null;
        private const string PREFS_KEY = "SelectedMirrorButtonName";

        private void OnEnable()
        {
            if (gameObject.CompareTag("MirrorButton"))
            {
                string savedName = PlayerPrefs.GetString(PREFS_KEY, "");

                if (savedName == gameObject.name)
                {
                    currentSelectedMirror = this;
                    isSelected = true;
                    SetHighlightStatic(true);
                }
                else
                {
                    isSelected = false;
                    SetHighlightStatic(false);
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (buttonHighlighter != null)
            {
                buttonHighlighter.PlayHoverSound(gameObject.tag);
            }

            if (!audioOnly && highlightImage != null && !isSelected)
                highlightImage.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!audioOnly && highlightImage != null && !isSelected)
                highlightImage.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (buttonHighlighter != null)
            {
                buttonHighlighter.PlayClickSound(gameObject.tag);
            }

            if (!gameObject.CompareTag("MirrorButton"))
                return;

            if (currentSelectedMirror != this)
            {
                if (currentSelectedMirror != null)
                    currentSelectedMirror.Deselect();

                Select();
                currentSelectedMirror = this;

                PlayerPrefs.SetString(PREFS_KEY, gameObject.name);
                PlayerPrefs.Save();
            }
        }

        private void Select()
        {
            isSelected = true;

            if (blinkCoroutine == null && highlightImage != null)
                blinkCoroutine = StartCoroutine(BlinkHighlight());
        }

        private void Deselect()
        {
            isSelected = false;

            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
                blinkCoroutine = null;
            }

            SetHighlightStatic(false);

            if (currentSelectedMirror == this)
            {
                PlayerPrefs.DeleteKey(PREFS_KEY);
                PlayerPrefs.Save();
                currentSelectedMirror = null;
            }
        }

        private IEnumerator BlinkHighlight()
        {
            while (highlightImage != null)
            {
                highlightImage.SetActive(true);
                yield return new WaitForSeconds(0.5f);
                highlightImage.SetActive(false);
                yield return new WaitForSeconds(0.5f);
            }
        }

        private void SetHighlightStatic(bool on)
        {
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
                blinkCoroutine = null;
            }

            if (highlightImage != null)
                highlightImage.SetActive(on);
        }
    }
}