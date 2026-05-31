namespace LoveAlgo.LockScreen
{
    /// <summary>
    /// 잠금화면 입력창 위 안내 문구 종류.
    /// </summary>
    public enum LockScreenHint
    {
        /// <summary>"앞으로 사용할 비밀번호를 입력해주세요. 최대 7자까지 입력 가능합니다."</summary>
        FirstSetup,

        /// <summary>"비밀번호 설정 완료!"</summary>
        Complete,

        /// <summary>"비밀번호를 입력해주세요."</summary>
        Normal,

        /// <summary>1회 오류 시 표시할 안내문.</summary>
        WrongOnce,

        /// <summary>2회 오류 시 표시할 안내문.</summary>
        WrongTwice,

        /// <summary>"비밀번호를 잊으셨다면 우측 하단 열쇠 모양 버튼을 눌러주세요." (3회 이상 오류 시)</summary>
        Forgot
    }
}
