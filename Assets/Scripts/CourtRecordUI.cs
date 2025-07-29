using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CourtRecordUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject courtRecordPanel;
    [SerializeField] private Button openCourtRecordButton;
    [SerializeField] private Button exitButton;
    private Vector3 defaultScale;
    public bool isGameplay = true;


    private void Start()
    {
        defaultScale = openCourtRecordButton.transform.localScale;
        courtRecordPanel.SetActive(false);
        openCourtRecordButton.onClick.AddListener(OpenCourtRecord);
        exitButton.onClick.AddListener(CloseCourtRecord);
    }

    void Update()
    {
        if (!isGameplay) return;

        if (EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() != null)
        {
            return;
        }


        if (!courtRecordPanel.activeInHierarchy && Input.GetKeyDown(KeyCode.R))
        {
            OpenCourtRecord();
        }

        if (courtRecordPanel.activeInHierarchy && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseCourtRecord();
        }
    }

    void OpenCourtRecord()
    {
        courtRecordPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    void CloseCourtRecord()
    {
        courtRecordPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        openCourtRecordButton.transform.localScale = defaultScale * 1.2f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        openCourtRecordButton.transform.localScale = defaultScale;
    }
}
