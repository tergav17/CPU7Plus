using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CPU7Plus.Emulation;
using CPU7Plus.ViewModels;

namespace CPU7Plus.Views {
    public partial class MainWindow : Window {

        // Instances
        private EmulationHandler _emulationHandler;
        private MemoryViewer _viewer;
        private DiagnosticPanel _diagnostic;
        private BinaryLoader _loader;
        
        public MainWindow() {
            InitializeComponent();

            // Initialize views
           _viewer = new MemoryViewer() {
               DataContext = new MemoryViewerViewModel(),
           };

           _loader = new BinaryLoader() {
               DataContext = new BinaryLoaderViewModel(),
           };
           
           _diagnostic = new DiagnosticPanel() {
               DataContext = new DiagnosticPanelViewModel(),
           };
           
           // Start emulator
           _emulationHandler = new EmulationHandler(this, _viewer, _loader, _diagnostic);

        }

        /**
         * On close event
         */
        public void OnClose(object? sender, EventArgs e) {
            
            // Terminate handler
            _emulationHandler.Terminate();

            // Close all additional windows
            _viewer.AllowClose();
            _viewer.Close();
            
            _loader.AllowClose();
            _loader.Close();
            
            _diagnostic.AllowClose();
            _diagnostic.Close();

            this.Hide();
            
            Thread.Sleep(500);
            System.Environment.Exit(0);  
        }

        /**
         * Button handler for single stepping
         */
        public void OnStepButton(object sender, RoutedEventArgs e) {
            _emulationHandler.IssueCommand(new Command(0));
            
            _viewer.UpdateDisplay();
        }

        /**
         * Button handler for running the processor
         */
        public void OnRunButton(object sender, RoutedEventArgs e) {
            _emulationHandler.StartExecution();
            
            ViewUpdater.DisableInputs(this);
        }
        
        /**
         * Button handler for stopping the processor
         */
        public void OnStopButton(object sender, RoutedEventArgs e) {
            _emulationHandler.StopExecution();
            
            ViewUpdater.EnableInputs(this);
            
            _viewer.UpdateDisplay();
        }
        
        /**
         * Menu button handler for stopping the processor
         */
        public void OnExitButton(object sender, RoutedEventArgs e) {
            this.Close();
        }
        
        /**
         * Menu button handler for opening the memory viewer
         */
        public void OnMemoryViewerButton(object sender, RoutedEventArgs e) {
            _viewer.Show();
        }

        /**
         * Menu button handler for opening the binary loader
         */
        private void OnBinaryLoaderButton(object? sender, RoutedEventArgs e) {
            _loader.Show();
        }

        /**
         * Menu button handler for opening the diagnostic panel
         */
        private void OnDiagnosticPanelButton(object? sender, RoutedEventArgs e) {
            _diagnostic.Show();
        }

        /**
         * Restart the processor
         */
        private void OnSelect(object? sender, RoutedEventArgs e) {
            _emulationHandler.IssueCommand(new Command(4));
            
            _emulationHandler.StartExecution();
            
            ViewUpdater.DisableInputs(this);
        }
    }
}