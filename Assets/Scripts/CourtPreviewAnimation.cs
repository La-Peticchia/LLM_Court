using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CourtPreviewAnimation : MonoBehaviour
{
    [SerializeField] private GameObject[] pages;
    [SerializeField] private TextMeshProUGUI[] casePreviewTextboxes;
    [SerializeField] private GameObject[] wayPoints;
    [SerializeField] private AnimationCurve curve;
    [SerializeField] private float animationDuration;
    
    private Vector3[] _startPositions;
    private Button _page1Button;
    
    private void Awake()
    {
        _startPositions = new Vector3[pages.Length];
        _startPositions[0] = wayPoints[0].transform.position;
        _startPositions[1] = pages[1].transform.position;
        _startPositions[2] = pages[2].transform.position;

        _page1Button = pages[0].GetComponentInChildren<Button>();
    }

    public async Task PlayAnimation(string newContent)
    {
        float deltaTimeNorm = Time.deltaTime / animationDuration;
        float currentTime = 0f;
        casePreviewTextboxes[2].text = newContent;
        
        if (!pages[0].activeInHierarchy)
        {
            Vector3 page1StartPos = pages[0].transform.position;
            while ((currentTime += deltaTimeNorm) < 1f)
            {
                pages[2].transform.position = Vector3.Lerp(_startPositions[2], page1StartPos, curve.Evaluate(currentTime));
                await Task.Yield();
            }
            pages[0].SetActive(true);
            casePreviewTextboxes[0].text = newContent;
            pages[2].transform.position = _startPositions[2];
            return;    
        }
        
        if (!pages[1].activeInHierarchy)
        {
            Vector3 page1StartPos = pages[0].transform.position;
            while ((currentTime += deltaTimeNorm) < 1f)
            {
                float curveEvaluation = curve.Evaluate(currentTime);
                pages[0].transform.position = Vector3.Lerp(page1StartPos, _startPositions[0], curveEvaluation );
                pages[2].transform.position = Vector3.Lerp(_startPositions[2], _startPositions[1], curveEvaluation);
                
                await Task.Yield();
            }
            pages[1].SetActive(true);
            
        }
        else
        {
            _page1Button.gameObject.SetActive(false);
            while ((currentTime += deltaTimeNorm) < 1f)
            {
                float curveEvaluation = curve.Evaluate(currentTime);
                pages[0].transform.position = Vector3.Lerp(_startPositions[0], wayPoints[1].transform.position, curveEvaluation);
                pages[1].transform.position = Vector3.Lerp(_startPositions[1], _startPositions[0], curveEvaluation );
                pages[2].transform.position = Vector3.Lerp(_startPositions[2], _startPositions[1], curveEvaluation);
                
                await Task.Yield();
            }
            casePreviewTextboxes[0].text = casePreviewTextboxes[1].text;
            _page1Button.gameObject.SetActive(true);
        }
        
        pages[0].transform.position = _startPositions[0];
        pages[1].transform.position = _startPositions[1];
        pages[2].transform.position = _startPositions[2];
        casePreviewTextboxes[1].text = newContent;

    }
}
