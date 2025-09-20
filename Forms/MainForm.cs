using KenshiUtilities.Core;
using System;
using System.IO;
using System.Windows.Forms;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KenshiUtilities
{
    public class ConflictCacheData
    {
        public Dictionary<string, List<string>> Conflicts { get; set; } = new();
    }
    public class ConflictIndicatorPanel : Panel
    {
        private List<int> conflictIndices = new();
        private int itemCount = 1;
        private int offset = 1;
        
        
        public void UpdateConflicts(List<int> indices, int totalItems)
        {
            conflictIndices = indices ?? new List<int>();
            itemCount = Math.Max(1, totalItems);
            Invalidate();
        }
        public void setOffset(int v)
        {
            offset = v;
        }


        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (conflictIndices.Count == 0) return;
            for(int i = 0; i < offset; i++)
            {
                e.Graphics.FillRectangle(Brushes.LightGray, 0, i, Width, 2);
            }
            
            int usableHeight = Height - (2*offset);

            int totalItems = Math.Max(1, itemCount);


            foreach (var index in conflictIndices)
            {
                float relative = (float)index / (totalItems+1);
                int y = (int)(relative * usableHeight);

                e.Graphics.FillRectangle(Brushes.Red, 0, y+ offset, Width, 2);
            }
            for (int i = offset + usableHeight; i <= Height; i++)
            {
                e.Graphics.FillRectangle(Brushes.LightGray, 0, i, Width, 2);
            }
        }
    }
}
namespace KenshiUtilities
{
    public class MainForm : ProtoMainForm
    {
        private ConflictIndicatorPanel fileConflictIndicator;

        private string separator = "|SEP|";
        private Dictionary<(string, string), List<string>> conflictCache = new();

        private const string ConflictCacheFile = "conflict_cache.txt";
        public MainForm()
        {
            Text = "Kenshi Utilities";
            AddButton("Refresh File Conflicts", SeekFileConflictsButton_Click);

            var listContainer = new Panel{Dock = DockStyle.Fill};
            listContainer.Controls.Add(modsListView);
            modsListView.Dock = DockStyle.Fill;
            fileConflictIndicator = new ConflictIndicatorPanel
            {
                Dock = DockStyle.Right,
                Width = 6,
                BackColor = Color.White
            };
            int offset = 25;
            fileConflictIndicator.setOffset(offset);
            fileConflictIndicator.Height = modsListView.ClientSize.Height;
            fileConflictIndicator.Top = modsListView.ClientRectangle.Top + offset;
            listContainer.Controls.Add(fileConflictIndicator);
            mainlayout.Controls.Add(listContainer, 0, 1);
            this.Resize += MainForm_Resize;
            LoadConflictCache(ConflictCacheFile);
            modsListView.SelectedIndexChanged += ModsListView_SelectedIndexChanged;
        }
        private void MainForm_Resize(object? sender, EventArgs e)
        {
            if (fileConflictIndicator != null)
            {
                fileConflictIndicator.Invalidate();
            }
        }
        private void ModsListView_SelectedIndexChanged(object? sender, EventArgs e)
        {
            ShowConflictsForSelectedMod();
        }
        public string[] getAllFiles(ModItem mod)
        {
            string modpath = Path.GetDirectoryName(mod.getModFilePath());
            return Directory.GetFiles(modpath, "*.*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(modpath, f)).ToArray();
        }
        private void SeekFileConflictsButton_Click(object? sender, EventArgs e)
        {
            BuildConflictCache();
            ShowConflictsForSelectedMod();
        }
        private void ShowConflictsForSelectedMod()
        {
            if (modsListView.SelectedItems.Count == 0)
                return;

            var selectedMod = getSelectedMod();
            var conflictIndices = new List<int>();

            foreach (ListViewItem item in modsListView.Items)
            {
                if (item.Text == selectedMod.Name) continue;

                if (conflictCache.TryGetValue((selectedMod.Name, item.Text), out var overlap))
                {
                    item.ForeColor = overlap.Count > 0 ? Color.Red : Color.Black;
                    if (overlap.Count > 0)
                        conflictIndices.Add(item.Index);
                }
                else
                {
                    item.ForeColor = Color.Black;
                }
            }

            modsListView.Refresh();
            fileConflictIndicator.UpdateConflicts(conflictIndices, modsListView.Items.Count);
        }
        private void SaveConflictCache(string path)
        {
            using var writer = new StreamWriter(path);
            foreach (var kvp in conflictCache)
            {
                string mod1 = kvp.Key.Item1;
                string mod2 = kvp.Key.Item2;
                string line = string.Join(separator, new[] { mod1, mod2 }.Concat(kvp.Value));
                writer.WriteLine(line);
            }
        }
        private void LoadConflictCache(string path)
        {
            conflictCache.Clear();
            if (!File.Exists(path))
                return;
            foreach (var line in File.ReadLines(path))
            {
                var parts = line.Split(separator, StringSplitOptions.None);
                if (parts.Length < 2) continue;

                string mod1 = parts[0];
                string mod2 = parts[1];
                var conflicts = parts.Skip(2).ToList();

                conflictCache[(mod1, mod2)] = conflicts;
            }
        }
        private void BuildConflictCache()
        {
            conflictCache.Clear();

            var mods = modsListView.Items.Cast<ListViewItem>().ToList();
            var pairs = mods.SelectMany((item, i) =>
                mods.Skip(i + 1).Select(other => (item, other)));

            RunWithProgress(pairs, (firstItem, secondItem, index, total) =>
            {
                var mod1 = (ModItem)firstItem.Tag;
                var mod2 = (ModItem)secondItem.Tag;

                var files1 = new HashSet<string>(getAllFiles(mod1));
                var overlap = getAllFiles(mod2).Where(f => files1.Contains(f)).ToList();

                if (overlap.Count > 0)
                {
                    conflictCache[(mod1.Name, mod2.Name)] = overlap;
                    conflictCache[(mod2.Name, mod1.Name)] = overlap;
                }
            });
            SaveConflictCache(ConflictCacheFile);
        }
    }
}
