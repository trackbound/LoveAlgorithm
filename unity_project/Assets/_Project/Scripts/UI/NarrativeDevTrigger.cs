using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // PlayScriptCommand
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// [DEV 스캐폴드] 내러티브 런타임 슬라이스1을 씬에서 손으로 구동하기 위한 임시 트리거. 버튼 클릭 시
    /// <see cref="PlayScriptCommand"/>를 발행해 NarrativeController를 깨운다(ScheduleSlot의 self-wire 패턴 미러).
    ///
    /// 왜 dev인가: "어떤 스케줄/이벤트에서 어떤 스크립트를 트는가"는 스토리 데이터(이벤트→스크립트 매핑)가
    /// 생긴 뒤의 결정이다. 그 전까지 플레이 루프(대사→선택지→효과→복귀)를 직접 보기 위한 최소 진입점.
    /// <see cref="demoScript"/>(엔진 포맷 CSV TextAsset)가 있으면 그것을, 없으면 <see cref="inlineDemoCsv"/>를 재생.
    /// 실제 트리거(이벤트 흐름)가 붙으면 이 컴포넌트는 제거한다.
    /// </summary>
    public class NarrativeDevTrigger : MonoBehaviour
    {
        [Tooltip("클릭 시 데모 스크립트를 재생하는 버튼. Awake에서 자가 배선.")]
        [SerializeField] Button button;
        [Tooltip("재생할 엔진 포맷 CSV(선택). 없으면 inlineDemoCsv 사용.")]
        [SerializeField] TextAsset demoScript;
        [Tooltip("demoScript 미지정 시 재생할 인라인 데모 CSV.")]
        [TextArea(4, 12)]
        [SerializeField] string inlineDemoCsv =
            "LineID,Type,Speaker,Value,Next\n" +
            ",Text,,데모 내러티브 시작.,click\n" +
            ",Text,로아,안녕! 잘 지냈어?,click\n" +
            ",Choice,,,>\n" +
            ",Option,,반갑게 인사한다|opt_a|Stat:Soc:2,>\n" +
            ",Option,,그냥 지나친다|opt_b|Money:100,>\n" +
            "opt_a,Text,로아,역시 너밖에 없어!,click\n" +
            ",Flow,,End,>\n" +
            "opt_b,Text,,어색하게 지나쳤다.,click\n";

        public Button Button { get => button; set => button = value; }
        public TextAsset DemoScript { get => demoScript; set => demoScript = value; }

        void Awake()
        {
            if (button != null) button.onClick.AddListener(Trigger);
        }

        /// <summary>데모 스크립트 재생을 요청한다. 버튼 onClick 또는 외부에서 직접 호출.</summary>
        public void Trigger()
        {
            if (demoScript != null)
                EventBus.Publish(new PlayScriptCommand(demoScript));
            else
                EventBus.Publish(new PlayScriptCommand(inlineDemoCsv, "dev_demo"));
        }

        void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(Trigger);
        }
    }
}
