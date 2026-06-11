using LoveAlgo.Core; // JsonSaveStore.AutoSaveSlot

namespace LoveAlgo.Game
{
    /// <summary>게임 씬 부팅 모드.</summary>
    public enum BootMode
    {
        /// <summary>새 게임 — 상태 리셋 + 1일차 시작.</summary>
        NewGame,
        /// <summary>이어하기 — 오토세이브 복원.</summary>
        Continue
    }

    /// <summary>
    /// 씬 전환 1회성 부팅 의도 홀더. 타이틀 씬(<c>SceneFlowController</c>)이 게임 씬 로드 직전에 설정하고,
    /// 게임 씬(<c>GameBootstrap</c>)이 부팅 시 <see cref="Consume"/>로 읽고 기본값(NewGame)으로 되돌린다.
    /// 씬 로드는 인자를 못 넘기므로 static을 1회성 의도 전달에만 쓴다 — 지속 상태가 아니다(소비 즉시 리셋,
    /// 세이브에도 안 실린다). 기본값 NewGame이라 게임 씬을 직접 플레이하면 새 게임으로 부팅된다.
    /// </summary>
    public static class GameEntry
    {
        public static BootMode PendingMode = BootMode.NewGame;

        /// <summary>Continue 시 로드할 슬롯(기본=자동저장 슬롯0). LoadGameCommand가 특정 슬롯으로 설정.</summary>
        public static int SelectedSlot = JsonSaveStore.AutoSaveSlot;

        /// <summary>현재 모드를 반환하고 기본값(NewGame·자동저장 슬롯)으로 리셋한다(1회성 소비).</summary>
        public static BootMode Consume()
        {
            var mode = PendingMode;
            PendingMode = BootMode.NewGame;
            SelectedSlot = JsonSaveStore.AutoSaveSlot;
            return mode;
        }
    }
}
