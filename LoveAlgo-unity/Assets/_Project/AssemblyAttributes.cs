using System.Runtime.CompilerServices;

// A3에서 GameManager.CurrentDay/RemainingActions 등 setter를 internal로 좁혔지만,
// Assets/Scripts/Editor/ 폴더의 DebugRemoteWindow처럼 Assembly-CSharp-Editor에서
// 디버그용으로 그 값들을 만질 필요가 있어 — Editor 어셈블리 전용으로 internal 공개.
// 이 파일은 Assembly-CSharp에 속하므로 여기 attribute를 두면 충분.
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor-firstpass")]
