using UnityEngine;
using UnityEngine.Audio;

namespace LoveAlgo.UI
{
    public class UISoundManager : MonoBehaviour
    {
        [SerializeField] public AudioClip hoverClip;
        [SerializeField] public AudioClip clickClip;
        [SerializeField] public AudioClip typingClip;
        [SerializeField] public AudioClip dialogueNextClip;
        [SerializeField] public AudioClip choiceSelectClip;
        [SerializeField] public AudioClip choiceAppearClip;
        [SerializeField] public AudioClip choiceHoverClip;
        [SerializeField] public AudioClip popupOpenClip;
        [SerializeField] public AudioClip popupCloseClip;
        [SerializeField] public AudioClip saveCompleteClip;
        [SerializeField] public AudioClip loadCompleteClip;
        [SerializeField] public AudioClip volumePreviewClip;
        [SerializeField] public AudioMixerGroup sfxMixerGroup;
        [SerializeField] public float hoverVolume = 0.5f;
        [SerializeField] public float clickVolume = 0.7f;
        [SerializeField] public float minTypingPitch = 0.9f;
        [SerializeField] public float maxTypingPitch = 1.1f;
        [SerializeField] public float minTypingVolume = 0.35f;
        [SerializeField] public float maxTypingVolume = 0.5f;
        [SerializeField] public float typingMinInterval = 0.035f;
        [SerializeField] public float volumePreviewDebounce = 0.08f;
        [SerializeField] public bool autoBindButtons = true;
    }
}
