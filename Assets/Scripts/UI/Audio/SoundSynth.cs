using System;

namespace BciChess.UI
{
    public enum GameSound
    {
        Move,
        Capture,
        Castle,
        Check,
        Promote,
        GameStart,
        Victory,
        Defeat,
        Draw,
        Click,
        BciSelect
    }

    /// <summary>
    /// Generates the game's sound effects procedurally (no audio assets needed): wooden piece taps built from
    /// damped resonances, soft bell chimes and short interface ticks. Returns mono samples in [-1, 1].
    /// </summary>
    public static class SoundSynth
    {
        public const int SampleRate = 44100;

        // Partials of a struck wooden bar: frequency ratio, relative amplitude, relative decay.
        private static readonly float[] WoodRatios = { 1f, 2.32f, 4.25f, 6.63f };
        private static readonly float[] WoodAmps = { 1f, 0.55f, 0.28f, 0.14f };
        private static readonly float[] WoodDecays = { 1f, 0.6f, 0.35f, 0.2f };

        public static float[] Generate(GameSound sound)
        {
            switch (sound)
            {
                case GameSound.Move:
                {
                    var b = Buffer(0.22f);
                    Tap(b, 0f, 540f, 1f, 0.03f, 0.25f, 11);
                    return Normalize(b, 0.8f);
                }
                case GameSound.Capture:
                {
                    var b = Buffer(0.26f);
                    Tap(b, 0f, 400f, 1f, 0.028f, 0.5f, 23);
                    Tap(b, 0.026f, 690f, 0.85f, 0.026f, 0.45f, 37);
                    return Normalize(b, 0.9f);
                }
                case GameSound.Castle:
                {
                    var b = Buffer(0.32f);
                    Tap(b, 0f, 540f, 1f, 0.03f, 0.25f, 5);
                    Tap(b, 0.1f, 470f, 0.9f, 0.03f, 0.25f, 7);
                    return Normalize(b, 0.8f);
                }
                case GameSound.Check:
                {
                    var b = Buffer(0.55f);
                    Tap(b, 0f, 540f, 1f, 0.03f, 0.25f, 3);
                    Bell(b, 0.04f, 1046.5f, 0.45f, 0.32f);
                    Bell(b, 0.04f, 1568f, 0.18f, 0.22f);
                    return Normalize(b, 0.8f);
                }
                case GameSound.Promote:
                {
                    var b = Buffer(0.6f);
                    Tap(b, 0f, 540f, 1f, 0.03f, 0.25f, 9);
                    Bell(b, 0.05f, 784f, 0.35f, 0.22f);
                    Bell(b, 0.12f, 987.8f, 0.35f, 0.22f);
                    Bell(b, 0.19f, 1174.7f, 0.4f, 0.3f);
                    return Normalize(b, 0.8f);
                }
                case GameSound.GameStart:
                {
                    var b = Buffer(0.7f);
                    Bell(b, 0f, 784f, 0.5f, 0.3f);
                    Bell(b, 0.14f, 1046.5f, 0.55f, 0.4f);
                    return Normalize(b, 0.7f);
                }
                case GameSound.Victory:
                {
                    var b = Buffer(1.2f);
                    Bell(b, 0f, 784f, 0.45f, 0.3f);
                    Bell(b, 0.12f, 987.8f, 0.45f, 0.3f);
                    Bell(b, 0.24f, 1174.7f, 0.45f, 0.3f);
                    Bell(b, 0.36f, 1568f, 0.6f, 0.55f);
                    return Normalize(b, 0.75f);
                }
                case GameSound.Defeat:
                {
                    var b = Buffer(1.1f);
                    Bell(b, 0f, 659.3f, 0.45f, 0.3f);
                    Bell(b, 0.16f, 587.3f, 0.45f, 0.3f);
                    Bell(b, 0.32f, 493.9f, 0.55f, 0.55f);
                    return Normalize(b, 0.7f);
                }
                case GameSound.Draw:
                {
                    var b = Buffer(0.9f);
                    Bell(b, 0f, 880f, 0.45f, 0.3f);
                    Bell(b, 0.16f, 659.3f, 0.5f, 0.45f);
                    return Normalize(b, 0.7f);
                }
                case GameSound.Click:
                {
                    var b = Buffer(0.06f);
                    Tap(b, 0f, 1900f, 1f, 0.008f, 0.15f, 41);
                    return Normalize(b, 0.35f);
                }
                case GameSound.BciSelect:
                {
                    var b = Buffer(0.45f);
                    Glide(b, 0f, 880f, 1320f, 0.09f, 0.35f);
                    Bell(b, 0.08f, 1318.5f, 0.4f, 0.25f);
                    return Normalize(b, 0.6f);
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(sound), sound, null);
            }
        }

