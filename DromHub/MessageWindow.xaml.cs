using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace DromHub
{
    /// <summary>
    /// Класс MessageWindow отвечает за логику компонента MessageWindow.
    /// </summary>
    public sealed partial class MessageWindow : Window
    {
        /// <summary>
        /// Конструктор MessageWindow инициализирует экземпляр класса.
        /// </summary>
        public MessageWindow(string message)
        {
            this.InitializeComponent();
            MessageText.Text = message;
        }
    }
}
