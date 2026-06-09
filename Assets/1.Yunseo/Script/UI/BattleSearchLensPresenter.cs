using System.Collections;
using UnityEngine;

/// <summary>
/// SearchLens 오브젝트에 붙여 탐색 연출을 제어합니다.
/// Animator/상태/트리거 이름은 인스펙터에서 본인 세팅에 맞게 지정하세요.
/// </summary>
[DisallowMultipleComponent]
public class BattleSearchLensPresenter : MonoBehaviour
{
    private const int UiLayer = 5;

    [Header("--- SearchLens 연출 (인스펙터에서 설정) ---")]
    [SerializeField] private Animator animator;
    [Tooltip("Animator Controller의 기본 재생 상태 이름")]
    [SerializeField] private string searchStateName = "SearchLens";
    [Tooltip("트리거 파라미터가 있을 때만 사용합니다. 없으면 상태 Play로 재생합니다.")]
    [SerializeField] private string searchTriggerName = "";
    [Tooltip("연출 재생 전 오브젝트를 켭니다.")]
    [SerializeField] private bool activateOnPlay = true;
    [Tooltip("연출 종료 후 오브젝트를 끕니다.")]
    [SerializeField] private bool deactivateOnStop = true;
    [Tooltip("켜면 Animator 클립 길이를 자동으로 대기 시간에 사용합니다.")]
    [SerializeField] private bool useClipLengthForDuration = true;
    [SerializeField] private float animationDuration = 1f;

    private int searchTriggerHash;
    private int searchStateHash;

    public float AnimationDuration { get; private set; }

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        gameObject.layer = UiLayer;
        searchTriggerHash = Animator.StringToHash(searchTriggerName);
        searchStateHash = Animator.StringToHash(searchStateName);
        RefreshAnimationDuration();

        if (deactivateOnStop)
            gameObject.SetActive(false);
    }

    /// <summary>탐색 버튼 클릭 직후 SearchLens 오브젝트를 즉시 켭니다.</summary>
    public void PrepareForPlayback()
    {
        EnsureHierarchyActive();
        RefreshAnimationDuration();

        if (activateOnPlay)
            gameObject.SetActive(true);

        transform.SetAsLastSibling();

        if (animator != null)
            animator.enabled = true;
    }

    public void PlaySearchAnimation()
    {
        if (animator == null)
        {
            Debug.LogWarning("[BattleSearchLensPresenter] Animator가 연결되지 않았습니다.");
            return;
        }

        PrepareForPlayback();
        PlayAnimatorFromStart();
    }

    /// <summary>활성화 → Animator 준비 → 클립 길이만큼 대기 → 비활성화</summary>
    public IEnumerator RunSearchSequence()
    {
        float waitDuration = Mathf.Max(0.01f, animationDuration);

        if (animator == null)
            animator = GetComponent<Animator>();

        PrepareForPlayback();

        yield return null;
        yield return null;

        if (animator == null)
        {
            Debug.LogWarning("[BattleSearchLensPresenter] Animator가 연결되지 않았습니다.");
            yield return new WaitForSecondsRealtime(waitDuration);
            StopSearchAnimation();
            yield break;
        }

        RefreshAnimationDuration();
        waitDuration = Mathf.Max(waitDuration, AnimationDuration);
        PlayAnimatorFromStart();

        yield return null;
        if (!IsSearchStatePlaying())
            PlayAnimatorFromStart();

        yield return new WaitForSecondsRealtime(waitDuration);
        StopSearchAnimation();
    }

    public void StopSearchAnimation()
    {
        if (deactivateOnStop)
            gameObject.SetActive(false);
    }

    private void EnsureHierarchyActive()
    {
        gameObject.layer = UiLayer;

        Transform node = transform;
        while (node != null)
        {
            if (!node.gameObject.activeSelf)
                node.gameObject.SetActive(true);

            node = node.parent;
        }
    }

    private void PlayAnimatorFromStart()
    {
        if (animator == null)
            return;

        animator.enabled = true;
        animator.Rebind();
        animator.Update(0f);

        if (!string.IsNullOrEmpty(searchTriggerName) && HasTriggerParameter(searchTriggerName))
        {
            animator.ResetTrigger(searchTriggerHash);
            animator.SetTrigger(searchTriggerHash);
            return;
        }

        if (!string.IsNullOrEmpty(searchStateName))
            animator.Play(searchStateHash, 0, 0f);
    }

    private bool IsSearchStatePlaying()
    {
        if (animator == null || string.IsNullOrEmpty(searchStateName))
            return false;

        if (!string.IsNullOrEmpty(searchTriggerName) && HasTriggerParameter(searchTriggerName))
            return true;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        return state.shortNameHash == searchStateHash && state.normalizedTime < 0.99f;
    }

    private void RefreshAnimationDuration()
    {
        if (useClipLengthForDuration && TryGetAnimatorClipLength(out float clipLength))
        {
            AnimationDuration = clipLength;
            return;
        }

        AnimationDuration = Mathf.Max(0.01f, animationDuration);
    }

    private bool TryGetAnimatorClipLength(out float clipLength)
    {
        clipLength = 0f;
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null || clip.length <= 0f)
                continue;

            clipLength = clip.length;
            return true;
        }

        return false;
    }

    private bool HasTriggerParameter(string parameterName)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName))
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Trigger &&
                parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (animationDuration <= 0f)
            animationDuration = 1f;
    }
#endif
}
