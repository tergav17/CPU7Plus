using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CPU7Plus.Terminal;
using CPU7Plus.Views;
using JetBrains.Annotations;

namespace CPU7Plus.Emulation {
    public class MemoryMappedAdapter {
        
        private TerminalHandler _consoleTerminal;
        private DiagnosticPanel _diagnostic;
        
        public MemoryMappedAdapter(TerminalHandler consoleTerminal, DiagnosticPanel diagnostic) {
            _consoleTerminal = consoleTerminal;
            _diagnostic = diagnostic;
        }
        


        /**
         * Write to MMIO region
         */
        public void WriteMapped(int addr, byte b) {

            if (addr == 0xF106) {
                // Blank Hex Displays
                _diagnostic.DisplayEnabled = true;
            } else if (addr == 0xF107) {
                // Blank Hex Displays
                _diagnostic.DisplayEnabled = false;
            } else if (addr == 0xF108) {
                // Set Point 1
                _diagnostic.DotOne = true;
                _diagnostic.Update();
            } else if (addr == 0xF109) {
                // Clear Point 1
                _diagnostic.DotOne = false;
                _diagnostic.Update();
            } else if (addr == 0xF10A) {
                // Set Point 2
                _diagnostic.DotTwo = true;
                _diagnostic.Update();
            } else if (addr == 0xF10B) {
                // Clear Point 2
                _diagnostic.DotTwo = false;
                _diagnostic.Update();
            } else if (addr == 0xF10C) {
                // Set Point 3
                _diagnostic.DotThree = true;
                _diagnostic.Update();
            } else if (addr == 0xF10D) {
                // Clear Point 3
                _diagnostic.DotThree = false;
                _diagnostic.Update();
            } else if (addr == 0xF10E) {
                // Set Point 4
                _diagnostic.DotFour = true;
                _diagnostic.Update();
            } else if (addr == 0xF10F) {
                // Clear Point 4
                _diagnostic.DotFour = false;
                _diagnostic.Update();
            } else if (addr == 0xF110) {
                // Set Display
                _diagnostic.Display = b;
                _diagnostic.Update();
            } else if (addr == 0xF201) {
                // Console terminal write
                _consoleTerminal.WriteByte(b);
            }

        }

        /**
         * Read from MMIO region
         */
        public byte ReadMapped(int addr) {

            if (addr == 0xF110) {
                // Set dip switch
                return _diagnostic.GetDip();
            }

            if (addr == 0xF200) {
                // Read console terminal status
                return Convert.ToByte(0b01000000 | (_consoleTerminal.HasByte() ? 0x80 : 0x00));
            }

            if (addr == 0xF201) {
                // Console terminal read
                return _consoleTerminal.ReadByte();
            }

            return 0;
        }

        [NotNull]
        public DiagnosticPanel DiagnosticPanel {
            get => _diagnostic;
            set => _diagnostic = value ?? throw new ArgumentNullException(nameof(value));
        }
    }
}