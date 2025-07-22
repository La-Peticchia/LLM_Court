using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelector : MonoBehaviour
{
    public List<Sprite> availableSprites;
    public Image previewImage;

    private int currentIndex = 0;

    public void NextCharacter()
    {
        currentIndex = (currentIndex + 1) % availableSprites.Count;
        previewImage.sprite = availableSprites[currentIndex];
    }

    public void PreviousCharacter()
    {
        currentIndex = (currentIndex - 1 + availableSprites.Count) % availableSprites.Count;
        previewImage.sprite = availableSprites[currentIndex];
    }

    public void ConfirmSelection()
    {
        PlayerPrefs.SetInt("SelectedCharacterIndex", currentIndex);
    }
}
