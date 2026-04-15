using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// LED 전광판 스타일 텍스트 스크롤 — TMP_Text에 부착
    /// 텍스트가 영역을 초과하면 좌로 끊김 없이 무한 루프 스크롤
    /// 부모에 RectMask2D 자동 생성 (마스크 컨테이너)
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class TextMarquee : MonoBehaviour
    {
        TMP_Text _text;
        RectTransform _textRT;
        RectTransform _maskRT;
        GameObject _maskGO;

        /// <summary>루프 시 텍스트 간 여백 (px)</summary>
        float _gap;

        bool _initialized;
        bool _scrolling;
        bool _pendingPlay;

        float _speed;
        float _initialPause;
        float _maskWidth;
        float _textWidth;
        float _cycleLength; // textWidth + gap — 한 사이클 거리

        // 원본 텍스트 설정 저장
        TextWrappingModes _origWrapMode;
        TextOverflowModes _origOverflow;

        float _offset;
        float _pauseTimer;
        bool _pausing; // 최초 정지 상태

        /// <summary>TMP_Text에서 TextMarquee 가져오거나 추가</summary>
        public static TextMarquee GetOrAdd(TMP_Text text)
        {
            var m = text.GetComponent<TextMarquee>();
            if (m == null) m = text.gameObject.AddComponent<TextMarquee>();
            return m;
        }

        /// <summary>마스크 컨테이너 생성 및 텍스트 리페어런트 (최초 1회)</summary>
        void Init()
        {
            if (_initialized) return;
            _initialized = true;

            _text = GetComponent<TMP_Text>();
            _textRT = GetComponent<RectTransform>();

            _origWrapMode = _text.textWrappingMode;
            _origOverflow = _text.overflowMode;

            // 마스크 컨테이너 생성 — 텍스트의 원래 자리를 대신 차지
            var parent = _textRT.parent;
            int sibIndex = _textRT.GetSiblingIndex();

            _maskGO = new GameObject("_MarqueeMask", typeof(RectTransform), typeof(RectMask2D));
            _maskGO.layer = gameObject.layer;
            _maskRT = _maskGO.GetComponent<RectTransform>();
            _maskRT.SetParent(parent, false);
            _maskRT.SetSiblingIndex(sibIndex);

            // 텍스트의 원본 레이아웃을 마스크로 복사
            CopyRect(_textRT, _maskRT);

            // 텍스트를 마스크 자식으로 이동
            _textRT.SetParent(_maskRT, false);

            // 기본 상태: 마스크를 꽉 채움
            ResetTextToFill();
        }

        /// <summary>
        /// 스크롤 시작
        /// </summary>
        /// <param name="speed">스크롤 속도 (px/s)</param>
        /// <param name="initialPause">처음 텍스트 보여주고 대기 (초)</param>
        /// <param name="gap">루프 시 텍스트 꼬리~머리 간 여백 (px)</param>
        public void Play(float speed = 50f, float initialPause = 1.5f, float gap = 80f)
        {
            Init();
            Stop();
            _speed = speed;
            _initialPause = initialPause;
            _gap = gap;
            _pendingPlay = true;
            enabled = true;
        }

        /// <summary>스크롤 정지 및 위치 초기화</summary>
        public void Stop()
        {
            _scrolling = false;
            _pendingPlay = false;
            _pausing = false;

            if (!_initialized) return;

            ResetTextToFill();
            _text.textWrappingMode = _origWrapMode;
            _text.overflowMode = _origOverflow;
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

            // 최초 정지 구간 — 텍스트를 잠깐 보여준 뒤 스크롤 시작
            if (_pausing)
            {
                _pauseTimer += Time.unscaledDeltaTime;
                if (_pauseTimer >= _initialPause)
                    _pausing = false;
                return;
            }

            // 연속 좌 스크롤 + 심리스 랩
            _offset += _speed * Time.unscaledDeltaTime;

            // 한 사이클 완료 시 랩 (끊김 없음 — 연속 위치 유지)
            if (_offset >= _cycleLength)
                _offset -= _cycleLength;

            _textRT.anchoredPosition = new Vector2(-_offset, 0f);
        }

        /// <summary>측정 후 스크롤 시작 여부 결정</summary>
        void StartScroll()
        {
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            _text.overflowMode = TextOverflowModes.Overflow;
            _text.ForceMeshUpdate();

            _maskWidth = _maskRT.rect.width;
            _textWidth = _text.preferredWidth;

            if (_textWidth <= _maskWidth || _maskWidth <= 0f)
            {
                // 스크롤 불필요 — 원본 상태로 복원
                _scrolling = false;
                ResetTextToFill();
                _text.textWrappingMode = _origWrapMode;
                _text.overflowMode = _origOverflow;
                return;
            }

            // 사이클 거리 = 초과분 + 여백 + 마스크 폭 (완전히 빠져나간 뒤 오른쪽에서 재진입)
            _cycleLength = _textWidth + _gap;

            // 텍스트를 좌측 고정, 콘텐츠 너비에 맞춤
            _textRT.anchorMin = new Vector2(0f, 0f);
            _textRT.anchorMax = new Vector2(0f, 1f);
            _textRT.pivot = new Vector2(0f, 0.5f);
            // 폭을 cycleLength 두 배로 잡아 랩 시에도 텍스트가 보이도록
            // TMP는 실제 글리프만 렌더하므로 넓어도 문제 없음
            _textRT.sizeDelta = new Vector2(_textWidth, 0f);
            _textRT.anchoredPosition = Vector2.zero;

            _scrolling = true;
            _offset = 0f;
            _pauseTimer = 0f;
            _pausing = _initialPause > 0f;
        }

        /// <summary>텍스트를 마스크 크기에 맞게 리셋</summary>
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
            _scrolling = false;
            _pendingPlay = false;
            _pausing = false;
        }
    }
}
