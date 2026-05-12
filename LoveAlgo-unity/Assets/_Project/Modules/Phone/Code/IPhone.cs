namespace LoveAlgo.Phone
{
    /// <summary>
    /// 메신저(폰) 모듈 외부 계약.
    /// 구현: <see cref="PhoneModule"/>.
    /// </summary>
    public interface IPhone
    {
        /// <summary>특정 히로인과의 채팅방 열기.</summary>
        void OpenChat(string heroineId);

        /// <summary>폰 UI 닫기.</summary>
        void Close();

        /// <summary>현재 폰 UI가 열려있는지.</summary>
        bool IsOpen { get; }
    }
}
