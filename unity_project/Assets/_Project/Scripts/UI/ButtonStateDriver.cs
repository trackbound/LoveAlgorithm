using System;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 버튼 상태 비주얼 통합 드라이버(StyledButton·ButtonSpriteSwap·TitleHighlightSwitcher 수렴 대상).
    /// 배경은 상태별 자식을 정확히 하나 SetActive(child-swap), 라벨은 단일 TMP 색 코드 구동,
    /// pressed는 활성 자식 Image에 틴트 곱, UI 사운드도 발행. raw 포인터 이벤트 구동(Selectable 미상속 → 포커스 가림 부재).
    ///
    /// 이 파일의 Task 1 단계는 순수 결정층(정적)만 — 인스턴스 어댑터는 Task 2에서 채운다.
    /// </summary>
    public class ButtonStateDriver : MonoBehaviour
    {
        /// <summary>비주얼 상태. 우선순위 Disabled &gt; On &gt; Hover &gt; Normal.</summary>
        public enum State { Normal, Hover, On, Disabled }

        /// <summary>호버/클릭이 UiSound 테이블의 어느 항목을 쓸지.</summary>
        public enum UiSoundRole { General, Choice, Silent }

        /// <summary>상태별 라벨(TMP) 색. drive=false면 라벨 색 미관여. 상태 4종과 1:1.</summary>
        [Serializable]
        public struct TextColorBlock
        {
            [Tooltip("켜면 상태별로 라벨 색을 구동. 끄면 라벨 색 미관여.")]
            public bool drive;
            public Color normal;   // OFF/기본
            public Color hover;
            public Color on;       // 토글 ON
            public Color disabled;

            public static TextColorBlock Default => new TextColorBlock
            {
                drive = false,
                normal = Color.black,
                hover = Color.white,
                on = Color.white,
                disabled = new Color(0.5f, 0.5f, 0.5f, 1f),
            };
        }

        // ── 순수 결정층 (GameObject 불필요 — EditMode 테스트 대상) ──────────────────────

        /// <summary>활성 상태(우선순위 Disabled &gt; On &gt; Hover &gt; Normal).</summary>
        public static State ResolveActiveState(bool interactable, bool isOn, bool pointerInside)
        {
            if (!interactable) return State.Disabled;
            if (isOn) return State.On;
            if (pointerInside) return State.Hover;
            return State.Normal;
        }

        /// <summary>눌림(interactable &amp;&amp; pressed)일 때 baseColor*pressedTint(어두워짐), 아니면 baseColor 유지.</summary>
        public static Color ResolvePressedTint(bool interactable, bool pressed, Color baseColor, Color pressedTint)
            => (interactable && pressed) ? baseColor * pressedTint : baseColor;

        /// <summary>상태별 라벨 색(drive 판단은 호출 측 책임).</summary>
        public static Color ResolveTextColor(State state, in TextColorBlock c)
        {
            switch (state)
            {
                case State.Hover: return c.hover;
                case State.On: return c.on;
                case State.Disabled: return c.disabled;
                default: return c.normal; // Normal
            }
        }

        /// <summary>역할+호버/클릭 → SFX 이름(table/항목 없으면 null). StyledButton.ResolveSfx 이식.</summary>
        public static string ResolveSfx(UiSoundRole role, bool hover, UiSoundSO table)
        {
            if (table == null) return null;
            switch (role)
            {
                case UiSoundRole.Silent: return null;
                case UiSoundRole.Choice: return hover ? table.ChoiceHover : table.ChoiceClick;
                default:                 return hover ? table.ButtonHover : table.ButtonClick;
            }
        }
    }
}
