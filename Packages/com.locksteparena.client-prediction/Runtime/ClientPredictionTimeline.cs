using System;
using LockstepArena.Simulation;

namespace LockstepArena.Client.Prediction
{
    public sealed class ClientPredictionTimeline
    {
        private BattleState _authoritativeState;
        private BattleState _predictedState;
        private PredictionRecord[] _history;
        private readonly int _maxPredictionTicks;
        private bool _faulted;

        public ClientPredictionTimeline(BattleState initialState, int maxPredictionTicks)
        {
            if (initialState is null)
            {
                throw new ArgumentNullException(nameof(initialState));
            }

            if (maxPredictionTicks < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxPredictionTicks),
                    "Maximum prediction ticks must be positive.");
            }

            _authoritativeState = initialState;
            _predictedState = initialState;
            _history = Array.Empty<PredictionRecord>();
            _maxPredictionTicks = maxPredictionTicks;
        }

        public BattleState AuthoritativeState => _authoritativeState;

        public BattleState PredictedState => _predictedState;

        public int MaxPredictionTicks => _maxPredictionTicks;

        public int PendingPredictionCount => _history.Length;

        public void Predict(FrameData predictedFrame)
        {
            ThrowIfFaulted();
            if (predictedFrame is null)
            {
                throw new ArgumentNullException(nameof(predictedFrame));
            }

            if (predictedFrame.Tick != _predictedState.Tick)
            {
                throw new ArgumentException(
                    "Predicted frame tick must match the predicted state tick.",
                    nameof(predictedFrame));
            }

            if (!_predictedState.Roster.HasSameStructure(predictedFrame.Roster))
            {
                throw new ArgumentException(
                    "Predicted frame roster must match the prediction roster.",
                    nameof(predictedFrame));
            }

            if (_predictedState.Tick == uint.MaxValue)
            {
                throw new InvalidOperationException("The predicted timeline is exhausted.");
            }

            EnsureInvariantOrFault(_authoritativeState, _predictedState, _history);

            ulong predictionLead = (ulong)_predictedState.Tick - _authoritativeState.Tick;
            if (predictionLead >= (ulong)_maxPredictionTicks)
            {
                throw new InvalidOperationException("Maximum prediction lead has been reached.");
            }

            try
            {
                var simulation = new BattleSimulation(_predictedState);
                simulation.Step(predictedFrame);

                var candidateHistory = new PredictionRecord[_history.Length + 1];
                Array.Copy(_history, candidateHistory, _history.Length);
                candidateHistory[candidateHistory.Length - 1] =
                    new PredictionRecord(_predictedState, predictedFrame);

                BattleState candidatePredictedState = simulation.State;
                EnsureInvariantOrFault(
                    _authoritativeState,
                    candidatePredictedState,
                    candidateHistory);

                _predictedState = candidatePredictedState;
                _history = candidateHistory;
            }
            catch
            {
                _faulted = true;
                throw;
            }
        }

        public bool ReconcileAuthoritative(FrameData authoritativeFrame)
        {
            ThrowIfFaulted();
            if (authoritativeFrame is null)
            {
                throw new ArgumentNullException(nameof(authoritativeFrame));
            }

            if (authoritativeFrame.Tick != _authoritativeState.Tick)
            {
                throw new ArgumentException(
                    "Authoritative frame tick must match the authoritative state tick.",
                    nameof(authoritativeFrame));
            }

            if (!_authoritativeState.Roster.HasSameStructure(authoritativeFrame.Roster))
            {
                throw new ArgumentException(
                    "Authoritative frame roster must match the prediction roster.",
                    nameof(authoritativeFrame));
            }

            if (_authoritativeState.Tick == uint.MaxValue)
            {
                throw new InvalidOperationException("The authoritative timeline is exhausted.");
            }

            EnsureInvariantOrFault(_authoritativeState, _predictedState, _history);

            if (_history.Length == 0)
            {
                return ReconcileAtAlignedFrontier(authoritativeFrame);
            }

            if (!FramesHaveSameValue(_history[0].PredictedFrame, authoritativeFrame))
            {
                throw new InvalidOperationException(
                    "Dirty reconciliation is added in the next implementation checkpoint.");
            }

            return ReconcileClean(authoritativeFrame);
        }

        private bool ReconcileAtAlignedFrontier(FrameData authoritativeFrame)
        {
            try
            {
                var simulation = new BattleSimulation(_authoritativeState);
                simulation.Step(authoritativeFrame);

                BattleState candidateState = simulation.State;
                PredictionRecord[] candidateHistory = Array.Empty<PredictionRecord>();
                EnsureInvariantOrFault(candidateState, candidateState, candidateHistory);

                _authoritativeState = candidateState;
                _predictedState = candidateState;
                _history = candidateHistory;
                return false;
            }
            catch
            {
                _faulted = true;
                throw;
            }
        }

        private bool ReconcileClean(FrameData authoritativeFrame)
        {
            try
            {
                var simulation = new BattleSimulation(_authoritativeState);
                simulation.Step(authoritativeFrame);

                var candidateHistory = new PredictionRecord[_history.Length - 1];
                Array.Copy(_history, 1, candidateHistory, 0, candidateHistory.Length);

                BattleState candidateAuthoritativeState = simulation.State;
                EnsureInvariantOrFault(
                    candidateAuthoritativeState,
                    _predictedState,
                    candidateHistory);

                _authoritativeState = candidateAuthoritativeState;
                _history = candidateHistory;
                return false;
            }
            catch
            {
                _faulted = true;
                throw;
            }
        }

        private void ThrowIfFaulted()
        {
            if (_faulted)
            {
                throw new InvalidOperationException("The prediction timeline is faulted.");
            }
        }

        private void EnsureInvariantOrFault(
            BattleState authoritativeState,
            BattleState predictedState,
            PredictionRecord[] history)
        {
            if (!HasValidInvariant(authoritativeState, predictedState, history))
            {
                _faulted = true;
                throw new InvalidOperationException("The prediction timeline invariant is invalid.");
            }
        }

        private bool HasValidInvariant(
            BattleState authoritativeState,
            BattleState predictedState,
            PredictionRecord[] history)
        {
            uint authoritativeTick = authoritativeState.Tick;
            uint predictedTick = predictedState.Tick;
            if (authoritativeTick > predictedTick ||
                !authoritativeState.Roster.HasSameStructure(predictedState.Roster))
            {
                return false;
            }

            ulong lead = (ulong)predictedTick - authoritativeTick;
            if (lead != (ulong)history.Length || history.Length > _maxPredictionTicks)
            {
                return false;
            }

            if (history.Length == 0)
            {
                return authoritativeTick == predictedTick;
            }

            if (!StatesHaveSameValue(history[0].StateBefore, authoritativeState))
            {
                return false;
            }

            for (int index = 0; index < history.Length; index++)
            {
                PredictionRecord record = history[index];
                if (record is null)
                {
                    return false;
                }

                uint expectedTick = checked(authoritativeTick + (uint)index);
                if (record.PredictedFrame.Tick != expectedTick ||
                    record.StateBefore.Tick != expectedTick ||
                    !authoritativeState.Roster.HasSameStructure(record.PredictedFrame.Roster) ||
                    !authoritativeState.Roster.HasSameStructure(record.StateBefore.Roster))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool StatesHaveSameValue(BattleState left, BattleState right)
        {
            if (left.Tick != right.Tick ||
                left.PlayerCount != right.PlayerCount ||
                !left.Roster.HasSameStructure(right.Roster))
            {
                return false;
            }

            for (int index = 0; index < left.PlayerCount; index++)
            {
                PlayerSlot slot = new PlayerSlot(index);
                PlayerState leftPlayer = left.GetPlayerState(slot);
                PlayerState rightPlayer = right.GetPlayerState(slot);
                if (leftPlayer.PositionX != rightPlayer.PositionX ||
                    leftPlayer.PositionZ != rightPlayer.PositionZ ||
                    leftPlayer.Aim != rightPlayer.Aim)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool FramesHaveSameValue(FrameData left, FrameData right)
        {
            if (left.Tick != right.Tick ||
                left.InputCount != right.InputCount ||
                !left.Roster.HasSameStructure(right.Roster))
            {
                return false;
            }

            for (int index = 0; index < left.InputCount; index++)
            {
                PlayerSlot slot = new PlayerSlot(index);
                InputFrame leftInput = left.GetInput(slot);
                InputFrame rightInput = right.GetInput(slot);
                if (leftInput.PlayerSlot != rightInput.PlayerSlot ||
                    leftInput.MoveX != rightInput.MoveX ||
                    leftInput.MoveZ != rightInput.MoveZ ||
                    leftInput.Aim != rightInput.Aim)
                {
                    return false;
                }
            }

            return true;
        }

        private sealed class PredictionRecord
        {
            public PredictionRecord(BattleState stateBefore, FrameData predictedFrame)
            {
                StateBefore = stateBefore;
                PredictedFrame = predictedFrame;
            }

            public BattleState StateBefore { get; }

            public FrameData PredictedFrame { get; }
        }
    }
}
