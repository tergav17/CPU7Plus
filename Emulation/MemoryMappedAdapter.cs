using CPU7Plus.Terminal;

namespace CPU7Plus.Emulation {
    public class MemoryMappedAdapter {
        
        private TerminalHandler _consoleTerminal;
        
        public MemoryMappedAdapter(TerminalHandler consoleTerminal) {
            _consoleTerminal = consoleTerminal;
        }

        /**
         * Write to MMIO region
         */
        public void WriteMapped(int addr, byte b) {

            if (addr == 0xF201) {
                // Console terminal write
                _consoleTerminal.WriteByte(b);
            }

        }

        /**
         * Read from MMIO region
         */
        public byte ReadMapped(int addr) {

            if (addr == 0xF201) {
                // Console terminal read
                return _consoleTerminal.ReadByte();
            }

            return 0;
        }

    }
}