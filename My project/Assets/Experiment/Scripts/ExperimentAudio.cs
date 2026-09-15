using UnityEngine;

public static class ExperimentAudio
{
    public static AudioClip CreateTone(float frequency = 880f, float duration = 0.6f, int sampleRate = 44100)
    {
        var sampleCount = Mathf.CeilToInt(sampleRate * duration);
        var samples = new float[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (float)sampleRate;
            var envelope = 1f;
            var fade = 0.05f;
            if (t < fade)
                envelope = t / fade;
            else if (t > duration - fade)
                envelope = Mathf.Max(0f, (duration - t) / fade);

            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.35f;
        }

        var clip = AudioClip.Create("EndTone", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    public static void PlayTone(AudioSource source, AudioClip clip)
    {
        if (source == null || clip == null)
            return;

        source.Stop();
        source.clip = clip;
        source.Play();
    }
}
