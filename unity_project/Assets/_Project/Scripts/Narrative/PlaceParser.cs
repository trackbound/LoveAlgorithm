namespace LoveAlgo.Story
{
    /// <summary>
    /// 위치 배너(Place) Value 순수 파서. EventBus·UnityEngine 비의존(EditMode 테스트). 형식 <c>제목 | 장소</c>
    /// (첫 '|' 기준 분리, 좌우 트림). '|'가 없으면 전체를 장소로 본다(제목 없음). 둘 다 비면 IsValid=false.
    /// 엔진(NarrativeController)이 결과를 받아 동결 수치(PlaceTuningSO)로 지속을 해석해 ShowPlaceCommand로 발행한다.
    /// </summary>
    public static class PlaceParser
    {
        public static PlaceIntent Parse(string value)
        {
            var r = new PlaceIntent();
            if (string.IsNullOrEmpty(value)) return r;

            int bar = value.IndexOf('|');
            if (bar >= 0)
            {
                r.Title = value.Substring(0, bar).Trim();
                r.Place = value.Substring(bar + 1).Trim();
            }
            else
            {
                r.Place = value.Trim();
            }

            r.IsValid = !string.IsNullOrEmpty(r.Title) || !string.IsNullOrEmpty(r.Place);
            return r;
        }
    }

    /// <summary>Place 분해 결과. Title(없으면 null/빈)·Place.</summary>
    public struct PlaceIntent
    {
        public bool IsValid;
        public string Title;
        public string Place;
    }
}
