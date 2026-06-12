using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 배틀 몬스터 Image에 Atlas Idle Loop / Hit 1회 재생을 처리합니다.
/// 프레임 이름 규칙:
/// - Idle: {image_key}_idle_0 또는 {image_key}_Idle_0 ... (없으면 {image_key}_0, _1 ... 폴백)
/// - Hit:  {image_key}_hit_0 또는 {image_key}_Hit_0 ...
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class BattleMonsterSpriteLooper : MonoBehaviour
{
    [SerializeField] private float framesPerSecond = 8f;

    private Image targetImage;
    private Sprite[] idleSprites;
    private Sprite[] hitSprites;
    private Coroutine activeRoutine;
    private bool wantsIdleLoop;

    private const int MaxAtlasFrameProbe = 128;

    public bool IsPlayingIdle { get; private set; }
    public bool IsPlayingHit { get; private set; }

    private void Awake()
    {
        targetImage = GetComponent<Image>();
    }

    private void OnEnable()
    {
        if (wantsIdleLoop && !IsPlayingHit)
            PlayIdleLoop();
    }

    private void OnDisable()
    {
        StopActiveRoutine();
        IsPlayingIdle = false;
        IsPlayingHit = false;
    }

    public bool ConfigureFromAtlas(string baseKey)
    {
        if (string.IsNullOrEmpty(baseKey) || AtlasManager.Instance == null)
            return false;

        idleSprites = CollectAtlasFrames($"{baseKey}_idle", $"{baseKey}_Idle");
        if (!HasValidSprites(idleSprites))
            idleSprites = CollectAtlasFrames(baseKey);

        hitSprites = CollectAtlasFrames($"{baseKey}_hit", $"{baseKey}_Hit");

        if (HasValidSprites(idleSprites) && idleSprites.Length == 1)
        {
            Debug.LogWarning(
                $"[{nameof(BattleMonsterSpriteLooper)}] Idle 프레임이 1장뿐입니다. Atlas 슬라이스 이름/개수를 확인하세요: {baseKey}");
        }

        if (HasValidSprites(idleSprites))
        {
            wantsIdleLoop = true;
            ApplyIdleFrame(0);
        }

        return HasValidSprites(idleSprites);
    }

    public void PlayIdleLoop()
    {
        if (!HasValidSprites(idleSprites))
        {
            Debug.LogWarning($"[{nameof(BattleMonsterSpriteLooper)}] Idle Atlas 프레임을 찾지 못했습니다.");
            wantsIdleLoop = false;
            return;
        }

        wantsIdleLoop = true;

        if (!isActiveAndEnabled)
        {
            ApplyIdleFrame(0);
            return;
        }

        StartRoutine(IdleLoopRoutine());
    }

    public float GetHitAnimationDuration()
    {
        if (!HasValidSprites(hitSprites))
            return 0f;

        float interval = 1f / Mathf.Max(framesPerSecond, 0.01f);
        return hitSprites.Length * interval;
    }

    public IEnumerator PlayHitOnceRoutine()
    {
        if (!HasValidSprites(hitSprites))
            yield break;

        StopActiveRoutine();
        IsPlayingIdle = false;
        IsPlayingHit = true;

        float interval = 1f / Mathf.Max(framesPerSecond, 0.01f);

        for (int i = 0; i < hitSprites.Length; i++)
        {
            ApplySprite(hitSprites[i]);
            yield return new WaitForSecondsRealtime(interval);
        }

        IsPlayingHit = false;
        PlayIdleLoop();
    }

    public void StopAll()
    {
        wantsIdleLoop = false;
        StopActiveRoutine();
        IsPlayingIdle = false;
        IsPlayingHit = false;
        ApplyIdleFrame(0);
    }

    private IEnumerator IdleLoopRoutine()
    {
        IsPlayingIdle = true;
        IsPlayingHit = false;

        float interval = 1f / Mathf.Max(framesPerSecond, 0.01f);
        int frameIndex = 0;

        while (true)
        {
            ApplyIdleFrame(frameIndex);
            frameIndex = (frameIndex + 1) % idleSprites.Length;
            yield return new WaitForSecondsRealtime(interval);
        }
    }

    private void StartRoutine(IEnumerator routine)
    {
        if (!isActiveAndEnabled)
            return;

        StopActiveRoutine();
        activeRoutine = StartCoroutine(routine);
    }

    private void StopActiveRoutine()
    {
        if (activeRoutine == null)
            return;

        StopCoroutine(activeRoutine);
        activeRoutine = null;
    }

    private static Sprite[] CollectAtlasFrames(params string[] namePrefixes)
    {
        if (namePrefixes == null || namePrefixes.Length == 0)
            return System.Array.Empty<Sprite>();

        foreach (string namePrefix in namePrefixes)
        {
            if (string.IsNullOrEmpty(namePrefix))
                continue;

            Sprite[] frames = CollectAtlasFramesForPrefix(namePrefix);
            if (HasValidSprites(frames))
                return frames;
        }

        return System.Array.Empty<Sprite>();
    }

    private static Sprite[] CollectAtlasFramesForPrefix(string namePrefix)
    {
        var collected = new List<Sprite>(MaxAtlasFrameProbe);

        // 슬라이스 번호가 0,3,8...처럼 비연속이어도 수집합니다.
        for (int i = 0; i < MaxAtlasFrameProbe; i++)
        {
            Sprite frame = AtlasManager.Instance.TryGetMonsterSpriteExact($"{namePrefix}_{i}");
            if (frame != null)
                collected.Add(frame);
        }

        if (collected.Count == 0)
        {
            Sprite fallback = AtlasManager.Instance.TryGetMonsterSpriteExact(namePrefix);
            if (fallback != null)
                collected.Add(fallback);
        }

        return collected.ToArray();
    }

    private void ApplyIdleFrame(int index)
    {
        if (idleSprites == null || idleSprites.Length == 0)
            return;

        index = Mathf.Clamp(index, 0, idleSprites.Length - 1);
        ApplySprite(idleSprites[index]);
    }

    private void ApplySprite(Sprite sprite)
    {
        if (targetImage == null)
            return;

        targetImage.sprite = sprite;
    }

    private static bool HasValidSprites(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return false;

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
                return true;
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (framesPerSecond <= 0f)
            framesPerSecond = 8f;
    }
#endif
}
