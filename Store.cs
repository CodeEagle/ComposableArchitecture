using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#pragma warning disable CS8632,CS0618

namespace SelfStudio.ComposableArchitecture {
    public delegate Task StoreDispatcher<Action>(Action action);
    public interface IComposableAction { }

    public class Store<IState, IAction, IChangeEvent> where IState : IStateCompatible<IState, IChangeEvent> {
        private readonly ILogicCompatible<IState, IAction> _logic;
        private IState _previousState;
        private readonly List<IMiddlewareCompatible<IState, IAction>> _middleware;
        [Obsolete("stateStream will be removed in a future version. Use stateStream instead.")]
        public readonly Stream<StateChangedInfo<IState, IChangeEvent>> stateStream;
        public Action<StateChangedInfo<IState, IChangeEvent>> OnStateChanged;

        public Store(Func<ILogicCompatible<IState, IAction>> initialLogic, List<IMiddlewareCompatible<IState, IAction>>? middleware = null) {
            _logic = initialLogic();
            _middleware = middleware ?? new List<IMiddlewareCompatible<IState, IAction>>();
            _previousState = _logic.State.Copy();
            stateStream = new Stream<StateChangedInfo<IState, IChangeEvent>>();
            OnStateChanged = (_) => { };
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
            if (changes == null || changes.Length == 0) {
                return;
            }
            var info = new StateChangedInfo<IState, IChangeEvent>(_previousState.Copy(), state, changes);
            stateStream.OnNext(info);
            OnStateChanged(info);
            _previousState = state.Copy();
        }

        public void Dispose() {
            _logic.Dispose();
        }
    }

    public class StateChangedInfo<IState, IChangeEvent> {
        public long Timestamp { get; }
        public IState Previous { get; }
        public IState Current { get; }
        public IChangeEvent[] Changes { get; }

        public StateChangedInfo(IState previous, IState current, IChangeEvent[] changes) {
            Previous = previous;
            Current = current;
            Changes = changes;
            Timestamp = (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
        }
    }

    public interface IStateCompatible<T, C> where T : IStateCompatible<T, C> {
        C[] Diff(T old);
        T Copy();
    }
    public interface IMiddlewareCompatible<IState, IAction> {
        Task<IAction> BeforeReduce(IAction action, IState state);
    }


    public class Stream<T> : IObservable<T> {
        private readonly List<IObserver<T>> _observers = new();

        public IDisposable Subscribe(IObserver<T> observer) {
            if (!_observers.Contains(observer))
                _observers.Add(observer);
            return new Unsubscriber(_observers, observer);
        }

        public void OnNext(T value) {
            foreach (var observer in _observers)
                observer.OnNext(value);
        }

        public void Complete() {
            foreach (var observer in _observers.ToArray())
                if (_observers.Contains(observer))
                    observer.OnCompleted();

            _observers.Clear();
        }


        private class Unsubscriber : IDisposable {
            private readonly List<IObserver<T>> _observers;
            private readonly IObserver<T> _observer;

            public Unsubscriber(List<IObserver<T>> observers, IObserver<T> observer) {
                _observers = observers;
                _observer = observer;
            }

            public void Dispose() {
                if (_observer != null && _observers.Contains(_observer))
                    _observers.Remove(_observer);
            }
        }
    }
}
