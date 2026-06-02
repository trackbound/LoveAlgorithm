namespace LoveAlgo.UI
{
    /// <summary>
    /// HUD 표시 문자열 포맷(순수 함수). 이벤트 데이터 → 표시 텍스트. UI/TMP/EventBus를 모른다 =
    /// EditMode에서 테스트 가능. <see cref="HudView"/>가 이 결과를 TMP에 밀어넣는다(ADR-007 표시/로직 분리).
    /// </summary>
    public static class HudFormat
    {
        public static string Day(int day) => $"Day {day}";

        /// <summary>소지금(long) 포맷: "₩1,234" / 음수 "-₩1,234". (MoneyFormat은 int 전용 레거시라 long 직접 포맷)</summary>
        public static string Money(long money)
        {
            long abs = money < 0 ? -money : money;
            return (money < 0 ? "-₩" : "₩") + abs.ToString("#,##0");
        }
        public static string Affinity(string heroineId, int score) => $"{heroineId} ♥ {score}";
        public static string Stat(string statId, int value) => $"{statId} {value}";
        public static string SaveStatus(bool success) => success ? "저장됨" : "저장 실패";
        public static string Bgm(string name) => string.IsNullOrEmpty(name) ? "♪ —" : $"♪ {name}";
    }
}
