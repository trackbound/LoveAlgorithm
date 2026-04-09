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
        //  선물 — 하예은 (No.1, 6, 11)
        // ═══════════════════════════════════════════
        items.Add(MakeGift("gift_ankle_guard", "발목 보호대",
            "발목을 보호해 주는 보호대", 30000, "Yeeun", 2, 3, "1_gift_ankle_guard"));
        items.Add(MakeGift("gift_meal_coupon", "[한식명인 최명자의 쌈밥명가] 식사권",
            "유명한 쌈밥 맛집의 식사권", 25000, "Yeeun", 2, 3, "6_gift_meal_coupon"));
        items.Add(MakeGift("gift_game_chip", "<점프점프 거북맨> 게임 칩",
            "예은과 어릴 때 함께 플레이했던 게임 칩", 30000, "Yeeun", 2, 3, "11_gift_game_chip"));

        // ═══════════════════════════════════════════
        //  선물 — 서다은 (No.2, 7, 12)
        // ═══════════════════════════════════════════
        items.Add(MakeGift("gift_album", "<트로피컬 글로우> 앨범",
            "세계적 인기 밴드 <트로피컬 글로우> 앨범", 30000, "Daeun", 2, 3, "2_gift_album"));
        items.Add(MakeGift("gift_bread", "알밤달밤 밤식빵",
            "학교 근처 유명 베이커리 [밤밤밤]의 밤식빵", 8000, "Daeun", 1, 2, "7_gift_bread"));
        items.Add(MakeGift("gift_concert_ticket", "<트로피컬 글로우> 내한 콘서트 티켓",
            "<트로피컬 글로우> 내한 콘서트 티켓", 100000, "Daeun", 3, 5, "12_gift_concert_ticket"));

        // ═══════════════════════════════════════════
        //  선물 — 이봄 (No.3, 8, 13)
        // ═══════════════════════════════════════════
        items.Add(MakeGift("gift_rabbit_keyring", "토끼 인형 키링",
            "미니 토끼 인형 키링", 20000, "Bom", 2, 3, "3_gift_rabbit_keyring"));
        items.Add(MakeGift("gift_strawberry_milk", "[내 맘에 딸기가득] 딸기우유",
            "귀여운 패키지의 딸기우유", 3000, "Bom", 1, 2, "8_gift_strawberry_milk"));
        items.Add(MakeGift("gift_popup_ticket", "<러블리톡> 팝업스토어 입장 확정 티켓",
            "<러블리톡> 팝업스토어 입장 확정 티켓", 60000, "Bom", 3, 4, "13_gift_popup_ticket"));

        // ═══════════════════════════════════════════
        //  선물 — 도희원 (No.4, 9, 14)
        // ═══════════════════════════════════════════
        items.Add(MakeGift("gift_soju", "한정판 소주 <여름, 레몬, 밤>",
            "소주 브랜드에서 새로 나온 과일 소주", 40000, "Heewon", 3, 4, "4_gift_soju"));
        items.Add(MakeGift("gift_diamond", "엠파이어 다이아몬드",
            "고급 위스키", 120000, "Heewon", 3, 5, "9_gift_diamond"));
        items.Add(MakeGift("gift_ribbon", "리본 머리끈",
            "흰색 실크 리본 머리끈", 35000, "Heewon", 2, 3, "14_gift_ribbon"));

        // ═══════════════════════════════════════════
        //  선물 — 로아 (No.5, 10, 15)
        // ═══════════════════════════════════════════
        items.Add(MakeGift("gift_blue_shirt", "하늘색 반팔 티셔츠",
            "검은색이 아닌 옷", 40000, "Roa", 3, 4, "5_gift_blue_shirt"));
        items.Add(MakeGift("gift_monitor", "QHD 모니터",
            "화질이 매우 좋은 모니터", 120000, "Roa", 3, 5, "10_gift_monitor"));
        items.Add(MakeGift("gift_headset", "생동감이 느껴지는 헤드셋",
            "로아의 목소리를 더 잘 들을 수 있는 노이즈 캔슬링 헤드셋", 90000, "Roa", 3, 5, "15_gift_headset"));

        // ═══════════════════════════════════════════
        //  피로 회복 소모품 (No.16, 17, 18, 34, 41)
        // ═══════════════════════════════════════════
        items.Add(MakeConsumable("consume_energy_drink", "에너지 음료",
            "고카페인 각성 음료", 3000, 5, "16_consume_energy_drink"));
        items.Add(MakeConsumable("consume_vitamin", "종합비타민",
            "힘이 나는 피로 회복 비타민", 12000, 8, "17_consume_vitamin"));
        items.Add(MakeConsumable("consume_arginine", "아르기닌",
            "남성건강 영양제", 20000, 12, "18_consume_arginine"));
        items.Add(MakeConsumable("consume_mood_lamp", "수면용 무드등",
            "부드러운 빛이 나는 무드등", 25000, 15, "34_consume_mood_lamp"));
        items.Add(MakeConsumable("consume_energy_bar", "에너지바",
            "견과류가 든 에너지바", 5000, 6, "41_consume_energy_bar"));

        // ═══════════════════════════════════════════
        //  세션 버프 — 체력 (No.19, 20)
        // ═══════════════════════════════════════════
        items.Add(MakeBuff("buff_protein_choco", "프로틴_초코맛",
            "단백질 충전에 필요한 초코맛 프로틴", 10000, "Str", 2, "buff_str", "19_buff_protein_choco"));
        items.Add(MakeBuff("buff_protein_straw", "프로틴_딸기맛",
            "단백질 충전에 필요한 딸기맛 프로틴", 12000, "Str", 3, "buff_str", "20_buff_protein_straw"));

        // ═══════════════════════════════════════════
        //  세션 버프 — 지성 (No.21, 22, 24, 25, 28, 30, 40)
        // ═══════════════════════════════════════════
        items.Add(MakeBuff("buff_keyboard", "기계식 키보드",
            "타건음이 좋은 기계식 키보드", 60000, "Int", 2, "buff_int", "21_buff_keyboard"));
        items.Add(MakeBuff("buff_gaming_mouse", "게이밍 마우스",
            "게임 플레이에 도움이 되는 마우스", 35000, "Int", 1, "buff_int", "22_buff_gaming_mouse"));
        items.Add(MakeBuff("buff_note", "노트",
            "필기에 사용하는 노트", 4000, "Int", 1, "buff_int", "24_buff_note"));
        items.Add(MakeBuff("buff_pen", "볼펜",
            "필기에 사용하는 볼펜", 3000, "Int", 1, "buff_int", "25_buff_pen"));
        items.Add(MakeBuff("buff_postit", "포스트잇",
            "공부할 때 사용하는 메모지", 6000, "Int", 1, "buff_int", "28_buff_postit"));
        items.Add(MakeBuff("buff_highlighter", "형광펜 세트",
            "필기에 사용하는 형광펜 3색 세트", 8000, "Int", 2, "buff_int", "30_buff_highlighter"));
        items.Add(MakeBuff("buff_sharp", "샤프",
            "필기에 사용하는 샤프", 5000, "Int", 1, "buff_int", "40_buff_sharp"));

        // ═══════════════════════════════════════════
        //  세션 버프 — 끈기 (No.23, 39, 42, 43)
        // ═══════════════════════════════════════════
        items.Add(MakeBuff("buff_vertical_mouse", "버티컬 마우스",
            "손목 건강에 도움이 되는 마우스", 35000, "Per", 1, "buff_per", "23_buff_vertical_mouse"));
        items.Add(MakeBuff("buff_blanket", "무릎담요",
            "적당한 사이즈의 폭신한 담요", 8000, "Per", 1, "buff_per", "39_buff_blanket"));
        items.Add(MakeBuff("buff_coffee", "아이스 아메리카노",
            "시원한 아메리카노 한 잔", 4000, "Per", 1, "buff_per", "42_buff_coffee"));
        items.Add(MakeBuff("buff_handcream", "핸드크림",
            "좋은 향이 나는 핸드크림", 6000, "Per", 1, "buff_per", "43_buff_handcream"));

        // ═══════════════════════════════════════════
        //  세션 버프 — 사교성 (No.26, 27, 29, 31, 32, 33, 35, 36, 37, 38)
        // ═══════════════════════════════════════════
        items.Add(MakeBuff("buff_tumbler", "텀블러",
            "음료를 담아 다닐 수 있는 텀블러", 7000, "Soc", 1, "buff_soc", "26_buff_tumbler"));
        items.Add(MakeBuff("buff_battery", "보조배터리",
            "어디서나 충전할 수 있는 보조배터리", 10000, "Soc", 1, "buff_soc", "27_buff_battery"));
        items.Add(MakeBuff("buff_diary", "다이어리",
            "하루를 기록하는 일기장", 12000, "Soc", 2, "buff_soc", "29_buff_diary"));
        items.Add(MakeBuff("buff_laptop_stand", "노트북 거치대",
            "노트북을 올려둘 수 있는 거치대", 15000, "Soc", 1, "buff_soc", "31_buff_laptop_stand"));
        items.Add(MakeBuff("buff_bluelight_glasses", "블루라이트 차단 안경",
            "눈에 좋지 않은 청광을 차단하는 안경", 18000, "Soc", 1, "buff_soc", "32_buff_bluelight_glasses"));
        items.Add(MakeBuff("buff_umbrella", "휴대용 우산",
            "가방에 쏙 들어가는 작은 우산", 9000, "Soc", 1, "buff_soc", "33_buff_umbrella"));
        items.Add(MakeBuff("buff_chocolate", "<마일드 스위트> 초콜릿",
            "달콤한 밀크초콜릿", 8000, "Soc", 1, "buff_soc", "35_buff_chocolate"));
        items.Add(MakeBuff("buff_jelly", "<몰랑말랑 후르츠> 젤리",
            "과일 맛의 하트 모양 젤리", 8000, "Soc", 1, "buff_soc", "36_buff_jelly"));
        items.Add(MakeBuff("buff_plant", "미니 화분 다육이",
            "작은 다육식물 화분", 7000, "Soc", 1, "buff_soc", "37_buff_plant"));
        items.Add(MakeBuff("buff_calendar", "탁상형 달력",
            "책상 위에 올려놓는 달력", 5000, "Soc", 1, "buff_soc", "38_buff_calendar"));

        // ─── 스프라이트 자동 바인딩 ───
        int boundDetail = 0, boundIcon = 0, missing = 0;
        foreach (var item in items)
        {
            string key = item.IconPath ?? item.Id;

            // Detail (큰 이미지): Art/GUI/{key}.png
            var detail = AssetDatabase.LoadAssetAtPath<Sprite>($"{DetailDir}/{key}.png");
            if (detail != null) { item.DetailSprite = detail; boundDetail++; }

            // Icon (작은 아이콘): Art/GUI/Icon/{key}_icon.png
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/{key}_icon.png");
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
        string heroine, int love2, int love3, string iconPath)
    {
        return new ItemData(id, name, desc, price, ItemCategory.Gift,
            iconPath: iconPath, targetHeroine: heroine, loveEffect2: love2, loveEffect3: love3);
    }

    static ItemData MakeConsumable(string id, string name, string desc, int price, int effectValue, string iconPath)
    {
        return new ItemData(id, name, desc, price, ItemCategory.Consumable,
            effectValue: effectValue, iconPath: iconPath);
    }

    static ItemData MakeBuff(string id, string name, string desc, int price,
        string stat, int effectValue, string dupTag, string iconPath)
    {
        return new ItemData(id, name, desc, price, ItemCategory.SessionBuff,
            effectValue: effectValue, iconPath: iconPath, effectStat: stat, duplicateTag: dupTag);
    }
}
