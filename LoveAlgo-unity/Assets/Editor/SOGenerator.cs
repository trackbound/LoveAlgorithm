using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LoveAlgo.Shop;
using LoveAlgo.Schedule;
using LoveAlgo.Core;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// SO 에셋 자동 생성 — 하드코딩 기본값으로 채움
    /// 메뉴: LoveAlgo > SO 생성
    /// </summary>
    public static class SOGenerator
    {
        const string DataRoot = "Assets/Resources/Data";

        [MenuItem("LoveAlgo/SO 생성/전체 (3개 모두)")]
        static void GenerateAll()
        {
            GenerateItemCatalog();
            GenerateScheduleData();
            GenerateGameBalance();
            Debug.Log("[SOGenerator] 전체 SO 생성 완료!");
        }

        // ────────────────────────────────────────
        // ItemCatalog
        // ────────────────────────────────────────
        [MenuItem("LoveAlgo/SO 생성/ItemCatalog")]
        static void GenerateItemCatalog()
        {
            var so = ScriptableObject.CreateInstance<ItemCatalogSO>();
            var list = new List<ItemData>
            {
                // ── 히로인 선물 — 하예은 ──
                new("gift_yeun_towel","스포츠 타월","하예은이 좋아할 스포츠용 타월.\n\"운동할 때 쓸게!\"",8000,ItemCategory.Gift,"Yeun",iconPath:"Items/turtleman",tier:GiftTier.Low),
                new("gift_yeun_shoes","러닝화","가볍고 튼튼한 러닝화.\n\"와 이거 진짜 좋다!\"",45000,ItemCategory.Gift,"Yeun",iconPath:"Items/ankle support",tier:GiftTier.High,availability:ItemAvailability.AfterEvent2Start),
                new("gift_yeun_watch","스포츠 시계","방수 기능이 있는 스포츠 시계.\n\"매일 차고 다닐게!\"",85000,ItemCategory.Gift,"Yeun",iconPath:"Items/dining ticket",tier:GiftTier.Premium,availability:ItemAvailability.AfterEvent3Start),
                // ── 히로인 선물 — 서다은 ──
                new("gift_daeun_pen","고급 볼펜","서다은이 좋아할 만년필.\n\"...괜찮은 선물이네.\"",10000,ItemCategory.Gift,"Daeun",iconPath:"Items/3clolor pen",tier:GiftTier.Low),
                new("gift_daeun_book","전공 서적","희귀한 전공 원서.\n\"이거... 구하기 어려운 건데.\"",35000,ItemCategory.Gift,"Daeun",iconPath:"Items/plant",tier:GiftTier.Mid,availability:ItemAvailability.AfterEvent2Start),
                new("gift_daeun_tablet","태블릿 펜","고급 디지털 필기 펜.\n\"필기감이 정말 좋아.\"",80000,ItemCategory.Gift,"Daeun",iconPath:"Items/monitor",tier:GiftTier.Premium,availability:ItemAvailability.AfterEvent3Start),
                // ── 히로인 선물 — 이봄 ──
                new("gift_bom_sticker","스티커 팩","이봄이 좋아할 캐릭터 스티커.\n\"이거 진짜 귀여워~!\"",5000,ItemCategory.Gift,"Bom",iconPath:"Items/rabbit_keyring",tier:GiftTier.Low),
                new("gift_bom_acc","헤어 액세서리","트렌디한 머리핀 세트.\n\"오늘부터 바로 해볼래!\"",25000,ItemCategory.Gift,"Bom",iconPath:"Items/hairband",tier:GiftTier.Mid,availability:ItemAvailability.AfterEvent2Start),
                new("gift_bom_bag","미니 크로스백","봄이 스타일에 딱 맞는 가방.\n\"대박... 이거 완전 내 스타일!\"",65000,ItemCategory.Gift,"Bom",iconPath:"Items/handcream",tier:GiftTier.High,availability:ItemAvailability.AfterEvent3Start),
                // ── 히로인 선물 — 도희원 ──
                new("gift_heewon_novel","문고본 소설","도희원이 좋아할 소설책.\n\"...읽어볼게.\"",8000,ItemCategory.Gift,"Heewon",iconPath:"Items/popup ticket",tier:GiftTier.Low),
                new("gift_heewon_tea","프리미엄 티 세트","희귀한 블렌딩 차 세트.\n\"...향이 좋아.\"",40000,ItemCategory.Gift,"Heewon",iconPath:"Items/empire diamond",tier:GiftTier.High,availability:ItemAvailability.AfterEvent2Start),
                new("gift_heewon_music","한정판 LP","클래식 한정판 레코드.\n\"이걸... 어떻게 구한 거야?\"",90000,ItemCategory.Gift,"Heewon",iconPath:"Items/CD_tropical glow",tier:GiftTier.Premium,availability:ItemAvailability.AfterEvent3Start),
                // ── 히로인 선물 — 로아 ──
                new("gift_roa_light","LED 조명","로아가 좋아할 방송용 조명.\n\"방송 퀄리티 업! 고마워!\"",10000,ItemCategory.Gift,"Roa",iconPath:"Items/lamp",tier:GiftTier.Low),
                new("gift_roa_mic","콘덴서 마이크","방송용 고급 마이크.\n\"소리 진짜 깨끗하다!\"",50000,ItemCategory.Gift,"Roa",iconPath:"Items/headphone",tier:GiftTier.High,availability:ItemAvailability.AfterEvent2Start),
                new("gift_roa_cam","웹캠 프로","4K 방송용 웹캠.\n\"이거면 화질 미쳤다!\"",95000,ItemCategory.Gift,"Roa",iconPath:"Items/monitor",tier:GiftTier.Premium,availability:ItemAvailability.AfterEvent3Start),
                // ── 피로 회복 소모품 ──
                new("consume_energy_drink","에너지 음료","졸릴 때 마시면 좋은 에너지 음료.\n피로가 조금 줄어든다.",3000,ItemCategory.Consumable,effectValue:5,iconPath:"Items/energybar"),
                new("consume_energy_bar","에너지바","간편하게 먹을 수 있는 에너지바.\n피로를 적당히 줄여준다.",5000,ItemCategory.Consumable,effectValue:6,iconPath:"Items/energybar"),
                new("consume_vitamin","비타민 음료","비타민이 풍부한 건강 음료.\n피로 회복에 효과적이다.",8000,ItemCategory.Consumable,effectValue:8,iconPath:"Items/vitamin"),
                new("consume_arginine","아르기닌 젤리","고함량 아르기닌 젤리.\n피로가 크게 줄어든다.",15000,ItemCategory.Consumable,effectValue:12,iconPath:"Items/jelly"),
                new("consume_mood_lamp","무드등 (아로마)","아로마 무드등. 은은한 향으로\n피로를 확실히 풀어준다.",25000,ItemCategory.Consumable,effectValue:15,iconPath:"Items/lamp"),
                // ── 세션 버프 — 체력 ──
                new("buff_protein_choco","프로틴 초코바","운동 전에 먹으면 효과적인 초코바.\n자유행동 1회 체력 효과 +2",8000,ItemCategory.SessionBuff,effectValue:2,iconPath:"Items/marron bread",effectStat:"Str",duplicateTag:"buff_str"),
                new("buff_protein_straw","프로틴 딸기바","딸기맛 프로틴 바. 맛도 좋고 효과도 좋다.\n자유행동 1회 체력 효과 +3",12000,ItemCategory.SessionBuff,effectValue:3,iconPath:"Items/marron bread",effectStat:"Str",duplicateTag:"buff_str"),
                // ── 세션 버프 — 지성 ──
                new("buff_note","정리 노트","깔끔한 줄 노트. 공부 효율이 오른다.\n자유행동 1회 지성 효과 +1",3000,ItemCategory.SessionBuff,effectValue:1,iconPath:"Items/popup ticket",effectStat:"Int",duplicateTag:"buff_int"),
                new("buff_pen","3색 볼펜","색깔별 필기가 가능한 볼펜.\n자유행동 1회 지성 효과 +1",3000,ItemCategory.SessionBuff,effectValue:1,iconPath:"Items/3clolor pen",effectStat:"Int",duplicateTag:"buff_int"),
                new("buff_postit","포스트잇 세트","형형색색 포스트잇. 정리에 딱이다.\n자유행동 1회 지성 효과 +1",4000,ItemCategory.SessionBuff,effectValue:1,effectStat:"Int",duplicateTag:"buff_int"),
                new("buff_highlighter","형광펜 세트","5색 형광펜 세트. 중요한 건 표시!\n자유행동 1회 지성 효과 +2",6000,ItemCategory.SessionBuff,effectValue:2,iconPath:"Items/3clolor pen",effectStat:"Int",duplicateTag:"buff_int"),
                new("buff_mech_pencil","고급 샤프","0.3mm 정밀 샤프. 필기감이 좋다.\n자유행동 1회 지성 효과 +1",5000,ItemCategory.SessionBuff,effectValue:1,iconPath:"Items/3clolor pen",effectStat:"Int",duplicateTag:"buff_int"),
                new("buff_keyboard","기계식 키보드","청축 기계식 키보드. 타건감 최고.\n자유행동 1회 지성 효과 +2",35000,ItemCategory.SessionBuff,effectValue:2,effectStat:"Int",duplicateTag:"buff_int"),
                new("buff_mouse","게이밍 마우스","인체공학 게이밍 마우스.\n자유행동 1회 지성 효과 +1",25000,ItemCategory.SessionBuff,effectValue:1,effectStat:"Int",duplicateTag:"buff_int"),
                // ── 세션 버프 — 끈기 ──
                new("buff_vertical_mouse","버티컬 마우스","손목이 편한 버티컬 마우스.\n자유행동 1회 끈기 효과 +1",20000,ItemCategory.SessionBuff,effectValue:1,effectStat:"Per",duplicateTag:"buff_per"),
            };

            var serialized = new SerializedObject(so);
            var itemsProp = serialized.FindProperty("items");
            itemsProp.ClearArray();
            foreach (var item in list)
            {
                itemsProp.InsertArrayElementAtIndex(itemsProp.arraySize);
                var elem = itemsProp.GetArrayElementAtIndex(itemsProp.arraySize - 1);
                elem.FindPropertyRelative("Id").stringValue = item.Id;
                elem.FindPropertyRelative("Name").stringValue = item.Name;
                elem.FindPropertyRelative("Description").stringValue = item.Description;
                elem.FindPropertyRelative("Price").intValue = item.Price;
                elem.FindPropertyRelative("Category").enumValueIndex = (int)item.Category;
                elem.FindPropertyRelative("TargetHeroine").stringValue = item.TargetHeroine ?? "";
                elem.FindPropertyRelative("EffectValue").intValue = item.EffectValue;
                elem.FindPropertyRelative("IconPath").stringValue = item.IconPath ?? "";
                elem.FindPropertyRelative("Tier").enumValueIndex = (int)item.Tier;
                elem.FindPropertyRelative("Availability").enumValueIndex = (int)item.Availability;
                elem.FindPropertyRelative("EffectStat").stringValue = item.EffectStat ?? "";
                elem.FindPropertyRelative("DuplicateTag").stringValue = item.DuplicateTag ?? "";
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            SaveAsset(so, "ItemCatalog.asset");
        }

        // ────────────────────────────────────────
        // ScheduleData
        // ────────────────────────────────────────
        [MenuItem("LoveAlgo/SO 생성/ScheduleData")]
        static void GenerateScheduleData()
        {
            var so = ScriptableObject.CreateInstance<ScheduleDataSO>();
            var serialized = new SerializedObject(so);

            var schedProp = serialized.FindProperty("schedules");
            schedProp.ClearArray();
            var scheduleDefaults = new (ScheduleType t, string name, string desc, int money, int str, int intel, int soc, int per, int fatigue, bool limited)[]
            {
                (ScheduleType.PartTime_Store,"편의점","어쩌구 편의점에서 아르바이트를 합니다.\n10,000원의 수익을 획득합니다.",20000,0,0,0,1,5,false),
                (ScheduleType.PartTime_Loading,"상하차 알바","상하차를 하면 돈도 벌고 힘도 세지고\n50,000원을 버는데\n밤은 새야할지도 어쩌구 저쩌구 3줄까지 입니다.",50000,0,0,0,2,15,true),
                (ScheduleType.Invest,"코인투자","영차영차\n다같이 외쳐 영차영차",0,0,0,0,0,0,false),
                (ScheduleType.Exercise_A,"운동 A","(기획 추가 예정)",0,3,0,0,0,0,false),
                (ScheduleType.Exercise_B,"운동 B","(기획 추가 예정)",0,2,0,0,0,5,false),
                (ScheduleType.Exercise_C,"운동 C","(기획 추가 예정)",0,1,0,0,1,3,false),
                (ScheduleType.Study_D,"공부 D","(기획 추가 예정)",0,0,3,0,0,5,false),
                (ScheduleType.Study_E,"공부 E","(기획 추가 예정)",0,0,2,0,0,3,false),
                (ScheduleType.Study_F,"공부 F","(기획 추가 예정)",0,0,1,1,0,2,false),
            };
            foreach (var s in scheduleDefaults)
            {
                schedProp.InsertArrayElementAtIndex(schedProp.arraySize);
                var elem = schedProp.GetArrayElementAtIndex(schedProp.arraySize - 1);
                elem.FindPropertyRelative("type").enumValueIndex = (int)s.t;
                var eff = elem.FindPropertyRelative("effect");
                eff.FindPropertyRelative("displayName").stringValue = s.name;
                eff.FindPropertyRelative("description").stringValue = s.desc;
                eff.FindPropertyRelative("moneyChange").intValue = s.money;
                eff.FindPropertyRelative("strengthChange").intValue = s.str;
                eff.FindPropertyRelative("intelligenceChange").intValue = s.intel;
                eff.FindPropertyRelative("socialChange").intValue = s.soc;
                eff.FindPropertyRelative("perseveranceChange").intValue = s.per;
                eff.FindPropertyRelative("fatigueChange").intValue = s.fatigue;
                eff.FindPropertyRelative("isLimited").boolValue = s.limited;
            }

            var catProp = serialized.FindProperty("categories");
            catProp.ClearArray();
            var catDefaults = new (ScheduleCategory cat, string name, string desc)[]
            {
                (ScheduleCategory.PartTime,"알바","돈을 벌 수 있어요. 피로도가 오릅니다."),
                (ScheduleCategory.Exercise,"운동","체력을 올릴 수 있어요."),
                (ScheduleCategory.Study,"공부","지성을 올릴 수 있어요. 피로도가 오릅니다."),
            };
            foreach (var c in catDefaults)
            {
                catProp.InsertArrayElementAtIndex(catProp.arraySize);
                var elem = catProp.GetArrayElementAtIndex(catProp.arraySize - 1);
                elem.FindPropertyRelative("category").enumValueIndex = (int)c.cat;
                elem.FindPropertyRelative("displayName").stringValue = c.name;
                elem.FindPropertyRelative("description").stringValue = c.desc;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            SaveAsset(so, "ScheduleData.asset");
        }

        // ────────────────────────────────────────
        // GameBalance
        // ────────────────────────────────────────
        [MenuItem("LoveAlgo/SO 생성/GameBalance")]
        static void GenerateGameBalance()
        {
            var so = ScriptableObject.CreateInstance<GameBalanceSO>();
            var serialized = new SerializedObject(so);

            // 히로인
            var heroProp = serialized.FindProperty("heroines");
            heroProp.ClearArray();
            var heroines = new (string id, string name, int threshold, string stat)[]
            {
                ("Roa","로아",46,"Fatigue"),
                ("Yeun","하예은",32,"Str"),
                ("Daeun","서다은",35,"Int"),
                ("Bom","이봄",39,"Soc"),
                ("Heewon","도희원",43,"Per"),
            };
            foreach (var h in heroines)
            {
                heroProp.InsertArrayElementAtIndex(heroProp.arraySize);
                var elem = heroProp.GetArrayElementAtIndex(heroProp.arraySize - 1);
                elem.FindPropertyRelative("id").stringValue = h.id;
                elem.FindPropertyRelative("displayName").stringValue = h.name;
                elem.FindPropertyRelative("endingThreshold").intValue = h.threshold;
                elem.FindPropertyRelative("preferredStat").stringValue = h.stat;
            }

            // 타임라인 30일
            var tlProp = serialized.FindProperty("timeline");
            tlProp.ClearArray();
            var days = new (int day, DayType type, StoryArc arc, string tag, int pts)[]
            {
                (1,DayType.Free,StoryArc.Opening,null,0),
                (2,DayType.Free,StoryArc.Opening,null,0),
                (3,DayType.Free,StoryArc.FreeTime1,null,0),
                (4,DayType.Free,StoryArc.FreeTime1,null,0),
                (5,DayType.Free,StoryArc.FreeTime1,null,0),
                (6,DayType.PersonalEvent,StoryArc.Event1,"Event1",3),
                (7,DayType.Free,StoryArc.FreeTime2,null,0),
                (8,DayType.Free,StoryArc.FreeTime2,null,0),
                (9,DayType.Free,StoryArc.FreeTime2,null,0),
                (10,DayType.GroupEvent,StoryArc.Festival,"Festival_Day1",0),
                (11,DayType.GroupEvent,StoryArc.Festival,"Festival_Day2",0),
                (12,DayType.GroupEvent,StoryArc.Festival,"Festival_Day3",4),
                (13,DayType.Free,StoryArc.FreeTime3,null,0),
                (14,DayType.Free,StoryArc.FreeTime3,null,0),
                (15,DayType.Free,StoryArc.FreeTime3,null,0),
                (16,DayType.PersonalEvent,StoryArc.Event2,"Event2",6),
                (17,DayType.Free,StoryArc.FreeTime4,null,0),
                (18,DayType.Free,StoryArc.FreeTime4,null,0),
                (19,DayType.Free,StoryArc.FreeTime4,null,0),
                (20,DayType.GroupEvent,StoryArc.MT,"MT_Day1",0),
                (21,DayType.GroupEvent,StoryArc.MT,"MT_Day2",5),
                (22,DayType.GroupEvent,StoryArc.MT,"MT_Day3",0),
                (23,DayType.Free,StoryArc.FreeTime5,null,0),
                (24,DayType.Free,StoryArc.FreeTime5,null,0),
                (25,DayType.Free,StoryArc.FreeTime5,null,0),
                (26,DayType.PersonalEvent,StoryArc.Event3,"Event3",9),
                (27,DayType.Free,StoryArc.FreeTime6,null,0),
                (28,DayType.Free,StoryArc.FreeTime6,null,0),
                (29,DayType.Free,StoryArc.FreeTime6,null,0),
                (30,DayType.Confession,StoryArc.Confession,"Confession",0),
            };
            foreach (var d in days)
            {
                tlProp.InsertArrayElementAtIndex(tlProp.arraySize);
                var elem = tlProp.GetArrayElementAtIndex(tlProp.arraySize - 1);
                elem.FindPropertyRelative("day").intValue = d.day;
                elem.FindPropertyRelative("type").enumValueIndex = (int)d.type;
                elem.FindPropertyRelative("arc").enumValueIndex = (int)d.arc;
                elem.FindPropertyRelative("eventTag").stringValue = d.tag ?? "";
                elem.FindPropertyRelative("eventPoints").intValue = d.pts;
            }

            // 밸런스 수치
            serialized.FindProperty("actionsPerDay").intValue = 2;
            serialized.FindProperty("maxDay").intValue = 30;
            serialized.FindProperty("endingLoveThreshold").intValue = 30;
            serialized.FindProperty("minInvestMoney").intValue = 30000;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            SaveAsset(so, "GameBalance.asset");
        }

        // ────────────────────────────────────────
        // 유틸
        // ────────────────────────────────────────
        static void SaveAsset(ScriptableObject so, string fileName)
        {
            if (!AssetDatabase.IsValidFolder(DataRoot))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                AssetDatabase.CreateFolder("Assets/Resources", "Data");
            }

            string path = $"{DataRoot}/{fileName}";
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) != null)
            {
                if (!EditorUtility.DisplayDialog("SO 덮어쓰기",
                    $"{path} 가 이미 존재합니다.\n덮어쓰시겠습니까?", "덮어쓰기", "취소"))
                {
                    Object.DestroyImmediate(so);
                    return;
                }
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CreateAsset(so, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = so;
            Debug.Log($"[SOGenerator] 생성 완료: {path}");
        }
    }
}
