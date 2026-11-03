using System.Windows;
using System.Windows.Input;

namespace zPlayer.App.Commands
{
    public sealed class RelayCommand<T> : ICommand where T : class
    {
        private readonly Action<T> execute;
        private readonly Predicate<T>? canExecute;

        public RelayCommand(Action<T> execute, Predicate<T>? canExecute = null) {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) {
            return parameter is T value && (canExecute?.Invoke(value) ?? true);
        }

        public void Execute(object? parameter) {
            if (parameter is T value && CanExecute(value)) execute(value);
        }

        public event EventHandler? CanExecuteChanged {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
