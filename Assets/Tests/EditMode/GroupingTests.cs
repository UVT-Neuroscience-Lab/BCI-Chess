using System.Collections.Generic;
using System.Linq;
using BciChess.Bci;
using NUnit.Framework;

namespace BciChess.Tests
{
    public class GroupingTests
    {
        private static List<BciTarget> Targets(int count) =>
            Enumerable.Range(0, count).Select(i => new BciTarget("t" + i, "T" + i)).ToList();

        [TestCase(2, 2)]
        [TestCase(5, 4)]
        [TestCase(10, 4)]
        [TestCase(16, 10)]
        [TestCase(20, 6)]
        [TestCase(27, 9)]
        [TestCase(60, 4)]
        public void EveryCandidateAppearsInExactlyOneGroup(int count, int maxGroups)
        {
            var candidates = Targets(count);
            var groups = new BalancedGroupingStrategy().Group(candidates, maxGroups);

            Assert.GreaterOrEqual(groups.Count, 2);
            Assert.LessOrEqual(groups.Count, maxGroups);
            Assert.IsTrue(groups.All(g => g.Members.Count > 0));

            var flattened = groups.SelectMany(g => g.Members).ToList();
            Assert.AreEqual(count, flattened.Count, "No duplicates or omissions");
            CollectionAssert.AreEqual(candidates, flattened, "Order is preserved");
        }

        [TestCase(10, 10)]
        [TestCase(37, 8)]
        public void GroupSizesDifferByAtMostOne(int count, int maxGroups)
        {
            var sizes = new BalancedGroupingStrategy().Group(Targets(count), maxGroups).Select(g => g.Members.Count).ToList();
            Assert.LessOrEqual(sizes.Max() - sizes.Min(), 1);
        }

        [Test]
        public void GroupCountIsAboutSquareRoot()
        {
            Assert.AreEqual(4, new BalancedGroupingStrategy().Group(Targets(16), 10).Count);
            Assert.AreEqual(5, new BalancedGroupingStrategy().Group(Targets(20), 10).Count);
        }

        [Test]
        public void Navigator_OffersCandidatesDirectly_WhenTheyFit()
        {
            var navigator = new CandidateNavigator(new BalancedGroupingStrategy());
            var candidates = Targets(3);
            navigator.Reset(candidates);
            Assert.IsTrue(navigator.TryGetOptions(3, out var options));
            CollectionAssert.AreEqual(candidates, options);
        }

        [Test]
        public void Navigator_NestsWhenGroupsAreStillTooLarge()
        {
            var navigator = new CandidateNavigator(new BalancedGroupingStrategy());
            navigator.Reset(Targets(30));

            Assert.IsTrue(navigator.TryGetOptions(3, out var top));
            Assert.AreEqual(3, top.Count);
            navigator.Enter((CandidateGroup)top[0].Payload);
            Assert.AreEqual(10, navigator.CurrentCandidates.Count);

            Assert.IsTrue(navigator.TryGetOptions(3, out var second));
            Assert.IsTrue(second.All(o => o.Payload is CandidateGroup || o.Id.StartsWith("t")));
            Assert.AreEqual(1, navigator.Depth);

            Assert.IsTrue(navigator.Back());
            Assert.IsFalse(navigator.Back());
            Assert.AreEqual(30, navigator.CurrentCandidates.Count);
        }

        [Test]
        public void Navigator_OffersSingleMemberGroupsAsTheMember()
        {
            var navigator = new CandidateNavigator(new BalancedGroupingStrategy());
            navigator.Reset(Targets(3));
            Assert.IsTrue(navigator.TryGetOptions(2, out var options));
            // 3 candidates into 2 groups: sizes 2 and 1; the single one is offered directly.
            Assert.IsTrue(options[0].Payload is CandidateGroup);
            Assert.AreEqual("t2", options[1].Id);
        }

        [Test]
        public void Navigator_FailsWithFewerThanTwoSlots()
        {
            var navigator = new CandidateNavigator(new BalancedGroupingStrategy());
            navigator.Reset(Targets(5));
            Assert.IsFalse(navigator.TryGetOptions(1, out _));
        }
    }
}
