using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{

    [SerializeField] private GameObject caseDescriptionBox;
    [SerializeField] private Button caseDescriptionToggleButton;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        caseDescriptionToggleButton.onClick.AddListener(OnCaseDescriptionToggleButtonClick);   
    }

    void OnCaseDescriptionToggleButtonClick()
    {
        caseDescriptionBox.SetActive(!caseDescriptionBox.activeInHierarchy);
    }
}
