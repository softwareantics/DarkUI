using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using DarkUI.Docking;
using DarkUI.Forms;
using DarkUI.Win32;

namespace Example
{
    public partial class MainForm : DarkForm
    {
        private readonly DockConsole _dockConsole;

        private readonly DockHistory _dockHistory;

        private readonly DockLayers _dockLayers;

        private readonly DockProject _dockProject;

        private readonly DockProperties _dockProperties;

        private readonly List<DarkDockContent> _toolWindows = new List<DarkDockContent>();

        public MainForm()
        {
            this.InitializeComponent();

            // Add the control scroll message filter to re-route all mousewheel events to the control the user is currently hovering over with their cursor.
            Application.AddMessageFilter(new ControlScrollFilter());

            // Add the dock content drag message filter to handle moving dock content around.
            Application.AddMessageFilter(this.DockPanel.DockContentDragFilter);

            // Add the dock panel message filter to filter through for dock panel splitter input before letting events pass through to the rest of the application.
            Application.AddMessageFilter(this.DockPanel.DockResizeFilter);

            // Hook in all the UI events manually for clarity.
            this.HookEvents();

            // Build the tool windows and add them to the dock panel
            this._dockProject = new DockProject();
            this._dockProperties = new DockProperties();
            this._dockConsole = new DockConsole();
            this._dockLayers = new DockLayers();
            this._dockHistory = new DockHistory();

            // Add the tool windows to a list
            this._toolWindows.Add(this._dockProject);
            this._toolWindows.Add(this._dockProperties);
            this._toolWindows.Add(this._dockConsole);
            this._toolWindows.Add(this._dockLayers);
            this._toolWindows.Add(this._dockHistory);

            // Deserialize if a previous state is stored
            if (File.Exists("dockpanel.config"))
            {
                this.DeserializeDockPanel("dockpanel.config");
            }
            else
            {
                // Add the tool window list contents to the dock panel
                foreach (DarkDockContent toolWindow in this._toolWindows)
                {
                    this.DockPanel.AddContent(toolWindow);
                }

                // Add the history panel to the layer panel group
                this.DockPanel.AddContent(this._dockHistory, this._dockLayers.DockGroup);
            }

            // Check window menu items which are contained in the dock panel
            this.BuildWindowMenu();

            // Add dummy documents to the main document area of the dock panel
            this.DockPanel.AddContent(new DockDocument("Document 1", null));
            this.DockPanel.AddContent(new DockDocument("Document 2", null));
            this.DockPanel.AddContent(new DockDocument("Document 3", null));
        }

        private void About_Click(object sender, EventArgs e)
        {
            var about = new DialogAbout();
            about.ShowDialog();
        }

        private void BuildWindowMenu()
        {
            this.mnuProject.Checked = this.DockPanel.ContainsContent(this._dockProject);
            this.mnuProperties.Checked = this.DockPanel.ContainsContent(this._dockProperties);
            this.mnuConsole.Checked = this.DockPanel.ContainsContent(this._dockConsole);
            this.mnuLayers.Checked = this.DockPanel.Contains(this._dockLayers);
            this.mnuHistory.Checked = this.DockPanel.Contains(this._dockHistory);
        }

        private void Close_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Console_Click(object sender, EventArgs e)
        {
            this.ToggleToolWindow(this._dockConsole);
        }

        private void DeserializeDockPanel(string path)
        {
            DockPanelState state = SerializerHelper.Deserialize<DockPanelState>(path);
            this.DockPanel.RestoreDockPanelState(state, this.GetContentBySerializationKey);
        }

        private void Dialog_Click(object sender, EventArgs e)
        {
            var test = new DialogControls();
            test.ShowDialog();
        }

        private void DockPanel_ContentAdded(object sender, DockContentEventArgs e)
        {
            if (this._toolWindows.Contains(e.Content))
            {
                this.BuildWindowMenu();
            }
        }

        private void DockPanel_ContentRemoved(object sender, DockContentEventArgs e)
        {
            if (this._toolWindows.Contains(e.Content))
            {
                this.BuildWindowMenu();
            }
        }

        private DarkDockContent GetContentBySerializationKey(string key)
        {
            foreach (DarkDockContent window in this._toolWindows)
            {
                if (window.SerializationKey == key)
                {
                    return window;
                }
            }

            return null;
        }

        private void History_Click(object sender, EventArgs e)
        {
            this.ToggleToolWindow(this._dockHistory);
        }

        private void HookEvents()
        {
            FormClosing += this.MainForm_FormClosing;

            this.DockPanel.ContentAdded += this.DockPanel_ContentAdded;
            this.DockPanel.ContentRemoved += this.DockPanel_ContentRemoved;

            this.mnuNewFile.Click += this.NewFile_Click;
            this.mnuClose.Click += this.Close_Click;

            this.btnNewFile.Click += this.NewFile_Click;

            this.mnuDialog.Click += this.Dialog_Click;

            this.mnuProject.Click += this.Project_Click;
            this.mnuProperties.Click += this.Properties_Click;
            this.mnuConsole.Click += this.Console_Click;
            this.mnuLayers.Click += this.Layers_Click;
            this.mnuHistory.Click += this.History_Click;

            this.mnuAbout.Click += this.About_Click;
        }

        private void Layers_Click(object sender, EventArgs e)
        {
            this.ToggleToolWindow(this._dockLayers);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.SerializeDockPanel("dockpanel.config");
        }

        private void NewFile_Click(object sender, EventArgs e)
        {
            var newFile = new DockDocument("New document", null);
            this.DockPanel.AddContent(newFile);
        }

        private void Project_Click(object sender, EventArgs e)
        {
            this.ToggleToolWindow(this._dockProject);
        }

        private void Properties_Click(object sender, EventArgs e)
        {
            this.ToggleToolWindow(this._dockProperties);
        }

        private void SerializeDockPanel(string path)
        {
            DockPanelState state = this.DockPanel.GetDockPanelState();
            SerializerHelper.Serialize(state, path);
        }

        private void ToggleToolWindow(DarkToolWindow toolWindow)
        {
            if (toolWindow.DockPanel == null)
            {
                this.DockPanel.AddContent(toolWindow);
            }
            else
            {
                this.DockPanel.RemoveContent(toolWindow);
            }
        }
    }
}