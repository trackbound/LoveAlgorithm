using System;
using System.Collections.Generic;
using UnityEngine;
using LoveAlgo;          // GameConstants
using LoveAlgo.Core;     // GameStateSO, DayLoop, JsonSaveStore
using LoveAlgo.Common;   // EventBus
using LoveAlgo.Events;   // 통지/명령 이벤트
using LoveAlgo.Affinity; // AffinityFormula
using LoveAlgo.Schedule; // ScheduleSelectedCommand, ScheduleType, ScheduleAppliedEvent

/// <summary>
/// 통합 테스트 씬 구동 드라이버(dev). 씬의 매니저들(GameManager/ScheduleController/SaveManager/
/// FlowCommandRouter)은 OnEnable에서 EventBus를 구독하므로, 이 드라이버는 같은 GameStateSO를 공유하며
/// EventBus 명령만 발행해 전 파이프라인을 구동·검증한다([INTEG] 로그 + PASS/FAIL).
/// </summary>
public class IntegrationDriver : MonoBehaviour
{
    [SerializeField] GameStateSO state;
    public GameStateSO State { get => state; set => state = value; }

    void Start()
    {
        const string P = "[INTEG]";
        if (state == null) { Debug.LogError($"{P} state(GameStateSO) 미바인딩"); return; }

        var subs = new List<IDisposable>();
        int statChanges = 0, scheduleApplied = 0, dayEnds = 0, saveCompletes = 0;
        bool dayChanged = false, saveOk = false, affinityChanged = false;
        int newDay = 0, affScore = 0; string affHid = null;

        subs.Add(EventBus.Subscribe<StatChangedEvent>(e => statChanges++));
        subs.Add(EventBus.Subscribe<ScheduleAppliedEvent>(e => scheduleApplied++));
        subs.Add(EventBus.Subscribe<DayEndRequestedEvent>(e => dayEnds++));
        subs.Add(EventBus.Subscribe<DayChangedEvent>(e => { dayChanged = true; newDay = e.NewDay; }));
        subs.Add(EventBus.Subscribe<SaveCompletedEvent>(e => { saveCompletes++; saveOk = e.Success; }));
        subs.Add(EventBus.Subscribe<AffinityChangedEvent>(e => { affinityChanged = true; affHid = e.HeroineId; affScore = e.NewScore; }));

        AffinityFormula.ResetToFallback();
        DayLoop.BeginRun(state);
        JsonSaveStore.Delete(JsonSaveStore.AutoSaveSlot);
        int apd = GameConstants.ActionsPerDay;
        Debug.Log($"{P} start Day={state.Day} Actions={state.RemainingActions} ActionsPerDay={apd} MaxDay={GameConstants.MaxDay}");

        // ── B: Flow → 호감도 (스탯 0 상태라 보너스 0 → 정확히 base 3) ──
        EventBus.Publish(new FlowCommandRequestedEvent("Affinity:EventChoice:HaYeEun:Event1:3"));
        bool b = affinityChanged && affHid == "HaYeEun" && affScore == 3;
        Debug.Log($"{P} B(Flow→호감도): changed={affinityChanged} {affHid}={affScore} -> {(b ? "PASS" : "FAIL")}");

        // ── A: 스케줄 소진 → 하루전환(GameManager) → 오토세이브(SaveManager) ──
        for (int i = 0; i < apd; i++)
            EventBus.Publish(new ScheduleSelectedCommand(ScheduleType.Exercise_A));
        bool a = dayChanged && newDay == 2 && state.Day == 2 && dayEnds == 1
                 && scheduleApplied == apd && statChanges > 0
                 && saveCompletes == 1 && saveOk && JsonSaveStore.Exists(JsonSaveStore.AutoSaveSlot);
        Debug.Log($"{P} A(스케줄→전환→세이브): Day={state.Day} statCh={statChanges} applied={scheduleApplied} dayEnd={dayEnds} save={saveCompletes}/{saveOk} fileExists={JsonSaveStore.Exists(JsonSaveStore.AutoSaveSlot)} -> {(a ? "PASS" : "FAIL")}");

        // ── C: Flow Day 강제 설정 ──
        EventBus.Publish(new FlowCommandRequestedEvent("Day:7"));
        bool c = newDay == 7 && state.Day == 7;
        Debug.Log($"{P} C(Flow Day:7): Day={state.Day} newDay={newDay} -> {(c ? "PASS" : "FAIL")}");

        Debug.Log($"{P} OVERALL {((a && b && c) ? "PASS" : "FAIL")}");

        foreach (var s in subs) s.Dispose();
        JsonSaveStore.Delete(JsonSaveStore.AutoSaveSlot);
    }
}
