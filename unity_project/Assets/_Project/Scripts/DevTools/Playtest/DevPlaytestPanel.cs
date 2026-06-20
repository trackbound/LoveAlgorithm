#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using LoveAlgo.Common;          // EventBus
using LoveAlgo.Core;            // MetaProgressStore
using LoveAlgo.Events;          // ShowChoiceCommand, ShowModalCommand, ModalButtonKind
using LoveAlgo.Story.StoryEngine; // NarrativeController (엔딩 직행 점프)
using LoveAlgo.UI;              // DialogueView (터보 빠른진행)
using UnityEngine;
using UnityEngine.InputSystem;  // Keyboard (F8 토글)

namespace LoveAlgo.DevTools.Playtest
{
    /// <summary>
    /// 플레이테스트 헬퍼 오버레이(에디터/개발 빌드 전용). 중복 프롤로그(회차별 엔딩 1~4)를 반복 검증하려고
    /// "풀 플레이"를 자동화한다. 씬 배선 0 — <see cref="Bootstrap"/>가 플레이 진입 시 자가 생성(DevToastView 컨벤션).
    /// 릴리즈 빌드는 파일 전체가 <c>#if</c>로 제외된다.
    ///
    /// 기능 3종:
    /// 1) <b>터보(자동 진행)</b>: <see cref="DialogueView.ForceFastForward"/>로 대사를 시프트-홀드와 같은 경로로
    ///    즉시 진행하고, 선택지(<see cref="ShowChoiceCommand"/>)는 첫 옵션, 시스템 모달(<see cref="ShowModalCommand"/>)은
    ///    Yes 버튼을 자동 선택한다. 시작부 잠금화면/이름입력(텍스트 입력 포커스)에서는 시프트와 동일하게 멈춰
    ///    수동 입력을 허용한다(1회뿐). 단축키 F8.
    /// 2) <b>회차 카운터 즉시 설정</b>: <see cref="MetaProgressStore.PrologueClears"/>를 0~4로 직접 세팅 →
    ///    원하는 엔딩 분기를 바로 만들 수 있다(4회 반복 불필요).
    /// 3) <b>엔딩 직행 점프</b>: 재생 중인 프롤로그 커서를 <c>ending_1..4</c> 라벨로 점프(회차 If 분기 우회).
    ///
    /// 모든 발행/완료는 EventBus + 완료 핸들 경유라 게임 코드(피처)와 직접 결합하지 않는다(ADR-007).
    /// </summary>
    public class DevPlaytestPanel : MonoBehaviour
    {
        // 자가 생성 on/off. 당분간 안 쓰므로 꺼둠 — 다시 쓸 땐 true로만 바꾸면 좌상단 패널이 부활한다.
        const bool AutoSpawn = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoSpawn) return;
            var go = new GameObject("[DevPlaytestPanel]");
            go.AddComponent<DevPlaytestPanel>();
            DontDestroyOnLoad(go);
        }

        bool _turbo;
        bool _expanded = true;
        IDisposable _choiceSub;
        IDisposable _modalSub;
        const int WindowId = 0x10A1AB; // IMGUI 윈도우 고유 id(단일 인스턴스 — 고정 상수)
        Rect _area = new Rect(8, 8, 220, 0); // 높이는 GUILayout.Window가 내용에 맞춰 자동 산정

        void OnEnable()
        {
            // 터보 켜진 동안만 자동 응답(핸들러는 항상 구독하되 _turbo 게이트로 동작).
            _choiceSub = EventBus.Subscribe<ShowChoiceCommand>(OnChoice);
            _modalSub = EventBus.Subscribe<ShowModalCommand>(OnModal);
        }

        void OnDisable()
        {
            _choiceSub?.Dispose();
            _modalSub?.Dispose();
            DialogueView.ForceFastForward = false;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f8Key.wasPressedThisFrame) SetTurbo(!_turbo);
            // 터보 상태를 매 프레임 미러(다른 경로가 false로 돌려놔도 유지).
            DialogueView.ForceFastForward = _turbo;
        }

        void SetTurbo(bool on)
        {
            _turbo = on;
            DialogueView.ForceFastForward = on;
        }

        void OnChoice(ShowChoiceCommand cmd)
        {
            if (!_turbo || cmd.Handle == null) return;
            cmd.Handle.Select(0); // 첫 옵션 자동 선택
        }

        void OnModal(ShowModalCommand cmd)
        {
            if (!_turbo || cmd.Handle == null) return;
            cmd.Handle.Select(PickConfirmIndex(cmd.Buttons)); // Yes(확인) 자동
        }

        // 모달 버튼 중 긍정(Yes)을, 없으면 마지막 버튼(보통 확인/닫기)을 고른다.
        static int PickConfirmIndex(System.Collections.Generic.IReadOnlyList<ModalButton> buttons)
        {
            if (buttons == null || buttons.Count == 0) return 0;
            for (int i = 0; i < buttons.Count; i++)
                if (buttons[i].Kind == ModalButtonKind.Yes) return i;
            return buttons.Count - 1;
        }

        void OnGUI()
        {
            // GUILayout.Window는 내용에 맞춰 높이를 자동 산정한다(BeginArea는 안 됨 → 납작한 검은 바 버그).
            string title = _turbo ? "Playtest [TURBO]" : "Playtest";
            _area = GUILayout.Window(WindowId, _area, DrawWindow, title);
        }

        void DrawWindow(int id)
        {
            if (GUILayout.Button(_expanded ? "▼ 접기" : "▶ 펼치기")) _expanded = !_expanded;

            if (_expanded)
            {
                if (GUILayout.Button(_turbo ? "■ 터보 끄기 (F8)" : "▶ 터보 자동진행 (F8)"))
                    SetTurbo(!_turbo);

                GUILayout.Space(4);
                int clears = MetaProgressStore.GetInt(MetaProgressStore.PrologueClears);
                GUILayout.Label($"회차(prologueClears): {clears}");
                GUILayout.BeginHorizontal();
                for (int n = 0; n <= 4; n++)
                    if (GUILayout.Button(n.ToString())) MetaProgressStore.SetInt(MetaProgressStore.PrologueClears, n);
                GUILayout.EndHorizontal();

                GUILayout.Space(4);
                GUILayout.Label("엔딩 직행 점프");
                GUILayout.BeginHorizontal();
                for (int e = 1; e <= 4; e++)
                    if (GUILayout.Button($"E{e}")) JumpToEnding(e);
                GUILayout.EndHorizontal();
            }
            GUI.DragWindow(); // 제목 줄 드래그로 이동
        }

        static void JumpToEnding(int n)
        {
            var nc = FindAnyObjectByType<NarrativeController>();
            if (nc == null) { Log.Warn("[DevPlaytestPanel] NarrativeController 없음 — 프롤로그 재생 중에 점프하세요."); return; }
            nc.DevJumpToLabel($"ending_{n}");
        }
    }
}
#endif
