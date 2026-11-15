using UnityEngine;
using UnityEngine.UI;

public class ButtonSFX : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip audioClip;
    public Button[] buttons;
    void OnEnable()
    {
        foreach(Button b in buttons)
        {
            b.onClick.AddListener(toggleSound);
        }
    }

    private void toggleSound()
    {
        audioSource.PlayOneShot(audioClip);
    }
}
