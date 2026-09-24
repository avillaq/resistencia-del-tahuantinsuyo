using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using ResistenciaTahuantinsuyo.Runtime.Gameplay;
using Random = UnityEngine.Random;

namespace ResistenciaTahuantinsuyo.Runtime.Audio
{
    /// <summary>
    /// Administrador central de Audio según product.md (Sección 13 y 15).
    /// Implementa las 4 buenas prácticas de audio para videojuegos:
    /// 1. Jerarquía de Buses y Headroom (Master, Music, Ambience, SFX, Footsteps, UI).
    /// 2. Ducking Dinámico / Snapshots de Estado (Exploration vs Tension).
    /// 3. Control de Volúmenes por AudioMixer con escala logarítmica (UI Settings).
    /// 4. Variación Acústica Anti-Fatiga (micro-variación de pitch y volumen en SFX repetitivos).
    /// Gestiona además la desconexión limpia de sonidos de gameplay al finalizar la misión.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Mixer & Routing")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup ambienceGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup footstepsGroup;
        [SerializeField] private AudioMixerGroup uiGroup;

        [Header("Snapshots de Estado y Ducking")]
        [SerializeField] private AudioMixerSnapshot snapshotExploration;
        [SerializeField] private AudioMixerSnapshot snapshotTension;

        [Header("Fuentes de Audio")]
        [SerializeField] private AudioSource musicSourceA;
        [SerializeField] private AudioSource musicSourceB;
        [SerializeField] private AudioSource ambienceSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource footstepSource;
        [SerializeField] private AudioSource uiSource;

        [Header("Pistas de Música")]
        [SerializeField] private AudioClip musicMenu;
        [SerializeField] private AudioClip musicExploration;
        [SerializeField] private AudioClip musicTension;
        [SerializeField] private AudioClip musicMissionComplete;
        [SerializeField] private AudioClip musicMissionFailed;

        [Header("Pistas de Ambiente")]
        [SerializeField] private AudioClip ambienceAndes;
        [SerializeField] private AudioClip ambienceCamp;
        [SerializeField] private AudioClip ambienceCoast;
        [SerializeField] private AudioClip ambienceSettlement;

        [Header("Efectos de Sonido - Jugador")]
        [SerializeField] private AudioClip sfxPlayerInteract;
        [SerializeField] private AudioClip sfxPlayerDamage;
        [SerializeField] private AudioClip[] sfxPlayerStepStone;
        [SerializeField] private AudioClip[] sfxPlayerStepDirt;

        [Header("Efectos de Sonido - Enemigos")]
        [SerializeField] private AudioClip sfxEnemyAlert;
        [SerializeField] private AudioClip sfxEnemyLostSight;
        [SerializeField] private AudioClip[] sfxEnemyStepArmor;

        [Header("Efectos de Sonido - UI")]
        [SerializeField] private AudioClip sfxUiSelect;
        [SerializeField] private AudioClip sfxUiPause;
        [SerializeField] private AudioClip sfxUiResume;
        [SerializeField] private AudioClip sfxObjectRecovered;

        private bool isUsingSourceA = true;
        private Coroutine musicFadeCoroutine;
        private int activeEnemiesAlerted = 0;

