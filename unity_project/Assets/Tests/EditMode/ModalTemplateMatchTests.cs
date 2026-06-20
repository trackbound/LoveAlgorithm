using NUnit.Framework;
using LoveAlgo.UI;
using LoveAlgo.Events;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// ModalTemplate.MatchTemplate 순수 선택 로직(GameObject 불필요). 정확 매칭 우선·없으면 폴백(빈 시그니처)·
    /// 폴백도 없으면 -1. 어댑터(ModalView 인스턴스화/바인딩)는 PlayMode에서 검증.
    /// </summary>
    public class ModalTemplateMatchTests
    {
        static ModalButtonKind[][] Sigs() => new[]
        {
            new[] { ModalButtonKind.No, ModalButtonKind.Yes }, // 0 = YesNo
            new[] { ModalButtonKind.Yes },                     // 1 = YesOnly
            new ModalButtonKind[0],                            // 2 = 폴백
        };

        [Test]
        public void Match_ExactSignature_ReturnsThatIndex()
        {
            Assert.AreEqual(0, ModalTemplate.MatchTemplate(new[] { ModalButtonKind.No, ModalButtonKind.Yes }, Sigs()));
            Assert.AreEqual(1, ModalTemplate.MatchTemplate(new[] { ModalButtonKind.Yes }, Sigs()));
        }

        [Test]
        public void Match_NoExact_ReturnsFallbackIndex()
        {
            // 종류 다름(Default), Close, 순서 뒤바뀜 → 모두 폴백(2)
            Assert.AreEqual(2, ModalTemplate.MatchTemplate(new[] { ModalButtonKind.Default, ModalButtonKind.Default }, Sigs()));
            Assert.AreEqual(2, ModalTemplate.MatchTemplate(new[] { ModalButtonKind.Close }, Sigs()));
            Assert.AreEqual(2, ModalTemplate.MatchTemplate(new[] { ModalButtonKind.Yes, ModalButtonKind.No }, Sigs())); // 순서 역
            Assert.AreEqual(2, ModalTemplate.MatchTemplate(new ModalButtonKind[0], Sigs()));
        }

        [Test]
        public void Match_NoFallbackAvailable_ReturnsMinusOne()
        {
            var noFallback = new[] { new[] { ModalButtonKind.Yes } };
            Assert.AreEqual(-1, ModalTemplate.MatchTemplate(new[] { ModalButtonKind.No, ModalButtonKind.Yes }, noFallback));
        }
    }
}
