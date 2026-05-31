# 아이템 이미지 폴더

## 구조

- `Art/Item/{name}.png` — **상세 팝업용 큰 이미지** (ShopItemDetailPopup)
- `Art/Item/Icon/{name}.png` — **아이콘** (SaleSlot, CartSlot 공통)

## SOGenerator 연동

`LoveAlgo/SO 생성/ItemCatalog` 실행 시 자동으로 스프라이트 바인딩:

- `ItemData.IconSprite` ← `Art/Item/Icon/{iconPath에서 Items/ 제거한 이름}`
- `ItemData.DetailSprite` ← `Art/Item/{iconPath에서 Items/ 제거한 이름}`

## 기존 이미지 마이그레이션

현재 이미지가 `Resources/Items/`에 있다면:

```
Resources/Items/turtleman.png      → Art/Item/turtleman.png
Resources/Items/Icon/turtleman.png → Art/Item/Icon/turtleman.png
```

마이그레이션 후 SO 재생성 필요:
1. Unity 메뉴 `LoveAlgo > SO 생성 > ItemCatalog` 실행
2. ItemCatalog.asset에 스프라이트 바인딩 확인
