using UnityEngine;

/// <summary>
/// StarParticles 전용. 파티클 알파 + 크기를 같이 바꿔 URP에서도 반짝임이 보이게 합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public class StarTwinkle : MonoBehaviour
{
    [SerializeField] float minAlpha = 0.1f;
    [SerializeField] float maxAlpha = 1f;
    [SerializeField] float minSpeed = 0.3f;
    [SerializeField] float maxSpeed = 1.5f;
    [SerializeField] bool pulseSize = true;
    [SerializeField] float minSizeMultiplier = 0.35f;
    [SerializeField] float maxSizeMultiplier = 1f;
    [SerializeField] bool disableColorModules = true;

    ParticleSystem ps;
    ParticleSystem.Particle[] particles;
    float baseStartSize;

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        baseStartSize = ps.main.startSize.constant;

        if (disableColorModules)
        {
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = false;

            var colorBySpeed = ps.colorBySpeed;
            colorBySpeed.enabled = false;
        }
    }

    void OnEnable()
    {
        if (ps != null && !ps.isPlaying)
            ps.Play(withChildren: true);
    }

    void LateUpdate()
    {
        if (ps == null)
            return;

        int count = ps.particleCount;
        if (count == 0)
            return;

        int bufferSize = ps.main.maxParticles;
        if (particles == null || particles.Length < bufferSize)
            particles = new ParticleSystem.Particle[bufferSize];

        int alive = ps.GetParticles(particles);
        float speedRange = maxSpeed - minSpeed;
        float sizeRange = maxSizeMultiplier - minSizeMultiplier;

        for (int i = 0; i < alive; i++)
        {
            uint seed = particles[i].randomSeed;
            float speed = minSpeed + (seed % 1000) / 1000f * speedRange;
            float phase = (seed % 6283) * 0.001f;

            float t = (Mathf.Sin(Time.time * speed + phase) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);

            Color c = particles[i].GetCurrentColor(ps);
            c.a = alpha;
            particles[i].startColor = c;

            if (pulseSize)
            {
                float sizeMul = Mathf.Lerp(minSizeMultiplier, maxSizeMultiplier, t);
                particles[i].startSize = baseStartSize * sizeMul;
            }
        }

        ps.SetParticles(particles, alive);
    }
}
