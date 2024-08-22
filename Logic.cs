#pragma warning disable CS8632

using System.Threading.Tasks;

namespace SelfStudio.ComposableArchitecture {

    public interface ILogicCompatible<IState, IAction> {
        public IState State { get; }
        public abstract Task<Effect<IAction?>?> Reduce<T>(T action) where T : IAction;
        public void Dispose();
    }
}
