using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

namespace SLA.Editor
{
    public class MonsterAnimatorSetup : EditorWindow
    {
        [MenuItem("Tools/Safety Guardians/Setup Monster Animators")]
        public static void SetupAnimators()
        {
            string[] controllerPaths = new string[]
            {
                "Assets/2.SLA/Animations/Monster_M001_Slime.controller",
                "Assets/2.SLA/Animations/Monster_M002_Mold.controller",
                "Assets/2.SLA/Animations/Monster_M003_Fire.controller"
            };

            int successCount = 0;

            foreach (string path in controllerPaths)
            {
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller == null)
                {
                    Debug.LogWarning($"[MonsterAnimatorSetup] {path}를 찾을 수 없습니다.");
                    continue;
                }

                SetupSingleController(controller);
                successCount++;
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("애니메이터 세팅 완료", $"{successCount}개의 몬스터 애니메이터 컨트롤러 세팅을 성공적으로 마쳤습니다!", "확인");
        }

        private static void SetupSingleController(AnimatorController controller)
        {
            // 1. IsMoving 파라미터 추가 (없을 경우)
            string paramName = "IsMoving";
            bool hasParam = false;
            foreach (var param in controller.parameters)
            {
                if (param.name == paramName)
                {
                    hasParam = true;
                    break;
                }
            }

            if (!hasParam)
            {
                controller.AddParameter(paramName, AnimatorControllerParameterType.Bool);
            }

            // 2. Base Layer 상태 머신 가져오기
            var layer = controller.layers[0];
            var stateMachine = layer.stateMachine;

            AnimatorState idleState = null;
            AnimatorState moveState = null;

            foreach (var childState in stateMachine.states)
            {
                string stateName = childState.state.name;
                if (stateName.Contains("Idle"))
                {
                    idleState = childState.state;
                }
                else if (stateName.Contains("Move"))
                {
                    moveState = childState.state;
                }
            }

            if (idleState == null || moveState == null)
            {
                Debug.LogError($"[MonsterAnimatorSetup] {controller.name}에서 Idle 또는 Move 상태를 찾을 수 없습니다.");
                return;
            }

            // 3. 기존 두 상태 간의 트랜지션 정리 (중복 생성 방지)
            RemoveTransitionsBetween(idleState, moveState);
            RemoveTransitionsBetween(moveState, idleState);

            // 4. Idle -> Move 트랜지션 생성
            var idleToMove = idleState.AddTransition(moveState);
            idleToMove.hasExitTime = false;
            idleToMove.duration = 0f;
            idleToMove.AddCondition(AnimatorConditionMode.If, 0, paramName); // IsMoving == true

            // 5. Move -> Idle 트랜지션 생성
            var moveToIdle = moveState.AddTransition(idleState);
            moveToIdle.hasExitTime = false;
            moveToIdle.duration = 0f;
            moveToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, paramName); // IsMoving == false

            EditorUtility.SetDirty(controller);
        }

        private static void RemoveTransitionsBetween(AnimatorState fromState, AnimatorState toState)
        {
            var transitions = fromState.transitions;
            var list = new List<AnimatorStateTransition>(transitions);
            
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].destinationState == toState)
                {
                    fromState.RemoveTransition(list[i]);
                }
            }
        }
    }
}
