using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CPU7Plus.Emulation;
using Microsoft.CodeAnalysis;

namespace CPU7Plus.Views {
    
    public partial class DiagnosticPanel : Window {
        
        private volatile bool _canClose;

        private volatile int _display = 0;
        private volatile bool _displayEnabled = false;
        private volatile bool _dotOne = false;
        private volatile bool _dotTwo = false;
        private volatile bool _dotThree = false;
        private volatile bool _dotFour = false;
        
        public DiagnosticPanel() {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            
                        
            // Setup some flags and values
            _canClose = false;
            
            // Make closing simply hide the window
            this.Closing += (s, e) => {
                if (!_canClose) {
                    e.Cancel = true;
                    this.Hide();
                }
            };
            
            TopIndicator = this.FindControl<TextBlock>("TopIndicator");
            BottomIndicator = this.FindControl<TextBlock>("BottomIndicator");
            
            SenseOne = this.FindControl<CheckBox>("SenseOne");
            SenseTwo = this.FindControl<CheckBox>("SenseTwo");
            SenseThree = this.FindControl<CheckBox>("SenseThree");
            SenseFour = this.FindControl<CheckBox>("SenseFour");

            TestNumber = this.FindControl<TextBox>("TestNumber");
            
            SenseButton = this.FindControl<Button>("SenseButton");

            Update();
        }
        
        /**
         * Permits the window to be closed
         */
        public void AllowClose() {
            _canClose = true;
        }

        public byte GetDip() {

            byte output;
            
            // Register WX
            try {
                int value = int.Parse(TestNumber.Text, System.Globalization.NumberStyles.HexNumber);

                output = Convert.ToByte(value);
            }
            catch (FormatException) {
                output = 0;
            }

            bool buttonDown = false;
            
            Dispatcher.UIThread.InvokeAsync(() => {
                buttonDown = SenseButton.IsPressed;
            }, Avalonia.Threading.DispatcherPriority.Normal).Wait();

            output = Convert.ToByte(output | (buttonDown ? 0x00 : 0x80));

            return output;
        }

        /**
         * Updates the display to the correct status
         */
        public void Update() {
            Dispatcher.UIThread.Post(() => {
                if (_displayEnabled) {
                    TopIndicator.Text = (_dotFour ? "." : " ") + ((_display & 0xF0) >> 4).ToString("X1") + (_dotThree ? "." : " ");
                    BottomIndicator.Text = (_dotTwo ? "." : " ") + (_display & 0x0F).ToString("X1") + (_dotOne ? "." : " ");
                } else {
                    TopIndicator.Text = " - ";
                    BottomIndicator.Text = " - ";
                }
            });
        }

        public bool GetSsw(int num) {
            if (num == 0 && SenseOne.IsChecked != null) {
                return (bool) SenseOne.IsChecked;
            } else if (num == 1 && SenseTwo.IsChecked != null) {
                return (bool) SenseTwo.IsChecked;
            } else if (num == 2 && SenseThree.IsChecked != null) {
                return (bool) SenseThree.IsChecked;
            } else if (num == 3 && SenseFour.IsChecked != null) {
                return (bool) SenseFour.IsChecked;
            } else {
                return false;
            }
        }

        public int Display {
            get => _display;
            set => _display = value;
        }

        public bool DisplayEnabled {
            get => _displayEnabled;
            set => _displayEnabled = value;
        }

        public bool DotOne {
            get => _dotOne;
            set => _dotOne = value;
        }

        public bool DotTwo {
            get => _dotTwo;
            set => _dotTwo = value;
        }

        public bool DotThree {
            get => _dotThree;
            set => _dotThree = value;
        }

        public bool DotFour {
            get => _dotFour;
            set => _dotFour = value;
        }

        public EmulationHandler? Handler { get; set; }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnRemove(object? sender, RoutedEventArgs e) {
            if (Handler != null) Handler.Context.DiagnosticMode = false;
        }
        
        private void OnInstall(object? sender, RoutedEventArgs e) {
            if (Handler != null) Handler.Context.DiagnosticMode = true;
        }
    }
}