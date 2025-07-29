using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class CourtPreviewAnimation : MonoBehaviour
{
    [SerializeField] private RectTransform[] pages;
    [SerializeField] private TextMeshProUGUI[] casePreviewTextboxes;
    [SerializeField] private RectTransform[] wayPoints;
    [SerializeField]private AnimationCurve newCaseCurve;
    [SerializeField]private AnimationCurve switchCaseCurve;
    [SerializeField] private float animationDuration;
    [SerializeField] private Button playButton;
    [SerializeField] private Button[] arrowButtons;
    
    private Vector2[] _startPositions;
    private readonly Vector2 _switchOffset = new (200, 0);
    
    private void Awake()
    {
        _startPositions = new Vector2[pages.Length];
        _startPositions[0] = pages[0].anchoredPosition;
        _startPositions[1] = pages[1].anchoredPosition;
        _startPositions[2] = pages[2].anchoredPosition;
        
    }

    public async Task PlayAnimation(string newContent)
    {
        float deltaTimeNorm = Time.deltaTime / animationDuration;
        float currentTime = 0f;
        casePreviewTextboxes[2].text = newContent;
        
        while ((currentTime += deltaTimeNorm) < 1f)
        {
            pages[2].anchoredPosition = Vector2.Lerp(_startPositions[2], _startPositions[0], newCaseCurve.Evaluate(currentTime));
            await Task.Yield();
        }
        
        if (!pages[1].gameObject.activeInHierarchy)
        {
            pages[1].gameObject.SetActive(true);
            playButton.gameObject.SetActive(true);
        } else if (!pages[0].gameObject.activeInHierarchy)
        {
            pages[0].gameObject.SetActive(true);
            arrowButtons[0].gameObject.SetActive(true);
            arrowButtons[1].gameObject.SetActive(true);
        }
        
        pages[2].anchoredPosition = _startPositions[2];
        casePreviewTextboxes[1].text = newContent;
        

    }

    public async Task PlaySwitchAnimation(int arrowButtonIndex, string newContent)
    {
        float deltaTimeNorm = Time.deltaTime / animationDuration;
        float currentTime = 0f;

        switch (arrowButtonIndex)
        {
            case 0:
                casePreviewTextboxes[0].text = newContent;
                Transform page0Transform = pages[0].transform;
                while ((currentTime += deltaTimeNorm) < 1f)
                {
                    pages[0].anchoredPosition = Vector2.Lerp(_startPositions[0], _startPositions[0] - _switchOffset, switchCaseCurve.Evaluate(currentTime));
                    pages[1].anchoredPosition = Vector2.Lerp(_startPositions[1], _startPositions[1] + _switchOffset/4, switchCaseCurve.Evaluate(currentTime));
                    
                    if(currentTime >= .5f)
                        page0Transform.SetSiblingIndex(1);
                        
                    await Task.Yield();
                }
                page0Transform.SetSiblingIndex(0);
                casePreviewTextboxes[1].text = newContent;
                
                break;
            
            case 1:
                casePreviewTextboxes[0].text = newContent;
                Transform page1Transform = pages[1].transform;
                while ((currentTime += deltaTimeNorm) < 1f)
                {
                    pages[1].anchoredPosition = Vector2.Lerp(_startPositions[1], _startPositions[1] + _switchOffset, switchCaseCurve.Evaluate(currentTime));
                    pages[0].anchoredPosition = Vector2.Lerp(_startPositions[0], _startPositions[0] - _switchOffset/4, switchCaseCurve.Evaluate(currentTime));
                    
                    
                    if(currentTime >= .5f)
                        page1Transform.SetSiblingIndex(0);
                        
                    await Task.Yield();
                }
                page1Transform.SetSiblingIndex(1);
                casePreviewTextboxes[1].text = newContent;
                
                break;
            
            
        }
    }
    
}