        public event Action<bool> OnTensionChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ValidateAndSetupAudioSources();
        }

        public void ValidateAndSetupAudioSources()
        {
            if (musicSourceA == null) musicSourceA = gameObject.AddComponent<AudioSource>();
            if (musicSourceB == null) musicSourceB = gameObject.AddComponent<AudioSource>();
            if (ambienceSource == null) ambienceSource = gameObject.AddComponent<AudioSource>();
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
            if (footstepSource == null) footstepSource = gameObject.AddComponent<AudioSource>();
            if (uiSource == null) uiSource = gameObject.AddComponent<AudioSource>();

            if (musicGroup != null)
            {
                if (musicSourceA.outputAudioMixerGroup == null) musicSourceA.outputAudioMixerGroup = musicGroup;
                if (musicSourceB.outputAudioMixerGroup == null) musicSourceB.outputAudioMixerGroup = musicGroup;
            }
            if (ambienceGroup != null && ambienceSource.outputAudioMixerGroup == null) ambienceSource.outputAudioMixerGroup = ambienceGroup;
            if (sfxGroup != null && sfxSource.outputAudioMixerGroup == null) sfxSource.outputAudioMixerGroup = sfxGroup;
            if (footstepsGroup != null && footstepSource.outputAudioMixerGroup == null) footstepSource.outputAudioMixerGroup = footstepsGroup;
            if (uiGroup != null && uiSource.outputAudioMixerGroup == null) uiSource.outputAudioMixerGroup = uiGroup;

            musicSourceA.loop = true;
            musicSourceB.loop = true;
            ambienceSource.loop = true;

            musicSourceA.playOnAwake = false;
            musicSourceB.playOnAwake = false;
            ambienceSource.playOnAwake = false;
            sfxSource.playOnAwake = false;
            footstepSource.playOnAwake = false;
            uiSource.playOnAwake = false;
        }

        private void Start()
        {
            if (snapshotExploration != null)
            {
                snapshotExploration.TransitionTo(0.1f);
            }

            if (ambienceAndes != null)
            {
                PlayAmbience(ambienceAndes);
            }

            if (musicExploration != null)
            {
                PlayMusic(musicExploration, 1.5f);
            }
        }

        #region Control de Música y Crossfade

        public void PlayMusic(AudioClip clip, float fadeDuration = 0.8f)
        {
            if (clip == null) return;

            AudioSource currentSource = isUsingSourceA ? musicSourceA : musicSourceB;
            AudioSource newSource = isUsingSourceA ? musicSourceB : musicSourceA;

            if (currentSource.clip == clip && currentSource.isPlaying) return;

            if (musicFadeCoroutine != null)
            {
                StopCoroutine(musicFadeCoroutine);
            }

            musicFadeCoroutine = StartCoroutine(CrossfadeMusicRoutine(currentSource, newSource, clip, fadeDuration));
            isUsingSourceA = !isUsingSourceA;
        }

        private IEnumerator CrossfadeMusicRoutine(AudioSource fromSource, AudioSource toSource, AudioClip newClip, float duration)
        {
            toSource.clip = newClip;
            toSource.volume = 0f;
            toSource.Play();

            float timer = 0f;
            float startVol = fromSource.volume;
            float targetVol = 1.0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / duration);

                fromSource.volume = Mathf.Lerp(startVol, 0f, t);
                toSource.volume = Mathf.Lerp(0f, targetVol, t);
                yield return null;
            }

            fromSource.volume = 0f;
            fromSource.Stop();
            toSource.volume = targetVol;
            musicFadeCoroutine = null;
        }

        public void SetTensionMusic(bool active)
        {
            if (ScoreManager.Instance != null && ScoreManager.Instance.IsMissionFinished) return;

            if (active)
            {
                activeEnemiesAlerted++;
                if (activeEnemiesAlerted == 1)
                {
                    if (musicTension != null)
                    {
                        PlayMusic(musicTension, 0.4f);
                    }

                    if (snapshotTension != null)
                    {
                        snapshotTension.TransitionTo(0.35f);
                    }

                    OnTensionChanged?.Invoke(true);
                }
            }
            else
            {
                activeEnemiesAlerted = Mathf.Max(0, activeEnemiesAlerted - 1);
                if (activeEnemiesAlerted == 0)
                {
                    if (musicExploration != null)
                    {
                        PlayMusic(musicExploration, 1.2f);
                    }

                    if (snapshotExploration != null)
                    {
                        snapshotExploration.TransitionTo(1.0f);
                    }

                    OnTensionChanged?.Invoke(false);
                }
            }
        }

        public void PlayMissionComplete()
        {
            activeEnemiesAlerted = 0;
            StopGameplayAudioOnMissionEnd();

            if (snapshotExploration != null)
            {
                snapshotExploration.TransitionTo(0.2f);
            }

            if (musicMissionComplete != null)
            {
                PlayMusic(musicMissionComplete, 0.5f);
            }
        }

        public void PlayMissionFailed()
        {
            // Si la misión ya finalizó con victoria, nunca reproducir música de derrota
            if (ScoreManager.Instance != null && ScoreManager.Instance.IsMissionFinished && ScoreManager.Instance.IsVictory)
            {
                return;
            }

            activeEnemiesAlerted = 0;
            StopGameplayAudioOnMissionEnd();

            if (musicMissionFailed != null)
            {
                PlayMusic(musicMissionFailed, 0.4f);
            }
        }

        public void StopGameplayAudioOnMissionEnd()
        {
            if (footstepSource != null && footstepSource.isPlaying)
            {
                footstepSource.Stop();
            }
            if (sfxSource != null && sfxSource.isPlaying)
            {
                sfxSource.Stop();
            }
        }

        #endregion

        #region Control de Ambiente

        public void PlayAmbience(AudioClip clip)
        {
            if (clip == null) return;
            ambienceSource.clip = clip;
            ambienceSource.volume = 1.0f;
            ambienceSource.Play();
        }

        #endregion

        #region Efectos de Sonido (SFX) con Variación Acústica Anti-Fatiga

        public void PlaySFX(AudioClip clip, float volumeScale = 1.0f, float pitchVariation = 0.04f)
        {
            if (clip == null || sfxSource == null) return;

            float volRandom = 1f + Random.Range(-0.04f, 0.04f);
            sfxSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            sfxSource.PlayOneShot(clip, volumeScale * volRandom);
        }

        public void PlayPlayerDamage()
        {
            if (ScoreManager.Instance != null && ScoreManager.Instance.IsMissionFinished) return;

            if (sfxPlayerDamage != null)
            {
                PlaySFX(sfxPlayerDamage, 1.0f, 0.08f);
            }
            else
            {
                PlaySFX(sfxEnemyLostSight, 1.0f, 0.15f);
            }
        }

        public void PlayEnemyAlert()
        {
            if (ScoreManager.Instance != null && ScoreManager.Instance.IsMissionFinished) return;
            PlaySFX(sfxEnemyAlert, 1.0f, 0.02f);
        }

        public void PlayEnemyLostSight()
        {
            if (ScoreManager.Instance != null && ScoreManager.Instance.IsMissionFinished) return;
            PlaySFX(sfxEnemyLostSight, 0.9f, 0.03f);
        }

        public void PlayPlayerStep(bool onStone)
        {
            if (ScoreManager.Instance != null && ScoreManager.Instance.IsMissionFinished) return;

            AudioClip[] steps = onStone ? sfxPlayerStepStone : sfxPlayerStepDirt;
            if (steps != null && steps.Length > 0 && footstepSource != null)
            {
                footstepSource.pitch = 1f + Random.Range(-0.07f, 0.07f);
                float volVar = 1f + Random.Range(-0.06f, 0.06f);
                int idx = Random.Range(0, steps.Length);
                footstepSource.PlayOneShot(steps[idx], 0.85f * volVar);
            }
        }

        public void PlayEnemyStep()
        {
            if (ScoreManager.Instance != null && ScoreManager.Instance.IsMissionFinished) return;

            if (sfxEnemyStepArmor != null && sfxEnemyStepArmor.Length > 0 && footstepSource != null)
            {
                footstepSource.pitch = 1f + Random.Range(-0.05f, 0.05f);
                float volVar = 1f + Random.Range(-0.05f, 0.05f);
                int idx = Random.Range(0, sfxEnemyStepArmor.Length);
                footstepSource.PlayOneShot(sfxEnemyStepArmor[idx], 0.9f * volVar);
            }
        }

        public void PlayUISelect() => PlayUISound(sfxUiSelect, 0.9f);
        public void PlayUIPause() => PlayUISound(sfxUiPause, 0.9f);
        public void PlayUIResume() => PlayUISound(sfxUiResume, 0.9f);
        public void PlayObjectRecovered() => PlayUISound(sfxObjectRecovered, 1.0f);

        private void PlayUISound(AudioClip clip, float volumeScale = 1.0f)
        {
            if (clip == null) return;
            AudioSource source = uiSource != null ? uiSource : sfxSource;
            if (source != null)
            {
                source.pitch = 1.0f;
                source.PlayOneShot(clip, volumeScale);
            }
        }

        #endregion

        #region Control de Volúmenes por AudioMixer (Escala Logarítmica para UI)

        public void SetMasterVolume(float linear01) => SetExposedVolume("MasterVolume", linear01);
        public void SetMusicVolume(float linear01) => SetExposedVolume("MusicVolume", linear01);
        public void SetAmbienceVolume(float linear01) => SetExposedVolume("AmbienceVolume", linear01);
        public void SetSFXVolume(float linear01) => SetExposedVolume("SFXVolume", linear01);
        public void SetFootstepsVolume(float linear01) => SetExposedVolume("FootstepsVolume", linear01);

        private void SetExposedVolume(string exposedParam, float linear01)
        {
            if (audioMixer == null) return;
            float clamped = Mathf.Clamp(linear01, 0.0001f, 1f);
            float dB = Mathf.Log10(clamped) * 20f;
            audioMixer.SetFloat(exposedParam, dB);
        }

        #endregion
    }
}