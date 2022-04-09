using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CPU7Plus.Emulation;
using JetBrains.Annotations;

namespace CPU7Plus.Views {
    public partial class MemoryViewer : Window {

        private volatile bool _canClose;

        private EmulationContext? _context;

        public MemoryViewer() {

            InitializeComponent();

#if DEBUG
            this.AttachDevTools();
#endif


            // Setup some flags
            _canClose = false;

            // Make closing simply hide the window
            this.Closing += (s, e) => {
                if (!_canClose) {
                    e.Cancel = true;
                    this.Hide();
                }
            };

            // Manual init of all controls because Avalonia does not feel like doing it for us
            MemoryInput = this.FindControl<TextBox>("MemoryInput");
            
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

        }
        
        /**
         * Permits the window to be closed
         */
        public void AllowClose() {
            _canClose = true;
        }
        
        /**
         * Keys down event
         */
        public void OnKeyDown(object sender, KeyEventArgs e) {
            Console.Write(e.Key + ", " + MemoryInput.CaretIndex + "\n");

            MemoryInput.CaretIndex++;
            
            e.Handled = true;
        }

        public void UpdateDisplay() {
            DisplayBuffer(0);
        }

        /**
         *  Displays the memory in context
         */
        public void DisplayBuffer(int block) {
            string contents = "";
            string ascii = "";

            if (_context == null) return;
            
            for (int i = 4096 * block; i < 4096; i++) {
                if (i % 16 == 0) {
                    contents = contents + i.ToString("X4") + ": ";
                }

                contents = contents + _context.Core[i].ToString("X2") + " ";

                if (_context.Core[i] >= 0x20 && _context.Core[i] < 0x7F) {
                    ascii = ascii + System.Text.Encoding.ASCII.GetString(new[]{_context.Core[i]});
                } else {
                    ascii = ascii + ".";
                }

                if (i % 16 == 15) {
                    contents = contents + " |  " + ascii + "\n";
                    ascii = "";
                }
            }

            MemoryInput.Text = contents;
        }

        [NotNull]
        public EmulationContext? Context {
            get => _context;
            set => _context = value ?? throw new ArgumentNullException(nameof(value));
        }
    }
    
    
}