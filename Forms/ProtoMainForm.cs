using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using KenshiUtilities.Core;

class ListViewColumnSorter : IComparer
{
    public int Column { get; set; } = 0;
    public SortOrder Order { get; set; } = SortOrder.Ascending;

    public int Compare(object? x, object? y)
    {
        if (x is not ListViewItem itemX || y is not ListViewItem itemY)
            return 0;

        string textX = itemX.SubItems[Column].Text;
        string textY = itemY.SubItems[Column].Text;

        int result = string.Compare(textX, textY, StringComparison.CurrentCultureIgnoreCase);
        return Order == SortOrder.Ascending ? result : -result;
    }
}

namespace KenshiUtilities
{
    public class ProtoMainForm : Form
    {
        public ListView modsListView;
        private ImageList modIcons = new ImageList();
        private Dictionary<string, ModItem> mergedMods = new Dictionary<string, ModItem>();
        private List<string> gameDirMods = new List<string>();
        private List<string> selectedMods = new List<string>();
        private List<string> workshopMods = new List<string>();
        private Dictionary<string, ListViewItem> modItemsLookup = new();

        private ProgressBar progressBar;
        private Label progressLabel;
        private ModManager modM = new ModManager(new ReverseEngineer());
        private Button openGameDirButton;
        private Button openSteamLinkButton;
        private Button copyToGameDirButton;
        protected TableLayoutPanel mainlayout =new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
            };
    protected Panel listContainer;
        private FlowLayoutPanel buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true
        };
        public ProtoMainForm()
        {
            Text = "Unnamed Proto Main Form";
            Width = 800;
            Height = 500;


            mainlayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mainlayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Controls.Add(mainlayout);

            progressBar = new ProgressBar { Dock = DockStyle.Top, Height = 20, Minimum = 0, Maximum = 0, Value = 0 };
            mainlayout.Controls.Add(progressBar, 0, 0);
            mainlayout.SetColumnSpan(progressBar, 2);

            progressLabel = new Label { Dock = DockStyle.Top, Height = 20, Text = "Ready", TextAlign = ContentAlignment.MiddleLeft };
            mainlayout.Controls.Add(progressLabel, 1, 0);
            mainlayout.SetColumnSpan(progressLabel, 2);

            modsListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true
            };
            modsListView.Columns.Add("Mod Name", -2, HorizontalAlignment.Left);
            //modsListView.Columns.Add("Source", 120);
            //modsListView.Columns.Add("Status", 120);
            mainlayout.Controls.Add(modsListView, 0, 1);

            //listContainer = new Panel { Dock = DockStyle.Fill };
            //modsListView.Dock = DockStyle.Fill;
            //listContainer.Controls.Add(modsListView);
            //layout.Controls.Add(listContainer, 0, 1);

            modsListView.SelectedIndexChanged += SelectedIndexChanged;
            modsListView.ColumnClick += ModsListView_ColumnClick;
            modsListView.ListViewItemSorter = new ListViewColumnSorter();

            mainlayout.Controls.Add(buttonPanel, 1, 1);

            openGameDirButton = new Button { Text = "Open Mod Directory", AutoSize = true, Enabled = false };
            openGameDirButton.Click += OpenGameDirButton_Click;
            buttonPanel.Controls.Add(openGameDirButton);

            openSteamLinkButton = new Button { Text = "Open Steam Link", AutoSize = true, Enabled = false };
            openSteamLinkButton.Click += OpenSteamLinkButton_Click;
            buttonPanel.Controls.Add(openSteamLinkButton);

            copyToGameDirButton = new Button { Text = "Copy to GameDir", AutoSize = true, Enabled = false };
            copyToGameDirButton.Click += CopyToGameDirButton_Click;
            buttonPanel.Controls.Add(copyToGameDirButton);

            _ = InitializeAsync();
        }
        protected void RunWithProgress<T>(IEnumerable<T> items, Action<T, int, int> action)
        {
            if (progressBar == null || progressLabel == null) return;

            int total = items.Count();
            progressBar.Minimum = 0;
            progressBar.Maximum = total;
            progressBar.Value = 0;

            int index = 0;
            foreach (var item in items)
            {
                action(item, index, total); // the action can update the item
                progressBar.Value = index + 1;
                progressLabel.Text = $"Processing {index + 1}/{total}";
                progressLabel.Refresh(); // force label update
                index++;
                Application.DoEvents(); // keep UI responsive
            }

            progressLabel.Text = "Done!";
        }
        protected virtual void SetupColumns() { }

        protected void AddButton(string text, EventHandler onClick)
        {

            var button = new Button
            {
                Text = text,
                AutoSize = true
            };
            button.Click += onClick;

            buttonPanel.Controls.Add(button);
        }

        private void ModsListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            var sorter = (ListViewColumnSorter)modsListView.ListViewItemSorter!;
            if (sorter.Column == e.Column)
                sorter.Order = sorter.Order == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            else
            {
                sorter.Column = e.Column;
                sorter.Order = SortOrder.Ascending;
            }
            modsListView.Sort();
        }

        private async Task InitializeAsync()
        {
            progressLabel.Text = "Loading mods...";
            progressBar.Style = ProgressBarStyle.Marquee;

            gameDirMods = await Task.Run(() => modM.LoadGameDirMods());
            selectedMods = await Task.Run(() => modM.LoadSelectedMods());
            workshopMods = await Task.Run(() => modM.LoadWorkshopMods());

            modIcons.ImageSize = new Size(48, 16);
            modsListView.SmallImageList = modIcons;

            this.Invoke((MethodInvoker)delegate {
                modIcons.ImageSize = new Size(48, 16);
                modsListView.SmallImageList = modIcons;
                PopulateModsListView();
                modsListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);

                progressBar.Style = ProgressBarStyle.Continuous;
                progressLabel.Text = "Ready";
            });
        }

        private void SelectedIndexChanged(object? sender, EventArgs? e)
        {
            if (modsListView.SelectedItems.Count != 1)
            {
                openGameDirButton.Enabled = false;
                openSteamLinkButton.Enabled = false;
                copyToGameDirButton.Enabled = false;
                return;
            }

            string modName = modsListView.SelectedItems[0].Text;
            if (mergedMods.TryGetValue(modName, out var mod))
            {
                openGameDirButton.Enabled = mod.InGameDir || mod.WorkshopId != -1;
                copyToGameDirButton.Enabled = !mod.InGameDir && mod.WorkshopId != -1;
                openSteamLinkButton.Enabled = mod.WorkshopId != -1;
            }
        }
        protected ModItem getSelectedMod()
        {
            string modName = modsListView.SelectedItems[0].Text;
            return (ModItem)modsListView.SelectedItems[0].Tag!;

        }

        private void OpenGameDirButton_Click(object? sender, EventArgs e)
        {
            //string modName = modsListView.SelectedItems[0].Text;
            //string? modpath = Path.GetDirectoryName(((ModItem)modsListView.SelectedItems[0].Tag!).getModFilePath());
            string? modpath = Path.GetDirectoryName(getSelectedMod().getModFilePath());
            if (modpath != null && Directory.Exists(modpath))
                Process.Start("explorer.exe", modpath);
            else
                MessageBox.Show($"{modpath} not found!");
        }

        private void OpenSteamLinkButton_Click(object? sender, EventArgs e)
        {
            string modName = modsListView.SelectedItems[0].Text;
            var mod = mergedMods[modName];
            if (mod != null && mod.WorkshopId != -1)
            {
                string url = $"https://steamcommunity.com/sharedfiles/filedetails/?id={mod.WorkshopId}";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else
            {
                MessageBox.Show("This mod is not from the Steam Workshop.");
            }
        }

        private void CopyToGameDirButton_Click(object? sender, EventArgs e)
        {
            if (modsListView.SelectedItems.Count != 1) return;

            string modName = modsListView.SelectedItems[0].Text;
            if (!mergedMods.TryGetValue(modName, out var mod)) return;
            if (mod.WorkshopId == -1) return;

            string workshopFolder = Path.Combine(ModManager.workshopModsPath, mod.WorkshopId.ToString());
            string gameDirFolder = Path.Combine(ModManager.gamedirModsPath, Path.GetFileNameWithoutExtension(modName));

            if (Directory.Exists(gameDirFolder))
            {
                MessageBox.Show("Mod already exists in GameDir!");
                return;
            }

            CopyDirectory(workshopFolder, gameDirFolder);
            mod.InGameDir = true;
            modsListView.SelectedItems[0].ImageKey = mod.Name;
            MessageBox.Show($"{mod.Name} copied to GameDir!");
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)));

            foreach (var dir in Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
        }

        private void PopulateModsListView()
        {
            modsListView.Items.Clear();

            foreach (var mod in modM.LoadSelectedMods())
            {
                if (!mergedMods.ContainsKey(mod))
                    mergedMods[mod] = new ModItem(mod);
                mergedMods[mod].Selected = true;
            }
            foreach (var mod in gameDirMods)
            {
                if (!mergedMods.ContainsKey(mod))
                    mergedMods[mod] = new ModItem(mod);
                mergedMods[mod].InGameDir = true;
            }
            foreach (var folder_mod in workshopMods)
            {
                string? folderPart = Path.GetDirectoryName(folder_mod!);
                if (folderPart == null) continue;
                string filePart = Path.GetFileName(folder_mod);
                if (!mergedMods.ContainsKey(filePart))
                    mergedMods[filePart] = new ModItem(filePart);
                mergedMods[filePart].WorkshopId = Convert.ToInt64(folderPart);
            }
            foreach (var mod in mergedMods.Values)
            {
                // Create composite icon for this mod
                Image icon = mod.CreateCompositeIcon();
                if (!modIcons.Images.ContainsKey(mod.Name))
                    modIcons.Images.Add(mod.Name, icon);
                // Add to ListView
                var item = new ListViewItem(new[] { mod.Name, mod.Language })
                {
                    Tag = mod,
                    ImageKey = mod.Name
                };
                item.UseItemStyleForSubItems = false;
                modsListView.Items.Add(item);
            }

            
        }
    }
}
