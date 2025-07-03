using System;
using System.Windows.Input;

namespace ManutMap.Helpers
{
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _act;
        public RelayCommand(Action<T?> act) => _act = act;
        public bool CanExecute(object? _) => true;
        public void Execute(object? parameter) => _act((T?)parameter);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
