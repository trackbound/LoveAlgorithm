using System.Runtime.CompilerServices;

// 전환기 한정 가교: ScriptLine 등의 internal setter는 원래 내러티브 어셈블리 내부 전용 의도였고,
// 구 소비처(DevTools/ScenarioEditor 등)가 모놀리식 Assembly-CSharp에 함께 있어 접근하던 것을 유지한다.
// 해당 소비처가 새 구조로 이식/삭제되면 이 노출을 제거한다(금지선: 구 모놀리식 결합 점진 해소).
[assembly: InternalsVisibleTo("Assembly-CSharp")]
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
