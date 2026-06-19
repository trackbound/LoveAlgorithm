using System;

namespace LoveAlgo.Events
{
    /// <summary>로아가 시각화되는 가상 기기. 오버레이 파일명 접두어(pc_/모바일_)를 결정.</summary>
    public enum RoaDevice { Pc, Mobile }

    /// <summary>
    /// 로아 디바이스 설정/전환 명령. 엔진이 CSV(Enter 디바이스 토큰 / RoaDevice 라인)를 해석해 발행하고,
    /// <c>RoaOverlayController</c>가 구독해 같은 감정 카테고리로 오버레이 디바이스만 교체한다(ADR-007).
    /// </summary>
    public readonly struct SetRoaDeviceCommand
    {
        public readonly RoaDevice Device;
        public SetRoaDeviceCommand(RoaDevice device) { Device = device; }
    }

    /// <summary>로아 디바이스 키워드 순수 파서(EditMode 테스트 가능). 키워드 2종 고정: pc / 모바일(mobile 허용).</summary>
    public static class RoaDeviceParse
    {
        public const string PcToken = "pc";
        public const string MobileToken = "모바일";

        public static bool TryParse(string token, out RoaDevice device)
        {
            device = RoaDevice.Pc;
            if (string.IsNullOrWhiteSpace(token)) return false;
            switch (token.Trim().ToLowerInvariant())
            {
                case "pc": device = RoaDevice.Pc; return true;
                case "모바일":
                case "mobile": device = RoaDevice.Mobile; return true;
                default: return false;
            }
        }

        public static string ToToken(RoaDevice device) => device == RoaDevice.Mobile ? MobileToken : PcToken;
    }
}
