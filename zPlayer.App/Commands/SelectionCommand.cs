using System;
using System.Collections;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;

namespace zPlayer.App.Commands
{
    public sealed class SelectionCommand<T> : ICommand where T : class
    {
        private readonly Action<T[]> execute;

        public SelectionCommand(Action<T[]> execute) {
            this.execute = execute;
        }

        private static T[] GetSelection(object? parameter) {
            IList? selectedItems = parameter switch {
                DataGrid grid => grid.SelectedItems,
                ListBox list => list.SelectedItems,
                _ => null
            };

            if (parameter is not ItemsControl control || selectedItems == null) return [];
            return control.Items.OfType<T>().Where(item => selectedItems.Contains(item)).ToArray();
        }

        public bool CanExecute(object? parameter) {
            return GetSelection(parameter).Length > 0;
        }

        public void Execute(object? parameter) {
            T[] selection = GetSelection(parameter);
            if (selection.Length > 0) execute(selection);
        }

        public event EventHandler? CanExecuteChanged {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}