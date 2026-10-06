using System.Collections.Generic;
using BciChess.Core;
using UnityEngine;

namespace BciChess.UI
{
    /// <summary>Plays the generated sound effects, honouring the volume and category switches in <see cref="GamePreferences"/>.</summary>
    public sealed class GameAudio : MonoBehaviour
    {
        private readonly Dictionary<GameSound, AudioClip> _clips = new Dictionary<GameSound, AudioClip>();
        private AudioSource _source;
        private GamePreferences _preferences;

        public void Initialize(GamePreferences preferences)
        {
            _preferences = preferences;
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;

            if (FindObjectOfType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();
        }

        public void Play(GameSound sound)
        {
            if (_source == null || _preferences == null || !_preferences.soundEnabled || !IsCategoryOn(sound))
                return;
            _source.PlayOneShot(GetClip(sound), Mathf.Clamp01(_preferences.masterVolume));
        }

        /// <summary>The sound for a move just played: game end, check, promotion, castling, capture or a plain move.</summary>
        public void PlayMove(ChessGame game, ChessMove move, PieceColor? humanColor)
        {
            if (game.IsGameOver)
            {
                if (game.Winner == null)
                    Play(GameSound.Draw);
                else
                    Play(humanColor.HasValue && game.Winner != humanColor ? GameSound.Defeat : GameSound.Victory);
            }
            else if (game.IsInCheck)
                Play(GameSound.Check);
            else if (move.IsPromotion)
                Play(GameSound.Promote);
            else if (move.IsCastling)
                Play(GameSound.Castle);
            else if (move.IsCapture)
                Play(GameSound.Capture);
            else
                Play(GameSound.Move);
        }

        private bool IsCategoryOn(GameSound sound)
        {
            switch (sound)
            {
                case GameSound.Click: return _preferences.interfaceSounds;
                case GameSound.BciSelect: return _preferences.bciSounds;
                default: return _preferences.gameSounds;
            }
        }

        private AudioClip GetClip(GameSound sound)
        {
            if (_clips.TryGetValue(sound, out var clip))
                return clip;
            var samples = SoundSynth.Generate(sound);
            clip = AudioClip.Create(sound.ToString(), samples.Length, 1, SoundSynth.SampleRate, false);
            clip.SetData(samples, 0);
            _clips[sound] = clip;
            return clip;
        }
    }
}
