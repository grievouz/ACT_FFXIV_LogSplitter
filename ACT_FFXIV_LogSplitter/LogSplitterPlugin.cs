using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Advanced_Combat_Tracker;
using System.IO;

namespace ACT_FFXIV_LogSplitter
{
    public class LogSplitterPlugin : UserControl, IActPluginV1
    {
        private StreamWriter _file;
        private string _currentZone;
        private string _currentFileName;
        private int _encounterCount;
        private int _zoneCounter;
        private readonly DateTime _initTime = DateTime.Now;
        private Label _pluginStatusText;
        private SettingsSerializer _xmlSettings;

        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            SetupPluginUi(pluginScreenSpace, pluginStatusText);
            InitializeLogFile();
            SubscribeToActEvents();
            _pluginStatusText.Text = "Initialized";
        }

        public void DeInitPlugin()
        {
            UnsubscribeFromActEvents();
            CloseCurrentLogFile();
            _pluginStatusText.Text = "Unloaded";
        }

        private void SetupPluginUi(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            _pluginStatusText = pluginStatusText;
            InitComponent();
            pluginScreenSpace.Controls.Add(this);
            pluginScreenSpace.Text = "FFXIV Log Splitter";
            Dock = DockStyle.Fill;
            _xmlSettings = new SettingsSerializer(this);
        }
        
        private void InitComponent()
        {
            var openLogsBtn = new Button();
            
            this.SuspendLayout();
            openLogsBtn.Location = new Point(8, 8);
            openLogsBtn.Name = "openLogsBtn";
            openLogsBtn.Size = new Size(180, 26);
            openLogsBtn.TabIndex = 1;
            openLogsBtn.Text = "Open Logs Directory";
            openLogsBtn.Click += OpenLogsDirectory;

            AutoScaleDimensions = new SizeF(6F, 13F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(openLogsBtn);
            Name = "FFXIVLogSplitter";
            Size = new Size(686, 384);
            ResumeLayout(false);
            PerformLayout();
        }
        
        private void OpenLogsDirectory(object sender, EventArgs e)
        {
            var directory = Path.GetDirectoryName(_currentFileName) ?? throw new InvalidOperationException();
            System.Diagnostics.Process.Start(directory);
        }

        private void InitializeLogFile()
        {
            _currentZone = ActGlobals.oFormActMain.CurrentZone;
            _currentFileName = GetLogFilename(_currentZone);
            Directory.CreateDirectory(Path.GetDirectoryName(_currentFileName) ?? throw new InvalidOperationException());
            _file = new StreamWriter(_currentFileName, true) { AutoFlush = true };
        }

        private void SubscribeToActEvents()
        {
            ActGlobals.oFormActMain.OnCombatEnd += OnCombatEnd;
            ActGlobals.oFormActMain.BeforeLogLineRead += OnLogLineRead;
        }

        private void UnsubscribeFromActEvents()
        {
            ActGlobals.oFormActMain.OnCombatEnd -= OnCombatEnd;
            ActGlobals.oFormActMain.BeforeLogLineRead -= OnLogLineRead;
        }

        private void OnCombatEnd(bool isImport, CombatToggleEventArgs encounterInfo)
        {
            if (!isImport && encounterInfo.encounter.GetEncounterSuccessLevel() > 0) _encounterCount++;
        }

        private void OnLogLineRead(bool isImport, LogLineEventArgs logInfo)
        {
            try
            {
                var line = logInfo.originalLogLine;
                var parts = line.Split('|');
                var lineId = uint.Parse(parts[0]);
                var zone = parts[3];

                if (lineId == 1 && !zone.Equals(_currentZone)) HandleZoneChange(zone, parts[1]);

                _file.WriteLine(line);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex}");
            }
        }

        private void HandleZoneChange(string newZone, string dateTimeString)
        {
            CloseCurrentLogFile();
            if (_encounterCount < 1) File.Delete(_currentFileName);
            NewLogFile(newZone);
        }

        private void NewLogFile(string newZone)
        {
            _encounterCount = 0;
            _currentZone = newZone;
            _currentFileName = GetLogFilename(_currentZone);
            Directory.CreateDirectory(Path.GetDirectoryName(_currentFileName) ?? throw new InvalidOperationException());
            _file = new StreamWriter(_currentFileName, true) { AutoFlush = true };
        }

        private void CloseCurrentLogFile()
        {
            _file?.Close();
        }

        private string GetLogFilename(string zoneName)
        {
            var logFolder = Path.GetDirectoryName(ActGlobals.oFormActMain.LogFilePath) ??
                            throw new InvalidOperationException();
            var splitFolder = $"Splitter_{_initTime:yyyy-MM-dd_h-mm-ss}";
            var zoneFile = Utils.NormalizeFilename($"{++_zoneCounter}_{zoneName}.log");
            return Path.Combine(logFolder, splitFolder, zoneFile);
        }
    }

    internal static class Utils
    {
        internal static string NormalizeFilename(string filename)
        {
            return string.Join("_",
                filename.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        }

        internal static string GetRandomString(int stringLength)
        {
            var sb = new StringBuilder();
            var numGuidsToConcat = (stringLength - 1) / 32 + 1;
            for (var i = 1; i <= numGuidsToConcat; i++) sb.Append(Guid.NewGuid().ToString("N"));

            return sb.ToString(0, stringLength);
        }
    }
}
