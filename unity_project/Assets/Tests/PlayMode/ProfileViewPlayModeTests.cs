using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo.Core;
using LoveAlgo.Messenger;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 프로필 패널/편집 PlayMode — 친구 vs 플레이어 표시 규칙(편집 버튼은 플레이어만),
    /// 사진 확대 열고 닫기, 편집 저장=상태 기록·닫기=폐기. 리그는 코드 구성(ModalView 패턴).
    /// </summary>
    public class ProfileViewPlayModeTests
    {
        GameStateSO _gs;
        FriendCatalogSO _friends;
        MessengerProfileCatalogSO _profiles;
        Texture2D _tex;
        Sprite _spriteA, _spriteB;

        [SetUp]
        public void SetUp()
        {
            _gs = ScriptableObject.CreateInstance<GameStateSO>();
            _gs.Data.playerName = "밥차";

            _friends = ScriptableObject.CreateInstance<FriendCatalogSO>();
            _friends.SetEntries(new List<FriendCatalogSO.Entry>
            {
                new() { id = "c01", displayName = "로아", defaultStatus = "상태 메세지입니다." }
            });

            _tex = new Texture2D(2, 2);
            _spriteA = Sprite.Create(_tex, new Rect(0, 0, 2, 2), Vector2.zero);
            _spriteB = Sprite.Create(_tex, new Rect(0, 0, 2, 2), Vector2.zero);
            _profiles = ScriptableObject.CreateInstance<MessengerProfileCatalogSO>();
            _profiles.SetData(new List<Sprite> { _spriteA, _spriteB }, new List<Sprite> { _spriteA, _spriteB });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_profiles);
            Object.DestroyImmediate(_friends);
            Object.DestroyImmediate(_gs);
            Object.DestroyImmediate(_spriteA);
            Object.DestroyImmediate(_spriteB);
            Object.DestroyImmediate(_tex);
        }

        ProfilePanelView BuildPanel(out GameObject go)
        {
            go = new GameObject("ProfilePanel", typeof(RectTransform));
            go.SetActive(false);
            var panel = go.AddComponent<ProfilePanelView>();
            panel.State = _gs;
            panel.Friends = _friends;
            panel.ProfileCatalog = _profiles;

            var empty = new GameObject("Empty", typeof(RectTransform));
            empty.transform.SetParent(go.transform);
            panel.EmptyRoot = empty;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(go.transform);
            content.SetActive(false);
            panel.ContentRoot = content;

            panel.BgImage = NewImage(content.transform, "Bg");
            panel.PortraitImage = NewImage(content.transform, "Portrait");
            panel.PortraitButton = panel.PortraitImage.gameObject.AddComponent<Button>();
            panel.NameText = NewText(content.transform, "Name");
            panel.StatusText = NewText(content.transform, "Status");
            panel.EditButton = NewImage(content.transform, "Edit").gameObject.AddComponent<Button>();

            var zoom = new GameObject("Zoom", typeof(RectTransform));
            zoom.transform.SetParent(go.transform);
            zoom.SetActive(false);
            panel.ZoomRoot = zoom;
            panel.ZoomImage = NewImage(zoom.transform, "ZoomImage");
            panel.ZoomNameText = NewText(zoom.transform, "ZoomName");
            panel.ZoomCloseButton = NewImage(zoom.transform, "ZoomBg").gameObject.AddComponent<Button>();
            return panel;
        }

        static Image NewImage(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent);
            return go.AddComponent<Image>();
        }

        static TMP_Text NewText(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent);
            return go.AddComponent<TextMeshProUGUI>();
        }

        [UnityTest]
        public IEnumerator Panel_Shows_Friend_Vs_Player_With_Edit_Rule()
        {
            var panel = BuildPanel(out var go);
            go.SetActive(true);
            yield return null;

            try
            {
                panel.Clear();
                Assert.IsTrue(panel.EmptyRoot.activeSelf, "기본 = 빈 화면(기획서)");
                Assert.IsFalse(panel.ContentRoot.activeSelf);

                panel.Show("c01");
                Assert.IsTrue(panel.ContentRoot.activeSelf);
                Assert.AreEqual("로아", panel.NameText.text);
                Assert.IsFalse(panel.EditButton.gameObject.activeSelf, "친구 프로필엔 편집 없음");

                MessengerService.SetPlayerProfile(_gs, 1, 0, "");
                panel.Show(FriendListView.PlayerRowId);
                Assert.AreEqual("밥차", panel.NameText.text, "플레이어 = playerName");
                Assert.AreEqual("상태 메세지입니다.", panel.StatusText.text, "빈 상메 = 기본 문구");
                Assert.IsTrue(panel.EditButton.gameObject.activeSelf, "플레이어 프로필만 편집 버튼");
                Assert.AreSame(_spriteB, panel.PortraitImage.sprite, "선택 인덱스(1)의 후보 사진");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [UnityTest]
        public IEnumerator Zoom_Opens_From_Portrait_And_Closes()
        {
            var panel = BuildPanel(out var go);
            go.SetActive(true);
            yield return null;

            try
            {
                panel.Show("c01");
                panel.PortraitButton.onClick.Invoke();
                Assert.IsTrue(panel.ZoomRoot.activeSelf, "사진 클릭 → 확대 출력(기획서 p12)");
                Assert.AreEqual("로아", panel.ZoomNameText.text);

                panel.ZoomCloseButton.onClick.Invoke();
                Assert.IsFalse(panel.ZoomRoot.activeSelf, "클릭으로 닫힘");
            }
            finally { Object.DestroyImmediate(go); }
        }

        ProfileEditView BuildEdit(out GameObject go)
        {
            go = new GameObject("ProfileEdit", typeof(RectTransform));
            go.SetActive(false);
            var edit = go.AddComponent<ProfileEditView>();
            edit.State = _gs;
            edit.ProfileCatalog = _profiles;

            var root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(go.transform);
            root.SetActive(false);
            edit.Root = root;

            var images = new GameObject("Images", typeof(RectTransform));
            images.transform.SetParent(root.transform);
            edit.ImageContainer = images.transform;
            var bgs = new GameObject("Bgs", typeof(RectTransform));
            bgs.transform.SetParent(root.transform);
            edit.BgContainer = bgs.transform;

            // 후보 슬롯 프리팹(코드 구성)
            var slotGo = new GameObject("ProfileChoiceSlot", typeof(RectTransform), typeof(Button));
            var slot = slotGo.AddComponent<ProfileChoiceSlot>();
            slot.Button = slotGo.GetComponent<Button>();
            slot.Image = NewImage(slotGo.transform, "Candidate");
            var frame = new GameObject("Frame", typeof(RectTransform));
            frame.transform.SetParent(slotGo.transform);
            frame.SetActive(false);
            slot.SelectedFrame = frame;
            edit.SlotPrefab = slot;

            var inputGo = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            inputGo.transform.SetParent(root.transform, false);
            edit.StatusInput = inputGo.GetComponent<TMP_InputField>();

            edit.SaveButton = NewImage(root.transform, "Save").gameObject.AddComponent<Button>();
            edit.CloseButton = NewImage(root.transform, "Close").gameObject.AddComponent<Button>();
            return edit;
        }

        [UnityTest]
        public IEnumerator Edit_Save_Writes_State_And_Close_Discards()
        {
            var edit = BuildEdit(out var go);
            go.SetActive(true);
            yield return null;

            bool saved = false;
            edit.Saved += () => saved = true;
            try
            {
                edit.Open();
                Assert.IsTrue(edit.IsOpen);
                var imageSlots = edit.ImageContainer.GetComponentsInChildren<ProfileChoiceSlot>();
                Assert.AreEqual(2, imageSlots.Length, "후보 수만큼 슬롯");

                // 사진 1번 + 배경 1번 선택 후 저장
                imageSlots[1].Button.onClick.Invoke();
                var bgSlots = edit.BgContainer.GetComponentsInChildren<ProfileChoiceSlot>();
                bgSlots[1].Button.onClick.Invoke();
                edit.StatusInput.text = "  새 상메  ";
                edit.SaveButton.onClick.Invoke();

                Assert.IsTrue(saved, "저장 통지");
                Assert.IsFalse(edit.IsOpen, "저장 후 닫힘");
                Assert.AreEqual(1, _gs.Data.messengerProfileImage);
                Assert.AreEqual(1, _gs.Data.messengerProfileBg);
                Assert.AreEqual("새 상메", _gs.Data.messengerStatusMessage, "공백 트림 저장");

                // 다시 열어 다른 선택 후 닫기 = 폐기
                edit.Open();
                edit.ImageContainer.GetComponentsInChildren<ProfileChoiceSlot>()[0].Button.onClick.Invoke();
                edit.CloseButton.onClick.Invoke();
                Assert.AreEqual(1, _gs.Data.messengerProfileImage, "닫기는 저장 안 함");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
