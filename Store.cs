using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using UniRx;

#pragma warning disable CS8632

namespace SelfStudio.ComposableArchitecture {
    public delegate Task StoreDispatcher<Action>(Action action);

    public class Store<IState, IAction, IChangeEvent> where IState : IStateCompatible<IState, IChangeEvent> {
        private readonly LogicCompatible<IState, IAction> _logic;
        private IState _previousState;
        private readonly List<IMiddlewareCompatible<IState, IAction>> _middleware;
        public readonly Subject<StateChangedInfo<IState, IChangeEvent>> stateStream;

        public Store(Func<LogicCompatible<IState, IAction>> initialLogic, List<IMiddlewareCompatible<IState, IAction>>? middleware = null) {
            _logic = initialLogic();
            _middleware = middleware ?? new List<IMiddlewareCompatible<IState, IAction>>();
            _previousState = _logic.State.Copy();
            stateStream = new Subject<StateChangedInfo<IState, IChangeEvent>>();
        }

        public async Task Send(IAction action) {
            var effect = await ProcessAction(action);
            var eAction = effect?.Action;

            if (eAction != null) {
                if (effect?.DispatchChanged == true) {
                    Dispatch(_logic.State);
                }
                var a = await eAction(new StoreDispatcher<IAction?>(OptionalSend));
                if (a != null) {
                    await Send(a);
                } else {
                    Dispatch(_logic.State);
                }
            } else {
                Dispatch(_logic.State);
            }
        }

        private async Task OptionalSend(IAction? action) {
            if (action != null) {
                await Send(action);
            }
        }

        private async Task<Effect<IAction?>?> ProcessAction(IAction action) {
            var processedAction = action;
            foreach (var middleware in _middleware) {
                processedAction = await middleware.BeforeReduce(processedAction, _logic.State);
            }
            return await _logic.Reduce(processedAction);
        }

        private void Dispatch(IState state) {
            var changes = state.Diff(_previousState);
            if (!changes.Any()) {
                return;
            }
            var info = new StateChangedInfo<IState, IChangeEvent>(_previousState, state, changes);
            stateStream.OnNext(info);
            _previousState = state.Copy();
        }

        public void Dispose() {
            _logic.Dispose();
        }
    }

    public class StateChangedInfo<IState, IChangeEvent> {
        public IState Previous { get; }
        public IState Current { get; }
        public IChangeEvent[] Changes { get; }

        public StateChangedInfo(IState previous, IState current, IChangeEvent[] changes) {
            Previous = previous;
            Current = current;
            Changes = changes;
        }
    }

    public interface IStateCompatible<T, C> where T : IStateCompatible<T, C> {
        C[] Diff(T old);
        T Copy();
    }

    public interface IMiddlewareCompatible<IState, IAction> {
        Task<IAction> BeforeReduce(IAction action, IState state);
    }
}
