using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Modules.Audio;
using LoveAlgo.UI;
using LoveAlgo.Core;
using LoveAlgo.Stage;

namespace LoveAlgo.Story.StoryEngine
{
    /// <summary>
    /// 정적 서비스 로케이터 — 스크립트 실행에 필요한 매니저 참조 제공
    /// </summary>
    public static class ExecutionDependencies
    {
        static DialogueUI _dialogueUI;
        static IStage _stage;
        static AudioManager _audio;

        public static DialogueUI DialogueUI
        {
            get
            {
                if (_dialogueUI == null)
                    _dialogueUI = UIManager.Instance?.DialogueUI;
                return _dialogueUI;
            }
        }

        public static IStage Stage
        {
            get
            {
                if (_stage == null)
                    _stage = StageModule.Instance;
                return _stage;
            }
        }

        public static AudioManager Audio
        {
            get
            {
                if (_audio == null)
                    _audio = AudioManager.Instance;
                return _audio;
            }
        }

        public static void Reset()
        {
            _dialogueUI = null;
            _stage = null;
            _audio = null;
        }
    }
}
