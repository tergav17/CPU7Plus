using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace CPU7Plus.Terminal {
    public class TerminalHandler {
        
        private volatile bool _run;

        private int _port;
        private static volatile int _number = 0;

        private volatile bool _escape = false;
        
        // Input / Output queu
        private readonly ConcurrentQueue<byte> _inputStream;
        private readonly ConcurrentQueue<byte> _outputStream;

        public TerminalHandler(int port) {
            // Create the thread
            Thread emulationThread = new Thread(new ThreadStart(this.HandleTerminal));

            // Set flags and stuff
            _run = true;
            _port = port;
            
            // Init objects
            _inputStream = new ConcurrentQueue<byte>();
            _outputStream = new ConcurrentQueue<byte>();
            
            // Start the thread up
            emulationThread.Start();
        }

        /**
         * Tells the handler thread to end its loop and quit
         */
        public void Terminate() {
            _run = false;
        }

        /**
         * Read a byte from the terminal if it exists
         */
        public byte ReadByte() {
            if (_inputStream.TryDequeue(out byte b)) {
                return b;
            } else {
                return 0;
            }
        }

        /**
         * Write a byte to the terminal
         */
        public void WriteByte(byte b) {
            b = Convert.ToByte(b & 0x7F);
            
            if (_escape) {
              // Handle escape code removal
              if (b != 28) {
                  _outputStream.Enqueue(0x1B);
                  _outputStream.Enqueue(b);
              }
              _escape = false;
            } else if (b == 27) {
                _escape = true;
            } else _outputStream.Enqueue(Convert.ToByte(b));
        }

        /**
         * Returns try if there is a byte to read
         */
        public bool HasByte() {
            return !_inputStream.IsEmpty;
        }

        /**
         * Terminal handler thread
         */
        private void HandleTerminal() {

            // ReSharper disable once NonAtomicCompoundOperator
            Console.Write("Starting terminal thread #" + _number++ + "...\n");
            
            IPHostEntry ipHost = Dns.GetHostEntry("localhost");
            IPAddress ipAddr = ipHost.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddr, _port);
            
            Socket listener = new Socket(ipAddr.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);
 
            try {
                Console.Write("Binding listener to port " + _port + "...\n");
                
                listener.Bind(localEndPoint);
                
                listener.Listen(10);
            } catch (Exception e) {
                // Ignored lmao
                Console.Write("Failed to bind to port " + _port + "!\n");
                Console.Write(e + "\n");
                _run = false;
            }

            while (_run) {
                while (!listener.Poll(10, SelectMode.SelectRead) && _run) Thread.Sleep(10);
                    
                if (!_run)
                    break;

                Socket clientSocket = listener.Accept();
                
                // Data buffer
                byte[] bytes = new Byte[1024];

                Console.Write("Connected to " + _port + "\n");
                
                while (_run) {

                    // Send everything on the output stream
                    if (_outputStream.TryDequeue(out byte initByte)) {

                        List<byte> toSend = new List<byte>();
                        toSend.Add(initByte);
                        
                        while (_outputStream.TryDequeue(out byte oByte)) {
                            toSend.Add(oByte);
                        }

                        //Console.WriteLine("Sending: " + oByte);
                        clientSocket.Send(toSend.ToArray());
                    }

                    // Poll to see if there is anything that needs to be read
                    if (!clientSocket.Poll(1000, SelectMode.SelectRead)) continue;
                    
                    int numByte = clientSocket.Receive(bytes);

                    // Enqueue everything come in from the terminal
                    for (int i = 0; i < numByte; i++) {
                        _inputStream.Enqueue(bytes[i]);
                    }

                    if (numByte == 0) break;
                }

                clientSocket.Close();


                Console.Write("Port " + _port + " disconnected\n");
                
                Thread.Sleep(10);
            }
            
            listener.Close();
            
            Console.Write("Stopping terminal thread...\n");


        }
    }
}