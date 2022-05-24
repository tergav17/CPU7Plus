using System;
using System.Collections.Concurrent;
using System.Threading;
using Avalonia.Threading;
using CPU7Plus.SerialDir;
using CPU7Plus.Terminal;
using CPU7Plus.Views;
using JetBrains.Annotations;

namespace CPU7Plus.Emulation {
    public class EmulationHandler {

        private volatile bool _run;
        private volatile bool _execute;
        private volatile bool _doUpdate;
        private volatile int _speed;
        private int _cooldown;

        private EmulationCore _emulator;
        private EmulationContext _context;
        private TerminalHandler _terminalConsole;
        private MemoryMappedAdapter _adapter;

        private MainWindow _window;
        private MemoryViewer _viewer;
        private DiagnosticPanel _diagnostic;

        private ConcurrentQueue<Command> _commandQueue;

        public EmulationHandler(MainWindow window, MemoryViewer viewer, BinaryLoader loader, DiagnosticPanel diagnostic) {
            // Prepare to create emulation thread
            _run = true;
            _execute = false;
            _doUpdate = true;
            _speed = 1000;
            _cooldown = 0;
            _commandQueue = new ConcurrentQueue<Command>();
            
            // Save passed objects
            _window = window;
            _viewer = viewer;
            _diagnostic = diagnostic;

            // Create terminal handler
            if (TerminalBlock.ConsoleTerminal == null) {
                _terminalConsole = new TerminalHandler(0);
            } else {
                _terminalConsole = TerminalBlock.ConsoleTerminal;
            }

            // Create emulator
            _adapter = new MemoryMappedAdapter(_terminalConsole, _diagnostic);
            _context = new EmulationContext(_adapter);
            _emulator = new EmulationCore(_context);
            
            // Set context
            _viewer.Context = _context;
            _viewer.Handler = this;
            _viewer.UpdateDisplay();
            _diagnostic.Handler = this;
            loader.Handler = this;

            // Create the thread
            Thread emulationThread = new Thread(new ThreadStart(this.HandleEmulation));
            
            // Start the thread up
            emulationThread.Start();
        }

        /**
         * Tells the handler thread to end its loop and quit
         */
        public void Terminate() {
            _run = false;
            _terminalConsole.Terminate();
        }

        /**
         * Resets the memory mapped adapter
         */
        public void ResetMemoryMappedAdapter() {
            _adapter.Reset();
        }
        
        /**
         * Start processor execution
         */
        public void StartExecution() {
            _doUpdate = true;
            _execute = true;
        }

        /**
         * Stop processor execution
         */
        public void StopExecution() {
            _execute = false;
            IssueCommand(new Command(3));
        }

        /**
         * Issues a command to the emulator thread
         */
        public void IssueCommand(Command command) {
            _commandQueue.Enqueue(command);
        }

        [NotNull]
        public EmulationContext Context {
            get => _context;
            set => _context = value ?? throw new ArgumentNullException(nameof(value));
        }

        /**
         * Emulation handler thread
         */
        private void HandleEmulation() {

            Console.Write("Starting emulation thread...\n");
            
            while (_run) {
                // Do emulation stuff
                
                Thread.Sleep(3);
                if (_doUpdate) {
                    _doUpdate = false;
                    ViewUpdater.UpdateContext(_context, _window);
                }

                if (_execute) {
                    for (int i = 0; i < _speed && _execute; i++)
                        if (!_emulator.Step()) {
                            _cooldown = 0;
                            _execute = false;
                        }

                    if (_cooldown <= 0) {
                        Dispatcher.UIThread.Post(() => {
                            ViewUpdater.UpdateView(_context, _window);
                            _viewer.UpdateDisplay();
                        });
                        _cooldown = 5;
                    } else _cooldown--;
                }

                // Check to see if any commands have been received
                
                if (!_commandQueue.IsEmpty) {
                    
                    // Dequeue all commands
                    while ( _commandQueue.TryDequeue(out Command? command)) {

                        if (command.Type == 0) {
                            // Single step command
                            ViewUpdater.UpdateContext(_context, _window);

                            // Step processor
                            _emulator.Step();

                            // Update all visuals
                            Dispatcher.UIThread.Post(() => {
                                ViewUpdater.UpdateView(_context, _window);
                                _viewer.UpdateDisplay();
                            });
                        }
                        else if (command.Type == 1 || command.Type == 2) {
                            // Write memory command

                            // Read the command and write the byte
                            try {
                                _context.Core[command.Address] = Convert.ToByte(command.Data);
                            }
                            catch (FormatException) {
                                // Do nothing lmao
                            }
                            catch (NullReferenceException) {
                                // Still nothing, fool!
                                // (I am good at programming)
                            }

                            // Update all visuals
                            if (command.Type == 1) {
                                Dispatcher.UIThread.Post(() => {
                                    ViewUpdater.UpdateView(_context, _window);
                                    _viewer.UpdateDisplay();
                                });
                            }
                        } else if (command.Type == 3) {
                            // Update all visuals and nothing else
                            
                            Dispatcher.UIThread.Post(() => {
                                ViewUpdater.UpdateView(_context, _window); 
                                _viewer.UpdateDisplay();
                            });

                        } else if (command.Type == 4) {
                            Context.Reset();
                            
                            Dispatcher.UIThread.Post(() => {
                                ViewUpdater.UpdateView(_context, _window);
                                _viewer.UpdateDisplay();
                            });
                        }
                    }
                }
            }
            
            Console.Write("Stopping emulation thread...\n");

        }
        
        
    }
}