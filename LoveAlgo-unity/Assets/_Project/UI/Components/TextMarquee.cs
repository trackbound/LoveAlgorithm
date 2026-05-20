using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 텍스트 마키 — TMP_Text에 부착해 영역을 초과하면 좌로 부드럽게 스크롤
    /// 부모에 RectMask2D 자동 생성. 시작/정지 시 ease-in/out으로 가속·감속해
    /// 끊김 없는 느낌을 준다 (호버 진입/이탈 UX에 적합).
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class TextMarquee : MonoBehaviour
    {
        TMP_Text _text;
        RectTransform _textRT;
        RectTransform _maskRT;
        GameObject _maskGO;

        TMP_Text _textClone;
        RectTransform _textCloneRT;

        float _gap;
        bool _initialized;
        bool _scrolling;
        bool _pendingPlay;

        float _maxSpeed;
        float _initialPause;
        float _accelTime;

        float _maskWidth;
        float _textWidth;
        float _cycleLength;

        TextWrappingModes _origWrapMode;
        TextOverflowModes _origOverflow;
        HorizontalAlignmentOptions _origHorizAlign;

        float _offset;
        float _currentSpeed;
        float _phaseTimer;

        enum Phase { Idle, InitialPause, Accel, Cruise, Decel }
        Phase _phase;

        public static TextMarquee GetOrAdd(TMP_Text text)
        {
            var m = text.GetComponent<TextMarquee>();
            if (m == null) m = text.gameObject.AddComponent<TextMarquee>();
            return m;
        }

        void Init()
        {
            if (_initialized) return;
            _initialized = true;

            _text = GetComponent<TMP_Text>();
            _textRT = GetComponent<RectTransform>();

            _origWrapMode = _text.textWrappingMode;
            _origOverflow = _text.overflowMode;
            _origHorizAlign = _text.horizontalAlignment;

            var parent = _textRT.parent;
            int sibIndex = _textRT.GetSiblingIndex();

            _maskGO = new GameObject("_MarqueeMask", typeof(RectTransform), typeof(RectMask2D));
            _maskGO.layer = gameObject.layer;
            _maskRT = _maskGO.GetComponent<RectTransform>();
            _maskRT.SetParent(parent, false);
            _maskRT.SetSiblingIndex(sibIndex);

            CopyRect(_textRT, _maskRT);
            _textRT.SetParent(_maskRT, false);

            var cloneGO = Instantiate(_text.gameObject, _maskRT);
            cloneGO.name = "_MarqueeClone";
            var dupMarquee = cloneGO.GetComponent<TextMarquee>();
            if (dupMarquee != null) Destroy(dupMarquee);
            _textClone = cloneGO.GetComponent<TMP_Text>();
            _textCloneRT = cloneGO.GetComponent<RectTransform>();
            cloneGO.SetActive(false);

            ResetTextToFill();
        }

        /// <summary>
        /// 스크롤 시작 — 초기 정지 → ease-in 가속 → 정속 순환 (호버 이탈 시 Stop()으로 감속)
        /// </summary>
        /// <param name="speed">정속 구간 속도 (px/s)</param>
        /// <param name="initialPause">시작 전 텍스트를 보여주는 대기 (초)</param>
        /// <param name="gap">루프 간 여백 (px, 텍스트 길이와 무관하게 일정)</param>
        /// <param name="accelTime">가속/감속에 걸리는 시간 (초)</param>
        public void Play(float speed = 40f, float initialPause = 0.7f, float gap = 48f, float accelTime = 0.35f)
        {
            Init();
            // 이미 재생 중이면 무시 — 호버 깜박임 시 재설정 방지
            if (_scrolling && (_phase == Phase.Accel || _phase == Phase.Cruise || _phase == Phase.InitialPause))
                return;

            _maxSpeed = speed;
            _initialPause = Mathf.Max(0f, initialPause);
            _gap = Mathf.Max(0f, gap);
            _accelTime = Mathf.Max(0.01f, accelTime);
            _pendingPlay = true;
            enabled = true;
        }

        /// <summary>
        /// 스크롤 정지 — 정속 또는 가속 중이면 부드럽게 감속 후 원위치, 그 외엔 즉시 초기화
        /// </summary>
        public void Stop()
        {
            if (!_initialized)
            {
                _pendingPlay = false;
                return;
            }

            if (_scrolling && (_phase == Phase.Accel || _phase == Phase.Cruise))
            {
                _phase = Phase.Decel;
                _phaseTimer = 0f;
                return;
            }

            InstantReset();
        }

        void InstantReset()
        {
            _phase = Phase.Idle;
            _scrolling = false;
            _pendingPlay = false;
            _offset = 0f;
            _currentSpeed = 0f;

            if (!_initialized) return;

            if (_textClone != null) _textClone.gameObject.SetActive(false);
            ResetTextToFill();
            _text.textWrappingMode = _origWrapMode;
            _text.overflowMode = _origOverflow;
            _text.horizontalAlignment = _origHorizAlign;
        }

        void Update()
        {
            if (_pendingPlay)
            {
                _pendingPlay = false;
                StartScroll();
                if (!_scrolling) return;
            }

            if (!_scrolling) return;

            float dt = Time.unscaledDeltaTime;
            _phaseTimer += dt;

            switch (_phase)
            {
                case Phase.InitialPause:
                    if (_phaseTimer >= _initialPause)
                    {
                        _phase = Phase.Accel;
                        _phaseTimer = 0f;
                    }
                    break;

                case Phase.Accel:
                {
                    float t = Mathf.Clamp01(_phaseTimer / _accelTime);
                    _currentSpeed = _maxSpeed * SmoothStep(t);
                    AdvanceOffset(dt);
                    if (t >= 1f)
                    {
                        _phase = Phase.Cruise;
                        _phaseTimer = 0f;
                    }
                    break;
                }

                case Phase.Cruise:
                    _currentSpeed = _maxSpeed;
                    AdvanceOffset(dt);
                    break;

                case Phase.Decel:
                {
                    float t = Mathf.Clamp01(_phaseTimer / _accelTime);
                    _currentSpeed = _maxSpeed * (1f - SmoothStep(t));
                    AdvanceOffset(dt);
                    if (t >= 1f)
                        InstantReset();
                    break;
                }
            }
        }

        void AdvanceOffset(float dt)
        {
            _offset += _currentSpeed * dt;
            if (_cycleLength > 0f && _offset >= _cycleLength)
                _offset -= _cycleLength;

            _textRT.anchoredPosition = new Vector2(-_offset, 0f);
            if (_textCloneRT != null)
                _textCloneRT.anchoredPosition = new Vector2(-_offset + _cycleLength, 0f);
        }

        static float SmoothStep(float t) => t * t * (3f - 2f * t);

        void StartScroll()
        {
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            _text.overflowMode = TextOverflowModes.Overflow;
            // 가운데/오른쪽 정렬이면 글리프가 rect 중앙/우측으로 모여 좌이동 마키가 안 보임 → Left 강제
            _text.horizontalAlignment = HorizontalAlignmentOptions.Left;
            _text.ForceMeshUpdate();

            _maskWidth = _maskRT.rect.width;
            _textWidth = _text.preferredWidth;

            if (_textWidth <= _maskWidth || _maskWidth <= 0f)
            {
                _scrolling = false;
                _phase = Phase.Idle;
                ResetTextToFill();
                _text.textWrappingMode = _origWrapMode;
                _text.overflowMode = _origOverflow;
                _text.horizontalAlignment = _origHorizAlign;
                return;
            }

            _cycleLength = _textWidth + _gap;

            _textRT.anchorMin = new Vector2(0f, 0f);
            _textRT.anchorMax = new Vector2(0f, 1f);
            _textRT.pivot = new Vector2(0f, 0.5f);
            _textRT.sizeDelta = new Vector2(_textWidth, 0f);
            _textRT.anchoredPosition = Vector2.zero;

            if (_textClone != null)
            {
                _textClone.text = _text.text;
                _textClone.font = _text.font;
                _textClone.fontSize = _text.fontSize;
                _textClone.color = _text.color;
                _textClone.alignment = _text.alignment;
                _textClone.textWrappingMode = TextWrappingModes.NoWrap;
                _textClone.overflowMode = TextOverflowModes.Overflow;
                _textClone.horizontalAlignment = HorizontalAlignmentOptions.Left;

                _textCloneRT.anchorMin = new Vector2(0f, 0f);
                _textCloneRT.anchorMax = new Vector2(0f, 1f);
                _textCloneRT.pivot = new Vector2(0f, 0.5f);
                _textCloneRT.sizeDelta = new Vector2(_textWidth, 0f);
                _textCloneRT.anchoredPosition = new Vector2(_cycleLength, 0f);
                _textClone.gameObject.SetActive(true);
            }

            _scrolling = true;
            _offset = 0f;
            _currentSpeed = 0f;
            _phaseTimer = 0f;
            _phase = _initialPause > 0f ? Phase.InitialPause : Phase.Accel;
        }

        void ResetTextToFill()
        {
            if (_textRT == null) return;
            _textRT.anchorMin = Vector2.zero;
            _textRT.anchorMax = Vector2.one;
            _textRT.offsetMin = Vector2.zero;
            _textRT.offsetMax = Vector2.zero;
            _textRT.anchoredPosition = Vector2.zero;
        }

        static void CopyRect(RectTransform src, RectTransform dst)
        {
            dst.anchorMin = src.anchorMin;
            dst.anchorMax = src.anchorMax;
            dst.sizeDelta = src.sizeDelta;
            dst.pivot = src.pivot;
            dst.offsetMin = src.offsetMin;
            dst.offsetMax = src.offsetMax;
            dst.anchoredPosition = src.anchoredPosition;
        }

        void OnDisable()
        {
            InstantReset();
        }
    }
}
