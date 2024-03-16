#pragma warning disable CS8632

using System;
using System.Threading.Tasks;

namespace SelfStudio.ComposableArchitecture {
    public class Effect<IAction> {
        public bool DispatchChanged { get; }
        public Func<StoreDispatcher<IAction>, Task<IAction?>>? Action { get; }

        public Effect(Func<StoreDispatcher<IAction>, Task<IAction?>>? action = null, bool dispatchChanged = false) {
            Action = action;
            DispatchChanged = dispatchChanged;
        }

        public static Effect<IAction?> Create(IAction? action = default, bool dispatchChanged = false, TimeSpan? delay = null) {
            return new Effect<IAction?>(async dispatcher => {
                if (delay != null) {
                    await Task.Delay(delay.Value);
                }
                return action;
            }, dispatchChanged);
        }
    }
}
