using System.Collections;
using UnityEngine;

// 스크립트가 부착될 때 2D 이동, 애니메이션, 이미지 렌더링에 필요한 필수 컴포넌트들을 자동으로 함께 부착해 주는 속성
[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5.0f; // 필드 위에서의 플레이어 이동 속도

    [Header("전투씬창 UI 연결")]
    [SerializeField] private GameObject battleSceneUI;

    [Header("기본 UI(HUD) 연결")]
    [SerializeField] private GameObject mainHUD;

    [Header("필드 이동 사운드")]
    [SerializeField] private AudioClip walkingClip1;
    [SerializeField] private AudioClip walkingClip2;
    [SerializeField] private AudioClip walkingOxygenClip;
    [Tooltip("발소리 간격(초). 0이면 walking_1 길이의 절반을 사용합니다.")]
    [SerializeField] private float walkingStepInterval;
    [SerializeField] private AudioClip idleCoughClip1;
    [SerializeField] private AudioClip idleCoughClip2;
    [SerializeField] private AudioClip idleLoopClip;

    [Header("도망 후 재진입 방지")]
    [Tooltip("도망 직후 다시 전투에 들어가지 않도록 잠깐 막는 시간")]
    [SerializeField] private float postFleeGraceDuration = 0.75f;

    [Tooltip("전투가 끝난 뒤 이 거리 이상 벗어나야 다시 전투에 들어갈 수 있습니다.")]
    [SerializeField] private float postBattleReentryDistance = 1.0f;

    // 캐싱용 컴포넌트 변수들
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private AudioSource walkingSource;
    private AudioSource walkingOxygenSource;
    private AudioSource coughSource;
    private AudioSource idleSource;
    private bool isWalkingSoundActive;
    private bool wasFieldWalking;
    private bool playWalkingClip1Next = true;
    private float nextWalkingStepTime;
    private Coroutine postFleeGraceRoutine;
    private Coroutine coughRoutine;

    private const int IdleAnimatorState = 4;

    // 실시간 제어 상태 플래그 변수들
    private Vector2 movementInput; // 현재 프레임에서 입력된 방향 벡터 (X, Y)
    private bool isBattleEntryLocked; // 도망 직후 전투 진입 잠금 여부
    private bool hasEnteredBattle; // 현재 전투 모드 진입 여부
    private Vector2 lastBattleEndedPosition; // 마지막 전투가 종료되었을 때 플레이어의 위치
    private bool hasLastBattleEndedPosition; // 마지막 전투 종료 위치의 기록 여부
    private bool isGameManagerSubscribed; // GameManager 배틀 종료 이벤트 구독 상태 플래그
    
    // 전투 종료 직후 필드로 복귀할 때 물리(simulated) 복구가 필요한 상태인지 확인하는 와치독 제어 변수
    private bool isWaitingForPostBattlePhysicsRecovery;

    // 몬스터 ID 상수 매핑
    private const string MonsterIdSlime = "M-001";
    private const string MonsterIdFungus = "M-002";
    private const string MonsterIdFire = "M-003";

    private void Awake()
    {
        // 시작 시 핵심 컴포넌트들을 스크립트 메모리에 저장(캐싱)
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        ConfigureWalkingAudioSource();
    }

    private void OnEnable()
    {
        TrySubscribeGameManager();
        EnsurePhysicsSimulated();
    }

    private void Start()
    {
        TrySubscribeGameManager();
        EnsurePhysicsSimulated();
    }

    private void OnDisable()
    {
        if (postFleeGraceRoutine != null)
            StopCoroutine(postFleeGraceRoutine);

        postFleeGraceRoutine = null;
        CancelIdleCoughSequence();
        StopWalkingSound();
        StopIdleLoop();
        wasFieldWalking = false;
        UnsubscribeGameManager();
    }

    private void OnDestroy()
    {
        UnsubscribeGameManager();
    }

    // GameManager 싱글톤의 배틀 종료 이벤트를 안전하게 연결하는 예외 처리 메서드
    private void TrySubscribeGameManager()
    {
        if (isGameManagerSubscribed || GameManager.Instance == null)
            return;

        GameManager.Instance.OnBattleEnded -= HandleBattleEnded;
        GameManager.Instance.OnBattleEnded += HandleBattleEnded;
        isGameManagerSubscribed = true;
    }

    // GameManager 배틀 종료 이벤트와의 연결을 해제하는 예외 처리 메서드
    private void UnsubscribeGameManager()
    {
        if (!isGameManagerSubscribed || GameManager.Instance == null)
            return;

        GameManager.Instance.OnBattleEnded -= HandleBattleEnded;
        isGameManagerSubscribed = false;
    }

    private void Update()
    {
        if (!isGameManagerSubscribed)
            TrySubscribeGameManager();

        // 1단계: 배틀 도중이거나 필드 연출 등으로 조작이 잠긴 경우 예외 처리
        if (IsBattleActive() || IsFieldMovementFrozen())
        {
            movementInput = Vector2.zero; // 이동 입력을 0으로 초기화
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero; // 미끄러짐 방지를 위해 속도 초기화
                rb.angularVelocity = 0f;
                rb.simulated = false; // 다른 연출을 위해 일시적으로 물리 연산 차단
            }

            // 캐릭터가 강제로 멈추는 타이밍에 즉시 애니메이터를 4번(Idle) 상태로 변경
            if (animator != null) animator.SetInteger("State", IdleAnimatorState);

            wasFieldWalking = false;
            CancelIdleCoughSequence();
            StopWalkingSound();
            StopIdleLoop();
            return;
        }

        // --- 물리 복구 감시자 (Watchdog) ---
        // 배틀이 종료되고 필드로 돌아온 최초 시점에 물리 연산(simulated) 상태를 복구시킵니다.
        // 이를 통해 이벤트 도중 대화창이나 일시정지 창에서 물리가 강제로 켜지는 버그를 원천 차단합니다.
        if (isWaitingForPostBattlePhysicsRecovery && rb != null)
        {
            if (!rb.simulated)
            {
                Debug.LogWarning("[PlayerController] 물리 시뮬레이션 복구됨 (Watchdog).");
                rb.simulated = true;
            }
            isWaitingForPostBattlePhysicsRecovery = false;
        }

        // 키보드 입력을 받고 대각선 속도 뻥튀기를 막기 위해 정규화(normalized) 처리
        movementInput.x = Input.GetAxisRaw("Horizontal");
        movementInput.y = Input.GetAxisRaw("Vertical");
        movementInput = movementInput.normalized;

        // ==========================================
        // [애니메이션 상태 머신 4방향 제어 로직]
        // ==========================================
        if (animator != null)
        {
            // 이동 방향키가 하나라도 눌려 있는지 감지
            bool isMoving = movementInput.sqrMagnitude > 0;

            if (wasFieldWalking && !isMoving)
                PlayIdleCoughSounds();

            if (isMoving)
            {
                // 대각선 키 입력 시 절대값이 더 큰(조금 더 확실하게 누르고 있는) 방향을 우선 판정
                if (Mathf.Abs(movementInput.x) > Mathf.Abs(movementInput.y))
                {
                    // 수평(좌우) 이동이 강한 경우
                    if (movementInput.x < 0)
                        animator.SetInteger("State", 0); // walk_left (0) 재생
                    else
                        animator.SetInteger("State", 1); // walk_right (1) 재생
                }
                else
                {
                    // 수직(상하) 이동이 강한 경우
                    if (movementInput.y < 0)
                        animator.SetInteger("State", 3); // 아래로 걷기 ➔ walk_forward (3) 재생
                    else
                        animator.SetInteger("State", 2); // 위로 걷기 ➔ walk_back (2) 재생
                }
            }
            else
            {
                // 키를 모두 떼고 멈추면 대기 상태 번호(4) 지정 ➔ Any State의 'State == 4' 조건에 의해 즉시 Idle로 복귀
                animator.SetInteger("State", IdleAnimatorState);
            }

            UpdateWalkingSound(isMoving);
            UpdateIdleSound(isMoving);
            wasFieldWalking = isMoving;
        }
        // ==========================================
    }

    private void FixedUpdate()
    {
        if (IsFieldMovementFrozen() || IsBattleActive())
            return;

        // 물리 프레임 주기에 맞추어 플레이어 실제 이동 연산 수행
        if (rb != null && rb.simulated)
            rb.MovePosition(rb.position + movementInput * moveSpeed * Time.fixedDeltaTime);
    }

    /// <summary>게임오버, 맵 이동 등의 돌발 정지 타이밍에 즉시 조작과 물리를 멈춥니다.</summary>
    public void StopFieldMovementImmediate()
    {
        movementInput = Vector2.zero;

        // 즉시 정지 시 애니메이터에 대기(4) 지정
        if (animator != null)
            animator.SetInteger("State", IdleAnimatorState);

        wasFieldWalking = false;
        CancelIdleCoughSequence();
        StopWalkingSound();
        StopIdleLoop();

        if (rb == null)
            return;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = false;
    }

    // 도망친 직후의 잠금 코루틴을 호출하는 허브 메서드
    public void BeginPostFleeGraceWindow()
    {
        if (postFleeGraceRoutine != null)
            StopCoroutine(postFleeGraceRoutine);

        postFleeGraceRoutine = StartCoroutine(PostFleeGraceRoutine());
    }

    // 몬스터 트리거 영역(Collider)에 플레이어가 처음 부딪힐 때 호출되는 배틀 트리거 처리부
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!CanStartBattleFromCollision(other))
            return;

        string resolvedMonsterId = ResolveMonsterId(other);
        string colliderName = other != null && other.gameObject != null ? other.gameObject.name : "(null)";
        Debug.Log($"[PlayerController] 배틀 충돌 감지. collider='{colliderName}', resolvedMonsterId='{resolvedMonsterId ?? "null"}'");

        if (string.IsNullOrEmpty(resolvedMonsterId))
            Debug.LogWarning($"[PlayerController] 몬스터 ID 해석 실패: {other.gameObject.name}");

        ConfirmBattleEntryFromField(resolvedMonsterId);
    }

    /// <summary>몬스터 충돌 시 배틀 씬으로 전환합니다.</summary>
    public void ConfirmBattleEntryFromField(string resolvedMonsterId)
    {
        StartCoroutine(ConfirmBattleEntryFromFieldRoutine(resolvedMonsterId));
    }

    private IEnumerator ConfirmBattleEntryFromFieldRoutine(string resolvedMonsterId)
    {
        BattleEncounterContext.SetEncounteredMonsterId(resolvedMonsterId);
        hasEnteredBattle = true;

        AtlasManager.Instance?.PreloadMonsterBattleSpritesForId(resolvedMonsterId);

        if (battleSceneUI != null)
            UIBattleManager.PrepareFieldBattlePresentation(battleSceneUI, resolvedMonsterId);

        yield return null;
        yield return new WaitForEndOfFrame();

        if (GameManager.Instance != null)
            GameManager.Instance.EnterBattle();

        if (battleSceneUI != null)
            battleSceneUI.SetActive(true);
        else
            Debug.LogWarning("[PlayerController] battleSceneUI가 연결되지 않았습니다.");

        if (mainHUD != null)
            mainHUD.SetActive(false);
    }

    // 트리거 영역(Collider) 안에 플레이어가 계속 머물러 있는 동안 배틀 트리거를 예외 검사하는 부분
    private void OnTriggerStay2D(Collider2D other)
    {
        if (IsBattleActive() || IsFieldMovementFrozen())
            return;

        if (hasEnteredBattle && !IsBattleUiVisible())
            hasEnteredBattle = false;

        if (hasEnteredBattle || isBattleEntryLocked)
            return;

        if (!IsMonsterCollider(other))
            return;

        // 전투가 끝나고 플레이어가 움직이지 않았을 때, 연속 전투가 즉시 재실행되는 버그 방지(거리 연산 검사)
        if (hasLastBattleEndedPosition && rb != null)
        {
            float movedDistance = Vector2.Distance(rb.position, lastBattleEndedPosition);
            if (movedDistance < postBattleReentryDistance)
                return;
        }

        OnTriggerEnter2D(other);
    }

    // 충돌 상태를 기반으로 플레이어가 현재 배틀에 들어갈 자격이 되는지 정밀 검사
    private bool CanStartBattleFromCollision(Collider2D other)
    {
        if (IsBattleActive() || IsFieldMovementFrozen())
            return false;

        if (hasEnteredBattle && !IsBattleUiVisible())
            hasEnteredBattle = false;

        if (isBattleEntryLocked || hasEnteredBattle)
            return false;

        if (!IsMonsterCollider(other))
            return false;

        if (other == null || !other.isTrigger)
            return false;

        if (hasLastBattleEndedPosition && rb != null)
        {
            float movedDistance = Vector2.Distance(rb.position, lastBattleEndedPosition);
            if (movedDistance < postBattleReentryDistance)
                return false;
        }

        return true;
    }

    /// <summary>게임오버, 맵 재이동, 챕터 초기화 시 인카운터 제약 상태와 UI를 깔끔하게 리셋합니다.</summary>
    public void ResetFieldBattleEntryState()
    {
        if (postFleeGraceRoutine != null)
        {
            StopCoroutine(postFleeGraceRoutine);
            postFleeGraceRoutine = null;
        }

        hasEnteredBattle = false;
        isBattleEntryLocked = false;
        hasLastBattleEndedPosition = false;
        isWaitingForPostBattlePhysicsRecovery = false;
        EnsurePhysicsSimulated();

        if (battleSceneUI != null && battleSceneUI.activeSelf)
            battleSceneUI.SetActive(false);

        if (mainHUD != null && !mainHUD.activeSelf)
            mainHUD.SetActive(true);
    }

    // 씬 내부의 활성/비활성화 상태인 모든 플레이어 컨트롤러를 다 끌어와 한번에 리셋하는 정적 보완 함수
    public static void ResetAllFieldBattleEntryStates()
    {
        PlayerController[] controllers =
            FindObjectsByType<PlayerController>(FindObjectsInactive.Include);

        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null)
                controllers[i].ResetFieldBattleEntryState();
        }
    }

    // GameManager의 배틀 종료 통보 수신 시, 플레이어의 물리 복구 및 전투 정지 상태를 풀어주는 함수
    private void HandleBattleEnded()
    {
        hasEnteredBattle = false;
        EnsurePhysicsSimulated();

        if (rb != null)
        {
            lastBattleEndedPosition = rb.position; // 연속 전투 방지용 거리 검사의 기준이 되는 포인트 저장
            hasLastBattleEndedPosition = true;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            // 전투 종료 직후 물리 상태 와치독 작동을 승인하기 위해 플래그를 true로 변경
            isWaitingForPostBattlePhysicsRecovery = true;
        }
        else
        {
            lastBattleEndedPosition = transform.position;
            hasLastBattleEndedPosition = true;
        }

        if (battleSceneUI != null && battleSceneUI.activeSelf)
            battleSceneUI.SetActive(false);

        if (mainHUD != null && !mainHUD.activeSelf)
            mainHUD.SetActive(true);
    }

    // 도망친 직후 지정된 리얼타임 시간 동안 배틀 강제 잠금을 처리해 주는 비동기 코루틴
    private IEnumerator PostFleeGraceRoutine()
    {
        isBattleEntryLocked = true;
        yield return new WaitForSecondsRealtime(postFleeGraceDuration);
        isBattleEntryLocked = false;
        postFleeGraceRoutine = null;
    }

    private bool IsBattleActive()
    {
        if (GameManager.Instance != null)
            return GameManager.Instance.IsInBattle;

        return IsBattleUiVisible();
    }

    private static bool IsFieldMovementFrozen()
    {
        return GameManager.Instance != null && GameManager.Instance.IsFieldMovementFrozen;
    }

    private bool IsBattleUiVisible()
    {
        if (battleSceneUI != null && battleSceneUI.activeInHierarchy)
            return true;

        if (UIManager.Instance != null && UIManager.Instance.IsBattleUiVisible())
            return true;

        return false;
    }

    private void EnsurePhysicsSimulated()
    {
        if (rb == null)
            return;

        if (!rb.simulated)
        {
            rb.simulated = true;
            Debug.LogWarning("[PlayerController] Rigidbody2D.simulated 복구됨.");
        }
    }

    private void ConfigureWalkingAudioSource()
    {
        walkingSource = gameObject.AddComponent<AudioSource>();
        walkingSource.playOnAwake = false;
        walkingSource.loop = false;
        walkingSource.spatialBlend = 0f;

        walkingOxygenSource = gameObject.AddComponent<AudioSource>();
        walkingOxygenSource.playOnAwake = false;
        walkingOxygenSource.loop = true;
        walkingOxygenSource.spatialBlend = 0f;

        coughSource = gameObject.AddComponent<AudioSource>();
        coughSource.playOnAwake = false;
        coughSource.loop = false;
        coughSource.spatialBlend = 0f;

        idleSource = gameObject.AddComponent<AudioSource>();
        idleSource.playOnAwake = false;
        idleSource.loop = true;
        idleSource.spatialBlend = 0f;
    }

    private void PlayIdleCoughSounds()
    {
        StopIdleLoop();

        if (coughSource == null || (idleCoughClip1 == null && idleCoughClip2 == null))
            return;

        CancelIdleCoughSequence();
        coughRoutine = StartCoroutine(PlayIdleCoughSequence());
    }

    private IEnumerator PlayIdleCoughSequence()
    {
        if (idleCoughClip1 != null)
        {
            coughSource.PlayOneShot(idleCoughClip1);
            yield return new WaitForSeconds(idleCoughClip1.length);
        }

        if (idleCoughClip2 != null)
        {
            coughSource.PlayOneShot(idleCoughClip2);
            yield return new WaitForSeconds(idleCoughClip2.length);
        }

        coughRoutine = null;
    }

    private void UpdateIdleSound(bool isMoving)
    {
        if (isMoving || ShouldSuppressBreathingSounds())
        {
            StopIdleLoop();
            return;
        }

        if (coughRoutine != null)
            return;

        StartIdleLoop();
    }

    private static bool ShouldSuppressBreathingSounds()
    {
        return UILoading.IsLoadingScreenVisible || GameplayAudioGuard.IsBlocked;
    }

    private void StartIdleLoop()
    {
        if (ShouldSuppressBreathingSounds() || idleLoopClip == null || idleSource == null)
            return;

        idleSource.clip = idleLoopClip;
        if (!idleSource.isPlaying)
            idleSource.Play();
    }

    private void StopIdleLoop()
    {
        if (idleSource != null && idleSource.isPlaying)
            idleSource.Stop();
    }

    private void CancelIdleCoughSequence()
    {
        if (coughRoutine == null)
            return;

        StopCoroutine(coughRoutine);
        coughRoutine = null;
    }

    private void UpdateWalkingSound(bool isMoving)
    {
        if (!isMoving)
        {
            StopWalkingSound();
            return;
        }

        if (walkingClip1 == null || walkingClip2 == null || walkingSource == null)
            return;

        if (!isWalkingSoundActive)
        {
            isWalkingSoundActive = true;
            playWalkingClip1Next = true;
            if (!ShouldSuppressBreathingSounds())
                StartWalkingOxygenLoop();
            PlayNextWalkingStep();
            nextWalkingStepTime = Time.time + GetWalkingStepInterval();
            return;
        }

        if (ShouldSuppressBreathingSounds())
            StopWalkingOxygenLoop();
        else if (walkingOxygenSource != null && !walkingOxygenSource.isPlaying)
            StartWalkingOxygenLoop();

        if (Time.time < nextWalkingStepTime)
            return;

        PlayNextWalkingStep();
        nextWalkingStepTime = Time.time + GetWalkingStepInterval();
    }

    private float GetWalkingStepInterval()
    {
        if (walkingStepInterval > 0f)
            return walkingStepInterval;

        return walkingClip1 != null ? walkingClip1.length * 0.5f : 0.35f;
    }

    private void PlayNextWalkingStep()
    {
        AudioClip clip = playWalkingClip1Next ? walkingClip1 : walkingClip2;
        playWalkingClip1Next = !playWalkingClip1Next;
        walkingSource.PlayOneShot(clip);
    }

    private void StartWalkingOxygenLoop()
    {
        if (ShouldSuppressBreathingSounds() || walkingOxygenClip == null || walkingOxygenSource == null)
            return;

        walkingOxygenSource.clip = walkingOxygenClip;
        if (!walkingOxygenSource.isPlaying)
            walkingOxygenSource.Play();
    }

    private void StopWalkingOxygenLoop()
    {
        if (walkingOxygenSource != null && walkingOxygenSource.isPlaying)
            walkingOxygenSource.Stop();
    }

    private void StopWalkingSound()
    {
        isWalkingSoundActive = false;
        playWalkingClip1Next = true;
        nextWalkingStepTime = 0f;

        if (walkingSource != null)
            walkingSource.Stop();

        StopWalkingOxygenLoop();
    }

    // 충돌한 상대가 실제 몬스터가 맞는지 타겟 검사 및 오작동 보호 장치를 동작시키는 함수
    private bool IsMonsterCollider(Collider2D other)
    {
        if (other == null)
            return false;

        // [오작동 보호장치] "해독제", "소화기" 등 이름에 몬스터 식별용 단어가 포함되어 있더라도, 아이템 컴포넌트(ItemPickup)가 존재하면 절대 몬스터로 판단하지 않음
        if (other.GetComponent<ItemPickup>() != null)
            return false;

        try
        {
            if (other.CompareTag("Monster"))
                return true;
        }
        catch (UnityException)
        {
        }

        return TryResolveMonsterNameHint(other, out string _);
    }

    // 상대 객체 이름 텍스트 분석을 거쳐 사전에 선언된 고유 몬스터 ID를 해석 및 반환하는 메서드
    private string ResolveMonsterId(Collider2D other)
    {
        if (!TryResolveMonsterNameHint(other, out string objectName))
            return null;

        string lower = objectName.ToLowerInvariant();

        if (objectName.Contains("슬라임") || lower.Contains("slime") || lower.Contains("m001"))
            return MonsterIdSlime;

        if (objectName.Contains("곰팡") || lower.Contains("fungus") || lower.Contains("mold") || lower.Contains("m002"))
            return MonsterIdFungus;

        if (objectName.Contains("불") || lower.Contains("fire") || lower.Contains("m003"))
            return MonsterIdFire;

        return null;
    }

    // 다중 자식/부모 계층 관계 속에서 몬스터 객체 이름의 힌트를 상하로 정밀 탐색해 찾아내는 알고리즘
    private bool TryResolveMonsterNameHint(Collider2D other, out string monsterNameHint)
    {
        monsterNameHint = null;
        if (other == null)
            return false;

        if (TryGetMatchedMonsterName(other.gameObject, out monsterNameHint))
            return true;

        if (other.attachedRigidbody != null && TryGetMatchedMonsterName(other.attachedRigidbody.gameObject, out monsterNameHint))
            return true;

        Transform tr = other.transform;
        if (tr != null)
        {
            if (tr.parent != null && TryGetMatchedMonsterName(tr.parent.gameObject, out monsterNameHint))
                return true;

            Transform root = tr.root;
            if (root != null && TryGetMatchedMonsterName(root.gameObject, out monsterNameHint))
                return true;
        }

        return false;
    }

    // 특정 문자열 패턴에 매칭되는 몬스터 단어가 포함되어 있는지 이름 규칙을 확인하는 내부 보조 함수
    private bool TryGetMatchedMonsterName(GameObject candidate, out string matchedName)
    {
        matchedName = null;
        if (candidate == null)
            return false;

        string objectName = candidate.name;
        if (string.IsNullOrEmpty(objectName))
            return false;

        string lowerObjectName = objectName.ToLowerInvariant();
        bool isMonsterName = objectName.Contains("슬라임") || objectName.Contains("곰팡") || objectName.Contains("불") ||
                             lowerObjectName.Contains("slime") || lowerObjectName.Contains("m001") ||
                             lowerObjectName.Contains("fungus") || lowerObjectName.Contains("mold") || lowerObjectName.Contains("m002") ||
                             lowerObjectName.Contains("fire") || lowerObjectName.Contains("m003");

        if (!isMonsterName)
            return false;

        matchedName = objectName;
        return true;
    }
}