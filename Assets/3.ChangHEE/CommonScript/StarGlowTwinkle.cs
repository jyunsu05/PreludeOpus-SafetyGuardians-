using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class StarGlowTwinkle : MonoBehaviour
{
    public float minAlpha = 0.2f;
    public float maxAlpha = 1f;
    public float minSpeed = 0.5f;
    public float maxSpeed = 2f;

    Image img;
    float speed;
    float offset;

    void Awake()
    {
        img = GetComponent<Image>();
        ResetTwinkle();
    }

    public void Configure(float newMinAlpha, float newMaxAlpha, float newMinSpeed, float newMaxSpeed)
    {
        minAlpha = newMinAlpha;
        maxAlpha = newMaxAlpha;
        minSpeed = newMinSpeed;
        maxSpeed = newMaxSpeed;
        ResetTwinkle();
    }

    void ResetTwinkle()
    {
        speed = Random.Range(minSpeed, maxSpeed);
        offset = Random.Range(0f, 10f);
    }

    void Update()
    {
        float t = Mathf.PingPong(Time.time * speed + offset, 1f);
        Color c = img.color;
        c.a = Mathf.Lerp(minAlpha, maxAlpha, t);
        img.color = c;
    }
}
