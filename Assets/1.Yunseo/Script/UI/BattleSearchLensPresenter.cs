using UnityEngine;

/// <summary>
/// SearchLens 오브젝트에 붙여 탐색 연출을 제어합니다.
/// Animator/상태/트리거 이름은 인스펙터에서 본인 세팅에 맞게 지정하세요.
/// </summary>
[DisallowMultipleComponent]
public class BattleSearchLensPresenter : MonoBehaviour
{
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

        searchTriggerHash = Animator.StringToHash(searchTriggerName);
        searchStateHash = Animator.StringToHash(searchStateName);
        RefreshAnimationDuration();

        if (deactivateOnStop)
            gameObject.SetActive(false);
    }

    public void PlaySearchAnimation()
    {
        if (animator == null)
        {
            Debug.LogWarning("[BattleSearchLensPresenter] Animator가 연결되지 않았습니다.");
            return;
        }

        RefreshAnimationDuration();

        if (activateOnPlay)
            gameObject.SetActive(true);

        animator.Rebind();
        animator.Update(0f);

        if (!string.IsNullOrEmpty(searchTriggerName) && HasTriggerParameter(searchTriggerName))
        {
            animator.SetTrigger(searchTriggerHash);
            return;
        }

        if (!string.IsNullOrEmpty(searchStateName))
            animator.Play(searchStateHash, 0, 0f);
    }

    public void StopSearchAnimation()
    {
        if (deactivateOnStop)
            gameObject.SetActive(false);
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