        private static float[] Buffer(float seconds) => new float[(int)(seconds * SampleRate)];

        /// <summary>A short wooden knock: a noise click exciting a few damped bar resonances.</summary>
        private static void Tap(float[] buffer, float start, float frequency, float amplitude, float decay,
            float noiseAmount, int seed)
        {
            int offset = (int)(start * SampleRate);
            var random = new Random(seed);
            float previousNoise = 0f;
            for (int i = 0; offset + i < buffer.Length; i++)
            {
                float t = (float)i / SampleRate;
                float attack = Math.Min(1f, t / 0.0006f);
                float sample = 0f;
                for (int p = 0; p < WoodRatios.Length; p++)
                {
                    float d = decay * WoodDecays[p];
                    sample += WoodAmps[p] * (float)(Math.Sin(2 * Math.PI * frequency * WoodRatios[p] * t) * Math.Exp(-t / d));
                }

                // High-passed noise burst for the contact "click".
                float noise = (float)(random.NextDouble() * 2 - 1);
                float click = (noise - previousNoise) * 0.5f * (float)Math.Exp(-t / 0.004f);
                previousNoise = noise;

                buffer[offset + i] += amplitude * attack * (sample * 0.6f + click * noiseAmount * 2f);
                if (t > decay * 8f)
                    break;
            }
        }

        /// <summary>Soft bell: fundamental plus inharmonic partials with an exponential decay.</summary>
        private static void Bell(float[] buffer, float start, float frequency, float amplitude, float decay)
        {
            int offset = (int)(start * SampleRate);
            for (int i = 0; offset + i < buffer.Length; i++)
            {
                float t = (float)i / SampleRate;
                float attack = Math.Min(1f, t / 0.004f);
                double w = 2 * Math.PI * frequency * t;
                float sample = (float)(Math.Sin(w) * Math.Exp(-t / decay)
                                       + 0.35 * Math.Sin(2.76 * w) * Math.Exp(-t / (decay * 0.45))
                                       + 0.15 * Math.Sin(5.4 * w) * Math.Exp(-t / (decay * 0.25)));
                buffer[offset + i] += amplitude * attack * sample;
            }
        }

        /// <summary>Sine sweeping from <paramref name="from"/> to <paramref name="to"/> Hz with a smooth envelope.</summary>
        private static void Glide(float[] buffer, float start, float from, float to, float seconds, float amplitude)
        {
            int offset = (int)(start * SampleRate);
            int length = (int)(seconds * SampleRate);
            double phase = 0;
            for (int i = 0; i < length && offset + i < buffer.Length; i++)
            {
                float progress = (float)i / length;
                phase += 2 * Math.PI * (from + (to - from) * progress) / SampleRate;
                float envelope = (float)Math.Sin(Math.PI * progress);
                buffer[offset + i] += amplitude * envelope * (float)Math.Sin(phase);
            }
        }

        private static float[] Normalize(float[] buffer, float peak)
        {
            float max = 0f;
            foreach (float s in buffer)
                max = Math.Max(max, Math.Abs(s));
            if (max <= 0f)
                return buffer;
            float gain = peak / max;
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] *= gain;

            // Short fade-out so no clip ends with a click.
            int fade = Math.Min(buffer.Length, SampleRate / 200);
            for (int i = 0; i < fade; i++)
                buffer[buffer.Length - 1 - i] *= (float)i / fade;
            return buffer;
        }
    }
}
