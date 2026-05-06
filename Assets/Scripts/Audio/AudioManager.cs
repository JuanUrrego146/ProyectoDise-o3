using UnityEngine;

namespace LingoteRush.Audio
{
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        private AudioSource musicSource;
        private AudioSource sfxSource;
        private AudioClip currentMusicClip;

        public float MasterVolume => masterVolume;

        public float MusicVolume => musicVolume;

        public float SfxVolume => sfxVolume;

        private void Awake()
        {
            EnsureAudioSources();
            ApplyVolumes();
        }

        public void PlayMusic(AudioClip clip, bool restartIfSameClip = false)
        {
            if (clip == null)
            {
                return;
            }

            if (!restartIfSameClip && currentMusicClip == clip && musicSource.isPlaying)
            {
                return;
            }

            currentMusicClip = clip;
            musicSource.clip = clip;
            musicSource.Play();
        }

        public void StopMusic()
        {
            currentMusicClip = null;
            musicSource.Stop();
            musicSource.clip = null;
        }

        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null)
            {
                return;
            }

            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        public void SetMasterVolume(float value)
        {
            masterVolume = Mathf.Clamp01(value);
            ApplyVolumes();
        }

        public void SetMusicVolume(float value)
        {
            musicVolume = Mathf.Clamp01(value);
            ApplyVolumes();
        }

        public void SetSfxVolume(float value)
        {
            sfxVolume = Mathf.Clamp01(value);
            ApplyVolumes();
        }

        private void EnsureAudioSources()
        {
            var sources = GetComponents<AudioSource>();

            if (sources.Length == 0)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                sfxSource = gameObject.AddComponent<AudioSource>();
            }
            else if (sources.Length == 1)
            {
                musicSource = sources[0];
                sfxSource = gameObject.AddComponent<AudioSource>();
            }
            else
            {
                musicSource = sources[0];
                sfxSource = sources[1];
            }

            ConfigureSources();
        }

        private void ConfigureSources()
        {
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;

            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;
        }

        private void ApplyVolumes()
        {
            AudioListener.volume = masterVolume;

            if (musicSource != null)
            {
                musicSource.volume = musicVolume;
            }

            if (sfxSource != null)
            {
                sfxSource.volume = sfxVolume;
            }
        }
    }
}
