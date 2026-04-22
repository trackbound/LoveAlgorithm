using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 눈 감기/뜨기 연출용 검은 바 (Top/Bottom).
    /// Stage 캔버스 하위에 배치되어 BG/캐릭터는 가리되 대화창(상위 캔버스)은 가리지 않음.
    /// 바인딩만 담당하고 트윈 로직은 ScreenFX에 위임.
    /// </summary>
    public class EyeMask : MonoBehaviour
    {
        [SerializeField] Image top;
        [SerializeField] Image bottom;

        public Image Top => top;
        public Image Bottom => bottom;
    }
}
