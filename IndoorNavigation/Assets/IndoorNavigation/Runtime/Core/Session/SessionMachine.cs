using System;
using System.Collections.Generic;

namespace IndoorNavigation.Core.Session
{
    /// <summary>
    /// The session state machine: one table of allowed transitions and nothing else.
    /// Keeping it this small makes it easy to test exhaustively (Harel 1987 for statecharts;
    /// the table is the screen index of the blueprint, read state by state).
    /// </summary>
    public sealed class SessionMachine
    {
        // One row per allowed transition. Anything not listed here is not allowed.
        private static readonly Dictionary<(SessionState, SessionTrigger), SessionState> Transitions =
            new Dictionary<(SessionState, SessionTrigger), SessionState>
            {
                // Getting ready
                { (SessionState.ChoosingBuilding, SessionTrigger.Download), SessionState.GettingData },
                { (SessionState.GettingData, SessionTrigger.DataReady), SessionState.Locating },
                { (SessionState.GettingData, SessionTrigger.Cancel), SessionState.ChoosingBuilding },
                { (SessionState.Locating, SessionTrigger.PositionFound), SessionState.Asking },

                // Finding a destination
                { (SessionState.Asking, SessionTrigger.ChangeBuilding), SessionState.ChoosingBuilding },
                { (SessionState.Asking, SessionTrigger.DescribeNeed), SessionState.Clarifying },
                { (SessionState.Asking, SessionTrigger.BrowsePlaces), SessionState.ChoosingPlace },
                { (SessionState.Asking, SessionTrigger.PickNamedPlace), SessionState.Previewing },
                { (SessionState.Clarifying, SessionTrigger.ResultsReady), SessionState.ChoosingPlace },
                { (SessionState.Clarifying, SessionTrigger.Cancel), SessionState.Asking },
                { (SessionState.ChoosingPlace, SessionTrigger.PickPlace), SessionState.Previewing },
                { (SessionState.ChoosingPlace, SessionTrigger.Refine), SessionState.Clarifying },
                { (SessionState.ChoosingPlace, SessionTrigger.EditSearch), SessionState.Asking },
                { (SessionState.Previewing, SessionTrigger.BackToPlaces), SessionState.ChoosingPlace },
                { (SessionState.Previewing, SessionTrigger.EditSearch), SessionState.Asking },
                { (SessionState.Previewing, SessionTrigger.Start), SessionState.Navigating },

                // Navigating and arriving
                { (SessionState.Navigating, SessionTrigger.Arrive), SessionState.Arrived },
                { (SessionState.Navigating, SessionTrigger.EndRoute), SessionState.Asking },
                { (SessionState.Arrived, SessionTrigger.FoundIt), SessionState.Asking },
            };

        public SessionMachine(SessionState initial)
        {
            State = initial;
        }

        /// <summary>The current state.</summary>
        public SessionState State { get; private set; }

        /// <summary>Raised after every transition, with the old and the new state.</summary>
        public event Action<SessionState, SessionState> StateChanged;

        /// <summary>All allowed transitions, for documentation and tests.</summary>
        public static IEnumerable<KeyValuePair<(SessionState, SessionTrigger), SessionState>> AllowedTransitions => Transitions;

        public bool CanFire(SessionTrigger trigger) => Transitions.ContainsKey((State, trigger));

        /// <summary>
        /// Moves to the next state if the trigger is allowed in the current one.
        /// Returns false and changes nothing otherwise, so a late or double tap is harmless.
        /// </summary>
        public bool TryFire(SessionTrigger trigger)
        {
            if (!Transitions.TryGetValue((State, trigger), out SessionState next))
            {
                return false;
            }

            SessionState previous = State;
            State = next;
            StateChanged?.Invoke(previous, next);
            return true;
        }
    }
}
