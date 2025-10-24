using System;
using System.Windows.Input;

namespace DromHub.Utils
{
    /// <summary>
    /// Класс RelayCommand отвечает за логику компонента RelayCommand.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public event EventHandler CanExecuteChanged;
        /// <summary>
        /// Конструктор RelayCommand инициализирует экземпляр класса.
        /// </summary>

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        /// <summary>
        /// Метод CanExecute выполняет основную операцию класса.
        /// </summary>

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
        /// <summary>
        /// Метод Execute выполняет основную операцию класса.
        /// </summary>

        public void Execute(object parameter) => _execute();
        /// <summary>
        /// Метод RaiseCanExecuteChanged выполняет основную операцию класса.
        /// </summary>

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    /// <summary>
    /// Класс RelayCommand отвечает за логику компонента RelayCommand.
    /// </summary>

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public event EventHandler CanExecuteChanged;
        /// <summary>
        /// Конструктор RelayCommand инициализирует экземпляр класса.
        /// </summary>

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        /// <summary>
        /// Метод CanExecute выполняет основную операцию класса.
        /// </summary>

        public bool CanExecute(object parameter) => _canExecute?.Invoke((T)parameter) ?? true;
        /// <summary>
        /// Метод Execute выполняет основную операцию класса.
        /// </summary>

        public void Execute(object parameter) => _execute((T)parameter);
        /// <summary>
        /// Метод RaiseCanExecuteChanged выполняет основную операцию класса.
        /// </summary>

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}