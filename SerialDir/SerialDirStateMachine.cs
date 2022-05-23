using System;
using System.Collections.Generic;
using System.IO;

namespace CPU7Plus.SerialDir {
    public class SerialDirStateMachine {
        
        // CRC polynomial for CRC-8-Bluetooth implementation
        private const int PolyMask = 0b110100111;

        private enum State {
            WaitCommand,
            GetBlockHigh,
            GetBlockLow,
            ReadData,
            ReadCheck
        }

        private State _state;
        private int _command;
        private int _crc;
        private int _block;
        private int _bytesToRead;
        private int _bufferIndex;
        private byte[] _buffer;
        private FileStream? _openFile;

        public SerialDirStateMachine() {
            Reset();
        }

        public List<byte> ReceiveByte(byte b) {
            List<byte> response = new List<byte>();

            if (_state == State.WaitCommand) {
                
                // Start the CRC
                ResetCyclicCheck();
                UpdateCyclicCheck(b);
                
                // Set the command
                _command = b;
                
                if (b == 1) {
                    // [B]ootstrap
                } else if (b == 2) {
                    // [O]pen File
                } else if (b >= 3 && b <= 8) {
                    // [C]lose File
                    _state = State.GetBlockHigh;
                }
            } 
            
            else if (_state == State.GetBlockHigh) {
                UpdateCyclicCheck(b);
                
                // Shift byte and set it to block
                _block = b << 8;
                _state = State.GetBlockLow;
            }
            
            else if (_state == State.GetBlockLow) {
                UpdateCyclicCheck(b);
                
                // Add byte to block #
                _block |= b;
                
                // Decide where to go next depending on command
                if (_command == 2 || _command == 4 || _command == 5) {
                    _state = State.ReadData;
                    _bytesToRead = 16;
                } else if (_command == 8) {
                    _state = State.ReadData;
                    _bytesToRead = 256;
                } else {
                    _state = State.ReadCheck;
                }

                _bufferIndex = 0;

            } 
            
            else if (_state == State.ReadData) {
                UpdateCyclicCheck(b);

                // Write to buffer
                _buffer[_bufferIndex] = b;
                _bytesToRead--;

                // If there are no more bytes to read, go on to crc check
                if (_bytesToRead <= 0) _state = State.ReadCheck;
            } 
            
            else if (_state == State.ReadCheck) {
                // Check and see if there is an error
                bool error = b != _crc;

                // Go back to waiting for a command
                _state = State.WaitCommand;
            }

            return response;
        }
        
        /**
         * Reset the state machine to the command state
         */
        public void Reset() {
            _state = State.WaitCommand;
            
            _openFile?.Close();
            _openFile = null;
            
            _command = 0;
            _block = 0;
            _bytesToRead = 0;
            _bufferIndex = 0;
            _buffer = new byte[256];
            
            
            ResetCyclicCheck();
        }

        /**
         * Reset the CRC calculator
         */
        private void ResetCyclicCheck() {
            _crc = 251;
        }
        
        /**
         * Generates a CRC value based on an incoming message
         * Lowest index is assumed to be the first byte received
         */
        public void UpdateCyclicCheck(byte b) {

            // Create a copy of this byte for usage
            int by = b;
            
            for (int i = 0; i < 8; i++) {
                // Get the highest bit the the byte
                int last = by & 0x80;
                if (last != 0) last = 1;
                
                // Shift the current byte up
                by = by << 1;

                // Shift it onto the CRC
                _crc = (_crc << 1) | last;

                // Check bit 9 to see if it is on, if so invert CRC bits
                if ((_crc & 0x100) != 0) {
                    // XOR polynomial mask onto CRC
                    
                    _crc ^= PolyMask;
                }
            }

            // Just in case, slice up the CRC
            _crc = _crc & 0xFF;
        }


    }
}