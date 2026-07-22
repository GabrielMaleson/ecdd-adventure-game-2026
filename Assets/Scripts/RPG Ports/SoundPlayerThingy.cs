using UnityEngine;

public class SoundPlayerThingy : MonoBehaviour
{
    public AudioClip clip;
    private SFXManager sfx;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        sfx = SFXManager.Instance;
        sfx.Play(clip);
    }
}
