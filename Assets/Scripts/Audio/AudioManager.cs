using System.Collections.Generic;
using UnityEngine;

namespace LastFight
{
    public enum SoundId { PlayerAttack, Hit, PlayerHurt, EnemyDeath, Victory, Defeat }

    /// <summary>
    /// Звуки и музыка. Если в Inspector назначен AudioClip — играет он.
    /// Если поле пустое — автоматически генерируется простой звук, поэтому игра звучит
    /// и без аудиофайлов, а позже файлы можно просто перетащить в поля.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Свои звуки (необязательно — пустое поле = встроенный звук)")]
        [SerializeField] private AudioClip music;
        [SerializeField] private AudioClip playerAttack;
        [SerializeField] private AudioClip hit;
        [SerializeField] private AudioClip playerHurt;
        [SerializeField] private AudioClip enemyDeath;
        [SerializeField] private AudioClip victory;
        [SerializeField] private AudioClip defeat;

        [Header("Громкость")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.8f;

        private readonly Dictionary<SoundId, AudioClip> clips = new Dictionary<SoundId, AudioClip>();
        private AudioSource musicSource;
        private AudioSource sfxSource;

        /// <summary>Безопасный вызов из любого скрипта: если менеджера нет — ничего не происходит.</summary>
        public static void Play(SoundId id)
        {
            if (Instance == null) return;
            Instance.PlayInternal(id);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.volume = masterVolume * musicVolume;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;

            BuildClips();
        }

        private void Start()
        {
            if (Instance != this || music == null) return;
            musicSource.clip = music;
            musicSource.Play();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildClips()
        {
            if (music == null)
                music = ProceduralAudio.Melody("music", new[] { 110f, 130.8f, 164.8f, 130.8f, 98f, 123.5f, 146.8f, 123.5f },
                    0.3f, ProceduralAudio.Wave.Square, 0.25f, 1.5f);

            clips[SoundId.PlayerAttack] = playerAttack != null ? playerAttack
                : ProceduralAudio.Tone("attack", 0f, 0f, 0.12f, ProceduralAudio.Wave.Noise, 0.3f);
            clips[SoundId.Hit] = hit != null ? hit
                : ProceduralAudio.Tone("hit", 220f, 80f, 0.1f, ProceduralAudio.Wave.Square, 0.5f);
            clips[SoundId.PlayerHurt] = playerHurt != null ? playerHurt
                : ProceduralAudio.Tone("hurt", 160f, 60f, 0.2f, ProceduralAudio.Wave.Square, 0.55f);
            clips[SoundId.EnemyDeath] = enemyDeath != null ? enemyDeath
                : ProceduralAudio.Tone("enemyDeath", 420f, 60f, 0.3f, ProceduralAudio.Wave.Sine, 0.6f);
            clips[SoundId.Victory] = victory != null ? victory
                : ProceduralAudio.Melody("victory", new[] { 523.3f, 659.3f, 784f, 1046.5f }, 0.18f, ProceduralAudio.Wave.Square, 0.5f, 0.6f);
            clips[SoundId.Defeat] = defeat != null ? defeat
                : ProceduralAudio.Melody("defeat", new[] { 392f, 329.6f, 261.6f, 196f }, 0.3f, ProceduralAudio.Wave.Square, 0.5f, 0.6f);
        }

        private void PlayInternal(SoundId id)
        {
            if (sfxSource == null) return;
            if (clips.TryGetValue(id, out AudioClip clip) && clip != null)
                sfxSource.PlayOneShot(clip, masterVolume * sfxVolume);
        }
    }

    /// <summary>Генерация простых звуков в коде (запасной вариант, если нет аудиофайлов).</summary>
    internal static class ProceduralAudio
    {
        public enum Wave { Sine, Square, Noise }

        private const int SampleRate = 44100;

        public static AudioClip Tone(string name, float freqStart, float freqEnd, float duration, Wave wave, float volume)
        {
            int count = Mathf.Max(1, (int)(SampleRate * duration));
            float[] data = new float[count];
            Fill(data, 0, count, freqStart, freqEnd, wave, volume, 2f);
            return Create(name, data);
        }

        public static AudioClip Melody(string name, float[] notes, float noteDuration, Wave wave, float volume, float decay)
        {
            int perNote = Mathf.Max(1, (int)(SampleRate * noteDuration));
            float[] data = new float[perNote * notes.Length];
            for (int i = 0; i < notes.Length; i++)
                Fill(data, i * perNote, perNote, notes[i], notes[i], wave, volume, decay);
            return Create(name, data);
        }

        private static void Fill(float[] data, int start, int count, float f0, float f1, Wave wave, float volume, float decay)
        {
            double phase = 0.0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float freq = Mathf.Lerp(f0, f1, t);
                phase += 2.0 * System.Math.PI * freq / SampleRate;

                float sample;
                switch (wave)
                {
                    case Wave.Sine: sample = (float)System.Math.Sin(phase); break;
                    case Wave.Square: sample = System.Math.Sin(phase) >= 0.0 ? 0.5f : -0.5f; break;
                    case Wave.Noise: sample = Random.value * 2f - 1f; break;
                    default: sample = 0f; break;
                }

                float envelope = Mathf.Pow(1f - t, decay);
                float attack = Mathf.Min(1f, i / 150f);
                data[start + i] = sample * envelope * attack * volume;
            }
        }

        private static AudioClip Create(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
