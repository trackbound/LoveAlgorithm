using System.Collections.Generic;
using LoveAlgo.Common;            // EventBus
using LoveAlgo.Core;              // NarrativeFlowGate
using LoveAlgo.Events;            // PlayScriptCommand, ResetNarrativeViewsCommand
using LoveAlgo.Story.StoryEngine; // ScriptValidator

namespace LoveAlgo.Story
{
    /// <summary>
    /// 스토리 편집 도구의 UI 무관 코어(에디터창·빌드 런타임 패널 공유). StreamingAssets/Story CSV의 목록/로드/검증/
    /// 적용/저장 + 안전장치(흐름 게이트가 잠기면 Apply 거부, 적용 전 화면 정리). ScriptParser/ScriptValidator/
    /// StoryAssetLoader/EventBus 재사용 — 신규 런타임 코드 없음. UI(EditorWindow/UIDocument)는 이 컨트롤러에 위임.
    /// </summary>
    public sealed class StoryEditController
    {
        public const string PlayName = "story-live";
        public const string StopName = "story-stop";

        /// <summary>흐름-critical 구간(데이루프 전환 등)이 아니면 적용 가능 — 그 구간엔 데드락 방지로 차단(NarrativeFlowGate).</summary>
        public bool CanApply => !NarrativeFlowGate.IsLocked;

        string _baseline = "";

        /// <summary>마지막으로 로드/저장한 CSV 상대경로(없으면 null).</summary>
        public string CurrentPath { get; private set; }

        public List<string> ListStories() => StoryAssetLoader.List();

        /// <summary>CSV를 읽어 편집 기준선(dirty 비교 기준)으로 삼는다.</summary>
        public string Load(string relPath)
        {
            var text = StoryAssetLoader.Read(relPath) ?? "";
            _baseline = text;
            CurrentPath = relPath;
            return text;
        }

        /// <summary>저장 성공 시 기준선 갱신(실패면 false).</summary>
        public bool Save(string relPath, string csv)
        {
            if (!StoryAssetLoader.Write(relPath, csv)) return false;
            _baseline = csv;
            CurrentPath = relPath;
            return true;
        }

        /// <summary>편집 텍스트가 기준선과 다른가(미저장 여부).</summary>
        public bool IsDirty(string current) => (current ?? "") != _baseline;

        /// <summary>파싱+검증. (위반 목록, 라인 수). Strict는 일시 적용 후 원복(러닝 게임 파싱에 영향 없게).</summary>
        public (List<ScriptValidator.Violation> violations, int lineCount) Validate(string csv, bool strict)
        {
            bool prev = ScriptParser.Strict;
            ScriptParser.Strict = strict;
            var lines = ScriptParser.Parse(csv ?? "");
            var violations = ScriptValidator.Validate(lines);
            ScriptParser.Strict = prev;
            return (violations, lines.Count);
        }

        /// <summary>러닝 게임에 편집본 재생(화면 정리 후). 흐름 게이트가 잠겼거나 빈 CSV면 거부(false).</summary>
        public bool Apply(string csv, bool strict)
        {
            if (!CanApply || string.IsNullOrWhiteSpace(csv)) return false;
            EventBus.Publish(new ResetNarrativeViewsCommand()); // 잔여 연출 청소
            bool prev = ScriptParser.Strict;
            ScriptParser.Strict = strict;
            EventBus.Publish(new PlayScriptCommand(csv, PlayName));
            ScriptParser.Strict = prev;
            return true;
        }

        /// <summary>재생 중단(빈 명령) + 화면 정리. 흐름 게이트가 잠겼으면 거부(false).</summary>
        public bool Stop()
        {
            if (!CanApply) return false;
            EventBus.Publish(new ResetNarrativeViewsCommand());
            EventBus.Publish(new PlayScriptCommand("", StopName));
            return true;
        }
    }
}
