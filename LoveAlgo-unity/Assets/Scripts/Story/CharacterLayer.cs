using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 캐릭터 레이어 - 3개 슬롯(L/C/R) 관리
    /// </summary>
    public class CharacterLayer : MonoBehaviour
    {
        [Header("슬롯 바인딩")]
        [SerializeField] CharacterSlot slotL;
        [SerializeField] CharacterSlot slotC;
        [SerializeField] CharacterSlot slotR;

        [Header("데이터베이스")]
        [SerializeField] CharacterDatabase characterDatabase;

        Dictionary<SlotPosition, CharacterSlot> slots;

        void Awake()
        {
            slots = new Dictionary<SlotPosition, CharacterSlot>
            {
                { SlotPosition.L, slotL },
                { SlotPosition.C, slotC },
                { SlotPosition.R, slotR }
            };
        }

        /// <summary>
        /// Char 명령 실행
        /// Value 형식: 슬롯:액션:대상[:옵션]
        /// </summary>
        public async UniTask ExecuteAsync(string value, CancellationToken ct = default)
        {
            var parts = value.Split(':');
            if (parts.Length < 2)
            {
                Debug.LogWarning($"[CharacterLayer] 잘못된 형식: {value}");
                return;
            }

            // 슬롯 파싱
            if (!TryParseSlot(parts[0], out SlotPosition slotPos))
            {
                Debug.LogWarning($"[CharacterLayer] 알 수 없는 슬롯: {parts[0]}");
                return;
            }

            var slot = GetSlot(slotPos);
            if (slot == null)
            {
                Debug.LogWarning($"[CharacterLayer] 슬롯이 바인딩되지 않음: {slotPos}");
                return;
            }

            // 액션 파싱
            string action = parts[1];

            switch (action)
            {
                case "Enter":
                    // 형식: 슬롯:Enter:캐릭터[:표정]
                    if (parts.Length >= 3)
                    {
                        string character = parts[2];
                        string emote = parts.Length >= 4 ? parts[3] : "Default";
                        await slot.EnterAsync(character, emote, ct);
                    }
                    break;

                case "Emote":
                    // 형식: 슬롯:Emote:표정
                    if (parts.Length >= 3)
                    {
                        string emote = parts[2];
                        await slot.EmoteAsync(emote, ct);
                    }
                    break;

                case "Exit":
                    // 형식: 슬롯:Exit
                    await slot.ExitAsync(ct);
                    break;

                default:
                    Debug.LogWarning($"[CharacterLayer] 알 수 없는 액션: {action}");
                    break;
            }
        }

        /// <summary>
        /// 슬롯 가져오기
        /// </summary>
        public CharacterSlot GetSlot(SlotPosition position)
        {
            return slots.TryGetValue(position, out var slot) ? slot : null;
        }

        /// <summary>
        /// 모든 캐릭터 퇴장
        /// </summary>
        public async UniTask ExitAllAsync(CancellationToken ct = default)
        {
            var tasks = new List<UniTask>();

            foreach (var slot in slots.Values)
            {
                if (slot != null && !slot.IsEmpty)
                {
                    tasks.Add(slot.ExitAsync(ct));
                }
            }

            await UniTask.WhenAll(tasks);
            
            // AudioManager에 모든 캐릭터 퇴장 알림
            AudioManager.Instance?.OnAllCharactersExit();
        }

        /// <summary>
        /// 모든 슬롯 즉시 클리어
        /// </summary>
        public void ClearAll()
        {
            foreach (var slot in slots.Values)
            {
                slot?.Clear();
            }
        }

        /// <summary>
        /// Speaker 이름으로 캐릭터 자동 등장 (아직 없으면)
        /// </summary>
        public async UniTask AutoEnterBySpeakerAsync(string speaker, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(speaker)) return;
            
            // Speaker → CharacterID 매핑
            string characterId = SpeakerToCharacterId(speaker);
            if (string.IsNullOrEmpty(characterId)) return;
            
            // 이미 해당 캐릭터가 화면에 있는지 확인
            if (IsCharacterOnStage(characterId)) return;
            
            // 기본 슬롯(C)에 등장
            var slot = GetSlot(SlotPosition.C);
            if (slot != null)
            {
                await slot.EnterAsync(characterId, "Default", ct);
            }
        }
        
        /// <summary>
        /// 캐릭터가 화면에 있는지 확인
        /// </summary>
        public bool IsCharacterOnStage(string characterId)
        {
            foreach (var slot in slots.Values)
            {
                if (slot != null && !slot.IsEmpty && 
                    string.Equals(slot.CurrentCharacter, characterId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// Speaker 이름 → CharacterID 변환
        /// </summary>
        string SpeakerToCharacterId(string speaker)
        {
            // CharacterDatabase에서 매핑 조회
            if (characterDatabase != null)
            {
                string id = characterDatabase.SpeakerToCharacterId(speaker);
                if (!string.IsNullOrEmpty(id))
                    return id;
            }
            
            // CharacterDatabase에 매핑이 없으면 null 반환 (주인공, 나레이션, 엑스트라 등)
            return null;
        }

        /// <summary>
        /// 표정 변경 (인라인 태그용)
        /// </summary>
        public void ChangeEmote(string slotStr, string emote)
        {
            if (TryParseSlot(slotStr, out SlotPosition pos))
            {
                var slot = GetSlot(pos);
                if (slot != null && !slot.IsEmpty)
                {
                    _ = slot.EmoteAsync(emote);
                }
            }
        }

        /// <summary>
        /// 슬롯 문자열 파싱
        /// </summary>
        bool TryParseSlot(string str, out SlotPosition position)
        {
            position = SlotPosition.C;

            switch (str.ToUpper())
            {
                case "L":
                    position = SlotPosition.L;
                    return true;
                case "C":
                    position = SlotPosition.C;
                    return true;
                case "R":
                    position = SlotPosition.R;
                    return true;
                default:
                    return false;
            }
        }
    }
}
