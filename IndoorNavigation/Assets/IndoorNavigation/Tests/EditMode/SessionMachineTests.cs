using System;
using System.Collections.Generic;
using System.Linq;
using IndoorNavigation.Core.Session;
using NUnit.Framework;

namespace IndoorNavigation.Core.Tests
{
    /// <summary>
    /// Checks the state machine against the screen index of the blueprint. The expected table
    /// is typed out again here on purpose: if someone edits the machine by mistake, the two
    /// tables disagree and a test fails.
    /// </summary>
    public sealed class SessionMachineTests
    {
        private static readonly (SessionState From, SessionTrigger Trigger, SessionState To)[] ScreenIndex =
        {
            (SessionState.ChoosingBuilding, SessionTrigger.Download, SessionState.GettingData),       // V2 -> V3
            (SessionState.GettingData, SessionTrigger.DataReady, SessionState.Locating),              // V3 -> 1
            (SessionState.GettingData, SessionTrigger.Cancel, SessionState.ChoosingBuilding),         // V3 Cancel
            (SessionState.Locating, SessionTrigger.PositionFound, SessionState.Asking),               // 1 -> 2
            (SessionState.Asking, SessionTrigger.ChangeBuilding, SessionState.ChoosingBuilding),      // 2 -> V1
            (SessionState.Asking, SessionTrigger.DescribeNeed, SessionState.Clarifying),              // 2 -> 3b
            (SessionState.Asking, SessionTrigger.BrowsePlaces, SessionState.ChoosingPlace),           // 2 -> 4 or 6
            (SessionState.Asking, SessionTrigger.PickNamedPlace, SessionState.Previewing),            // 2b -> 5
            (SessionState.Clarifying, SessionTrigger.ResultsReady, SessionState.ChoosingPlace),       // 3b -> 4 or 4b
            (SessionState.Clarifying, SessionTrigger.Cancel, SessionState.Asking),                    // 3b Cancel
            (SessionState.ChoosingPlace, SessionTrigger.PickPlace, SessionState.Previewing),          // 4 -> 5
            (SessionState.ChoosingPlace, SessionTrigger.Refine, SessionState.Clarifying),             // 4 -> 3
            (SessionState.ChoosingPlace, SessionTrigger.EditSearch, SessionState.Asking),             // 4b -> 2
            (SessionState.Previewing, SessionTrigger.BackToPlaces, SessionState.ChoosingPlace),       // 5 -> 4
            (SessionState.Previewing, SessionTrigger.EditSearch, SessionState.Asking),                // 5 -> 2
            (SessionState.Previewing, SessionTrigger.Start, SessionState.Navigating),                 // 5 -> B or A
            (SessionState.Navigating, SessionTrigger.Arrive, SessionState.Arrived),                   // -> G
            (SessionState.Navigating, SessionTrigger.EndRoute, SessionState.Asking),                  // B3 -> 2
            (SessionState.Arrived, SessionTrigger.FoundIt, SessionState.Asking),                      // G -> 2
        };

        // Every pair of state and trigger: 9 x 17 = 153 cases.
        private static IEnumerable<TestCaseData> AllPairs()
        {
            foreach (SessionState state in Enum.GetValues(typeof(SessionState)))
            {
                foreach (SessionTrigger trigger in Enum.GetValues(typeof(SessionTrigger)))
                {
                    yield return new TestCaseData(state, trigger).SetName($"{state} + {trigger}");
                }
            }
        }

        [TestCaseSource(nameof(AllPairs))]
        public void Trigger_MovesExactlyAsTheScreenIndexSays(SessionState state, SessionTrigger trigger)
        {
            var machine = new SessionMachine(state);
            var expected = ScreenIndex.Where(row => row.From == state && row.Trigger == trigger).ToList();

            bool fired = machine.TryFire(trigger);

            if (expected.Count == 1)
            {
                Assert.IsTrue(fired, "This transition is in the screen index and must be allowed.");
                Assert.AreEqual(expected[0].To, machine.State);
            }
            else
            {
                Assert.IsFalse(fired, "This transition is not in the screen index and must be refused.");
                Assert.AreEqual(state, machine.State, "A refused trigger must not change the state.");
            }
        }

        [Test]
        public void MachineTable_HasNoRowsBeyondTheScreenIndex()
        {
            Assert.AreEqual(ScreenIndex.Length, SessionMachine.AllowedTransitions.Count());
        }

        [Test]
        public void EveryState_CanBeReachedFromTheFirstStart()
        {
            var reached = new HashSet<SessionState> { SessionState.ChoosingBuilding };
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (var row in ScreenIndex)
                {
                    if (reached.Contains(row.From) && reached.Add(row.To)) grew = true;
                }
            }

            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(SessionState)), reached);
        }

        [Test]
        public void StateChanged_ReportsOldAndNewState()
        {
            var machine = new SessionMachine(SessionState.Previewing);
            SessionState from = default;
            SessionState to = default;
            machine.StateChanged += (f, t) => { from = f; to = t; };

            machine.TryFire(SessionTrigger.Start);

            Assert.AreEqual(SessionState.Previewing, from);
            Assert.AreEqual(SessionState.Navigating, to);
        }
    }
}
