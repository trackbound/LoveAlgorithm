using UnityEditor;
using UnityEngine;
using LoveAlgo.Shop;

/// <summary>
/// 에디터 전용: ItemCatalog SO를 자동 생성하고 스프라이트를 바인딩합니다.
/// 메뉴: LoveAlgo > Generate Item Catalog
/// </summary>
public static class ItemCatalogGenerator
{
    const string OutputPath = "Assets/Resources/Data/ItemCatalog.asset";
    const string DetailDir = "Assets/Art/GUI";
    const string IconDir   = "Assets/Art/GUI/Icon";

    [MenuItem("LoveAlgo/Generate Item Catalog")]
    public static void Generate()
    {
        // 기존 에셋 로드 또는 새로 생성
        var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(OutputPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ItemCatalogSO>();
            // 폴더 보장
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Data"))
                AssetDatabase.CreateFolder("Assets/Resources", "Data");
            AssetDatabase.CreateAsset(catalog, OutputPath);
        }

        // Reflection으로 private items 필드 접근
        var field = typeof(ItemCatalogSO).GetField("items",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null)
        {
            Debug.LogError("[ItemCatalogGenerator] items 필드를 찾을 수 없습니다.");
            return;
        }

        var items = new System.Collections.Generic.List<ItemData>();

        // ═══════════════════════════════════════════
        //  선물 — 하예은
        // ═══════════════════════════════════════════
        items.Add(MakeGift("gift_ankle_guard", "발목 보호대",
            "운동할 때 필수인 발목 보호대.\n예은이가 좋아할 것 같다.", 30000, "Yeeun", 2, 3));
        items.Add(MakeGift("gift_meal_coupon", "쌈밥 식사권",
            "동네 맛집 쌈밥 식사권.\n예은이랑 같이 가면 좋겠다.", 25000, "Yeeun", 2, 3));
        items.Add(MakeGift("gift_game_chip", "게임칩",
            "인기 게임의 칩 세트.\n예은이가 관심 있어할지도.", 30000, "Yeeun", 2, 3));

        // ═══════════════════════════════════════════
        //  선물 — 서다은
        // ═══════════════════════════════════════════
        items.Add(MakeGift("gift_album", "앨범",
            "좋아하는 아티스트의 앨범.\n다은이가 좋아할 것 같다.", 30000, "Daeun", 2, 3));
        items.Add(MakeGift("gift_bread", "밤식빵",
            "밤이 가득 든 식빵.\n다은이의 취향 저격.", 8000, "Daeun", 1, 2));
        items.Add(MakeGift("gift_concert_ticket", "콘서트 티켓",
            "인기 가수의 콘서트 티켓.\n다은이가 정말 좋아할 거다.", 100000, "Daeun", 3, 5));

        // ═══════════════════════════════════════════
        //  선물 — 이봄
        // ═══════════════════════════════════════════
        items.Add(MakeGift("gift_rabbit_keyring", "토끼 키링",
            "귀여운 토끼 열쇠고리.\n봄이가 좋아할 것 같다.", 20000, "Bom", 2, 3));
        items.Add(MakeGift("gift_strawberry_milk", "딸기우유",
            "딸기맛 우유.\n봄이의 최애 음료.", 3000, "Bom", 1, 2));
        items.Add(MakeGift("gift_popup_ticket", "팝업스토어 티켓",
            "인기 팝업스토어 입장권.\n봄이랑 같이 가면 좋겠다.", 60000, "Bom", 3, 4));

        // ═══════════════════════════════════════════
        //  선물 — 도희원
        // ═══════════════════════════════════════════
        items.Add(MakeGift("gift_soju", "한정판 소주",
            "한정판 프리미엄 소주.\n희원이가 좋아할 것 같다.", 40000, "Heewon", 3, 4));
        items.Add(MakeGift("gift_diamond", "다이아몬드",
            "작지만 빛나는 다이아몬드.\n희원이가 정말 좋아할 거다.", 120000, "Heewon", 3, 5));
        items.Add(MakeGift("gift_ribbon", "리본 머리끈",
            "예쁜 리본 모양 머리끈.\n희원이한테 어울릴 것 같다.", 35000, "Heewon", 2, 3));

        // ═══════════════════════════════════════════
        //  선물 — 로아
        // ═══════════════════════════════════════════
        items.Add(MakeGift("gift_blue_shirt", "하늘색 티셔츠",
            "하늘색 포인트 티셔츠.\n로아가 좋아할 것 같다.", 40000, "Roa", 3, 4));
        items.Add(MakeGift("gift_monitor", "QHD 모니터",
            "고해상도 QHD 모니터.\n로아가 정말 좋아할 거다.", 120000, "Roa", 3, 5));
        items.Add(MakeGift("gift_headset", "헤드셋",
            "고급 게이밍 헤드셋.\n로아가 정말 좋아할 거다.", 90000, "Roa", 3, 5));

        // ═══════════════════════════════════════════
        //  피로 회복 소모품
        // ═══════════════════════════════════════════
        items.Add(MakeConsumable("consume_energy_drink", "에너지 음료",
            "졸릴 때 마시면 좋은 에너지 음료.\n피로가 조금 줄어든다.", 3000, 5));
        items.Add(MakeConsumable("consume_energy_bar", "에너지바",
            "간편하게 먹을 수 있는 에너지바.\n피로를 적당히 줄여준다.", 5000, 6));
        items.Add(MakeConsumable("consume_vitamin", "종합비타민",
            "종합비타민 한 알로 컨디션 UP.\n피로 회복에 효과적이다.", 12000, 8));
        items.Add(MakeConsumable("consume_arginine", "아르기닌",
            "고함량 아르기닌 보충제.\n피로가 크게 줄어든다.", 20000, 12));
        items.Add(MakeConsumable("consume_mood_lamp", "수면용 무드등",
            "은은한 수면용 무드등.\n피로를 확실히 풀어준다.", 25000, 15));

        // ═══════════════════════════════════════════
        //  세션 버프 — 체력
        // ═══════════════════════════════════════════
        items.Add(MakeBuff("buff_protein_choco", "프로틴 초코맛",
            "운동 전에 먹으면 효과적인 초코 프로틴.\n자유행동 1회 체력 +2", 10000, "Str", 2, "buff_str"));
        items.Add(MakeBuff("buff_protein_straw", "프로틴 딸기맛",
            "딸기맛 프로틴. 맛도 좋고 효과도 좋다.\n자유행동 1회 체력 +3", 12000, "Str", 3, "buff_str"));

        // ═══════════════════════════════════════════
        //  세션 버프 — 지성
        // ═══════════════════════════════════════════
        items.Add(MakeBuff("buff_note", "노트",
            "깔끔한 줄 노트. 공부 효율이 오른다.\n자유행동 1회 지성 +1", 4000, "Int", 1, "buff_int"));
        items.Add(MakeBuff("buff_pen", "볼펜",
            "필기감 좋은 볼펜.\n자유행동 1회 지성 +1", 3000, "Int", 1, "buff_int"));
        items.Add(MakeBuff("buff_postit", "포스트잇",
            "형형색색 포스트잇. 정리에 딱이다.\n자유행동 1회 지성 +1", 6000, "Int", 1, "buff_int"));
        items.Add(MakeBuff("buff_highlighter", "형광펜 세트",
            "5색 형광펜 세트. 중요한 건 표시!\n자유행동 1회 지성 +2", 8000, "Int", 2, "buff_int"));
        items.Add(MakeBuff("buff_sharp", "샤프",
            "0.3mm 정밀 샤프. 필기감이 좋다.\n자유행동 1회 지성 +1", 5000, "Int", 1, "buff_int"));
        items.Add(MakeBuff("buff_keyboard", "기계식 키보드",
            "청축 기계식 키보드. 타건감 최고.\n자유행동 1회 지성 +2", 60000, "Int", 2, "buff_int"));
        items.Add(MakeBuff("buff_gaming_mouse", "게이밍 마우스",
            "인체공학 게이밍 마우스.\n자유행동 1회 지성 +1", 35000, "Int", 1, "buff_int"));

        // ═══════════════════════════════════════════
        //  세션 버프 — 끈기
        // ═══════════════════════════════════════════
        items.Add(MakeBuff("buff_vertical_mouse", "버티컬 마우스",
            "손목이 편한 버티컬 마우스.\n자유행동 1회 끈기 +1", 35000, "Per", 1, "buff_per"));
        items.Add(MakeBuff("buff_coffee", "아이스 아메리카노",
            "시원한 아이스 아메리카노.\n자유행동 1회 끈기 +1", 4000, "Per", 1, "buff_per"));
        items.Add(MakeBuff("buff_blanket", "무릎담요",
            "따뜻한 무릎담요. 집중력이 올라간다.\n자유행동 1회 끈기 +1, 피로 -2", 8000, "Per", 1, "buff_per"));
        items.Add(MakeBuff("buff_handcream", "핸드크림",
            "보습 핸드크림. 손이 편해진다.\n자유행동 1회 끈기 +1", 6000, "Per", 1, "buff_per"));

        // ═══════════════════════════════════════════
        //  세션 버프 — 사교성
        // ═══════════════════════════════════════════
        items.Add(MakeBuff("buff_tumbler", "텀블러",
            "감성 텀블러. 대화의 시작.\n자유행동 1회 사교성 +1", 7000, "Soc", 1, "buff_soc"));
        items.Add(MakeBuff("buff_battery", "보조배터리",
            "든든한 보조배터리.\n자유행동 1회 사교성 +1", 10000, "Soc", 1, "buff_soc"));
        items.Add(MakeBuff("buff_diary", "다이어리",
            "예쁜 다이어리. 일정 관리에 딱이다.\n자유행동 1회 사교성 +2", 12000, "Soc", 2, "buff_soc"));
        items.Add(MakeBuff("buff_laptop_stand", "노트북 거치대",
            "인체공학 노트북 거치대.\n자유행동 1회 사교성 +1, 지성 +1", 15000, "Soc", 1, "buff_soc"));
        items.Add(MakeBuff("buff_bluelight_glasses", "블루라이트 차단 안경",
            "눈이 편해지는 블루라이트 차단 안경.\n자유행동 1회 사교성 +1", 18000, "Soc", 1, "buff_soc"));
        items.Add(MakeBuff("buff_umbrella", "휴대용 우산",
            "갑자기 비가 와도 걱정 없다.\n자유행동 1회 사교성 +1", 9000, "Soc", 1, "buff_soc"));
        items.Add(MakeBuff("buff_plant", "미니 화분",
            "작은 다육식물 화분.\n자유행동 1회 사교성 +1", 7000, "Soc", 1, "buff_soc"));
        items.Add(MakeBuff("buff_calendar", "탁상 달력",
            "귀여운 탁상 달력.\n자유행동 1회 사교성 +1", 5000, "Soc", 1, "buff_soc"));
        items.Add(MakeBuff("buff_chocolate", "초콜릿",
            "달콤한 초콜릿.\n자유행동 1회 사교성 +1", 8000, "Soc", 1, "buff_soc"));
        items.Add(MakeBuff("buff_jelly", "젤리",
            "쫀득한 젤리.\n자유행동 1회 사교성 +1", 8000, "Soc", 1, "buff_soc"));

        // ─── 스프라이트 자동 바인딩 ───
        int boundDetail = 0, boundIcon = 0, missing = 0;
        foreach (var item in items)
        {
            string key = item.IconPath ?? item.Id;

            // Detail (큰 이미지): Art/GUI/{key}.png
            var detail = AssetDatabase.LoadAssetAtPath<Sprite>($"{DetailDir}/{key}.png");
            if (detail != null) { item.DetailSprite = detail; boundDetail++; }

            // Icon (작은 아이콘): Art/GUI/Icon/{key}.png
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/{key}.png");
            if (icon != null) { item.IconSprite = icon; boundIcon++; }

            if (detail == null && icon == null) missing++;
        }

        field.SetValue(catalog, items);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ItemCatalogGenerator] {items.Count}개 아이템 등록 완료\n"
                + $"  Detail 바인딩: {boundDetail}, Icon 바인딩: {boundIcon}, 미존재: {missing}");
    }

    static ItemData MakeGift(string id, string name, string desc, int price,
        string heroine, int love2, int love3)
    {
        return new ItemData(id, name, desc, price, ItemCategory.Gift,
            iconPath: id, targetHeroine: heroine, loveEffect2: love2, loveEffect3: love3);
    }

    static ItemData MakeConsumable(string id, string name, string desc, int price, int effectValue)
    {
        return new ItemData(id, name, desc, price, ItemCategory.Consumable,
            effectValue: effectValue, iconPath: id);
    }

    static ItemData MakeBuff(string id, string name, string desc, int price,
        string stat, int effectValue, string dupTag)
    {
        return new ItemData(id, name, desc, price, ItemCategory.SessionBuff,
            effectValue: effectValue, iconPath: id, effectStat: stat, duplicateTag: dupTag);
    }
}
