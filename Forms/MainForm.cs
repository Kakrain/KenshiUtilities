using KenshiCore;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
namespace KenshiUtilities
{
    public class ConflictIndicatorPanel : Panel
    {
        private List<int> conflictIndices = new();
        private int itemCount = 1;
        private int offset = 1;
        private Brush main_brush = Brushes.Red;
        
        public void UpdateConflicts(List<int> indices, int totalItems)
        {
            conflictIndices = indices ?? new List<int>();
            itemCount = Math.Max(1, totalItems);
            Invalidate();
        }
        public void SetOffset(int v)
        {
            offset = v;
        }
        public void SetBrush(Brush b)
        {
            main_brush = b;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (conflictIndices.Count == 0) return;

            if (offset > 0)
                e.Graphics.FillRectangle(Brushes.LightGray, 0, 0, Width, offset);
            
            int usableHeight = Height - (2*offset);

            int totalItems = Math.Max(1, itemCount);


            foreach (var index in conflictIndices)
            {
                float relative = (float)index / Math.Max(1, totalItems - 1);
                int y = (int)(relative * usableHeight);

                e.Graphics.FillRectangle(main_brush, 0, y+ offset, Width, 2);
            }
            if (offset > 0)
                e.Graphics.FillRectangle(Brushes.LightGray, 0, Height - offset, Width, offset);
        }
    }
}
namespace KenshiUtilities
{
    public class MainForm : ProtoMainForm
    {
        private ConflictIndicatorPanel fileConflictIndicator;
        private ConflictIndicatorPanel modConflictIndicator;

        private string _separator = "|SEP|";
        private Dictionary<(string, string), List<string>> conflictFileCache = new();
        private Dictionary<(string, string), List<string>> conflictModCache = new();

        private const string ConflictFileCachepath = "conflict_cache_file.txt";
        private const string ConflictModCachepath = "conflict_cache_mod.txt";
        private Dictionary<ModItem, ModAnalysis>? lookupModAnalysis = null;
        private Dictionary<ModItem, HashSet<string>>? lookupFiles = null;
        public MainForm()
        {
            Text = "Kenshi Utilities";
            setColors(Color.SteelBlue, Color.SkyBlue);
            AddButton("Refresh File Overrides", SeekFileConflictsButton_Click);
            AddButton("Refresh Mod Overrides", SeekModConflictsButton_Click);

            var listContainer = new Panel{Dock = DockStyle.Fill};
            listContainer.Controls.Add(modsListView);
            modsListView.Dock = DockStyle.Fill;
            fileConflictIndicator = new ConflictIndicatorPanel
            {
                Dock = DockStyle.Right,
                Width = 6,
                BackColor = Color.White
            };
            fileConflictIndicator.SetBrush(Brushes.Green);
            modConflictIndicator = new ConflictIndicatorPanel
            {
                Dock = DockStyle.Right,
                Width = 6,
                BackColor = Color.White
            };
            modConflictIndicator.SetBrush(Brushes.Blue);
            int offset = 25;
            fileConflictIndicator.SetOffset(offset);
            fileConflictIndicator.Height = modsListView.ClientSize.Height;
            fileConflictIndicator.Top = modsListView.ClientRectangle.Top + offset;

            modConflictIndicator.SetOffset(offset);
            modConflictIndicator.Height = modsListView.ClientSize.Height;
            modConflictIndicator.Top = modsListView.ClientRectangle.Top + offset;

            listContainer.Controls.Add(fileConflictIndicator);
            listContainer.Controls.Add(modConflictIndicator);
            mainlayout.Controls.Add(listContainer, 0, 1);

            
            this.Resize += MainForm_Resize;
            conflictFileCache=LoadConflictCache(ConflictFileCachepath);
            conflictModCache=LoadConflictCache(ConflictModCachepath);
            modsListView.SelectedIndexChanged += ModsListView_SelectedIndexChanged;
            EnableConsoleLog();
        }
        private void MainForm_Resize(object? sender, EventArgs e)
        {
            if (fileConflictIndicator != null)
            {
                fileConflictIndicator.Invalidate();
            }
            if (modConflictIndicator != null)
            {
                modConflictIndicator.Invalidate();
            }
        }
        private void ModsListView_SelectedIndexChanged(object? sender, EventArgs? e)
        {
            generalLog!.Clear();
            string file_conflicts = ShowFileConflictsForSelectedMod(conflictFileCache, fileConflictIndicator);
            LogMessage(file_conflicts);
            string mod_conflicts = ShowFileConflictsForSelectedMod(conflictModCache, modConflictIndicator);
            LogMessage(mod_conflicts);
            modsListView.Refresh();
        }
        public string[] GetAllFiles(ModItem mod)
        {
            string modpath = Path.GetDirectoryName(mod.getModFilePath())!;
            return Directory.GetFiles(modpath, "*.*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(modpath, f)).ToArray();
        }
        
        private async void SeekFileConflictsButton_Click(object? sender, EventArgs e)
        {
            var mods = modsListView.Items.Cast<ListViewItem>().Select(item => (ModItem)item.Tag!).ToList();
            var totalPairs = mods.Count * (mods.Count - 1) / 2;
            progressBar.Minimum = 0;
            progressBar.Maximum = totalPairs;
            if (lookupFiles == null)
            {
                lookupFiles = mods.ToDictionary(m => m, m => new HashSet<string>(GetAllFiles(m)));
            }

            await Task.Run(() => BuildConflictCache(conflictFileCache, ConflictFileCachepath, mods, GetOverlappingFiles));
            ModsListView_SelectedIndexChanged(null, null);
        }
        private async void SeekModConflictsButton_Click(object? sender, EventArgs e)
        {
            var mods = modsListView.Items.Cast<ListViewItem>().Select(item => (ModItem)item.Tag!).ToList();
            var totalPairs = mods.Count * (mods.Count - 1) / 2;
            progressBar.Minimum = 0;
            progressBar.Maximum = totalPairs;

            if (lookupModAnalysis == null)
            {
                lookupModAnalysis = mods.ToDictionary(m => m, m => new ModAnalysis(m));
            }
            await Task.Run(() => BuildConflictCache(conflictModCache, ConflictModCachepath, mods, GetOverlappingMods));
            ModsListView_SelectedIndexChanged(null, null);
        }
        
        private string ShowFileConflictsForSelectedMod(Dictionary<(string, string), List<string>> conflictCache, ConflictIndicatorPanel conflict_panel)
        {
            if (modsListView.SelectedItems.Count == 0)
                return "";

            var selectedMod = getSelectedMod();
            var conflictIndices = new List<int>();
            
            StringBuilder msgs = new StringBuilder();
            foreach (ListViewItem item in modsListView.Items)
            {
                if (item.Text == selectedMod.Name) continue;

                if (conflictCache.TryGetValue((selectedMod.Name, item.Text), out var overlap))
                {
                    item.ForeColor = overlap.Count > 0 ? Color.Red : Color.Black;
                    if (overlap.Count > 0) { 
                        conflictIndices.Add(item.Index);
                        msgs.Append($"[{selectedMod.Name}] vs [{item.Text}]\r\n");
                        foreach (var file in overlap)
                            msgs.Append($"   {file}\r\n");
                        msgs.Append($"\r\n");
                    }
                }
                else
                {
                    item.ForeColor = Color.Black;
                }
            }
            conflict_panel.UpdateConflicts(conflictIndices, modsListView.Items.Count);
            return msgs.ToString();
            
        }
        
        private void SaveConflictCache(Dictionary<(string, string), List<string>> cache, string path)
        {
            using var writer = new StreamWriter(path);
            foreach (var kvp in cache)
            {
                string mod1 = kvp.Key.Item1;
                string mod2 = kvp.Key.Item2;
                string line = string.Join(_separator, new[] { mod1, mod2 }.Concat(kvp.Value));
                writer.WriteLine(line);
            }
        }
        private Dictionary<(string, string), List<string>> LoadConflictCache(string path)
        {
            Dictionary<(string, string), List<string>> result = new();
            if (!File.Exists(path))
                return result;
            foreach (var line in File.ReadLines(path))
            {
                var parts = line.Split(_separator, StringSplitOptions.None);
                if (parts.Length < 2) continue;

                string mod1 = parts[0];
                string mod2 = parts[1];
                var conflicts = parts.Skip(2).ToList();

                result[(mod1, mod2)] = conflicts;
            }
            return result;
        }

        private void BuildConflictCache(Dictionary<(string, string), List<string>> cache, string path, List<ModItem> mods, Func<ModItem, ModItem, List<string>> func)
        {
            var newCache = new ConcurrentDictionary<(string, string), List<string>>();
            int processed = 0;

            Parallel.For(0, mods.Count, i =>
            {
                for (int j = i + 1; j < mods.Count; j++)
                {
                    var mod1 = mods[i];
                    var mod2 = mods[j];
                    
                    var overlap = func(mod1, mod2);

                    if (overlap.Count > 0)
                    {
                        // Normalize key order (avoid duplicates in memory)
                        var key = mod1.Name.CompareTo(mod2.Name) < 0
                            ? (mod1.Name, mod2.Name)
                            : (mod2.Name, mod1.Name);

                        newCache[key] = overlap;
                    }
                    int done = Interlocked.Increment(ref processed);
                    if (done % 100 == 0)
                        ReportProgress(done, $"processing {mod1.Name} vs {mod2.Name}");

                }
            });
            ReportProgress(processed, $"Finished");
            cache.Clear();
            foreach (var kvp in newCache)
            {
                cache[kvp.Key] = kvp.Value;
            }
            SaveConflictCache(cache, path);
        }
        private List<string> GetOverlappingFiles(ModItem modA, ModItem modB)
        {
            var smaller = lookupFiles![modA];
            var larger = lookupFiles[modB];

            // Always iterate the smaller set
            if (smaller.Count > larger.Count)
            {
                var tmp = smaller;
                smaller = larger;
                larger = tmp;
            }

            var overlap = new List<string>();
            foreach (var f in smaller)
            {
                if (larger.Contains(f))
                    overlap.Add(f);
            }
            return overlap;
        }
        private List<string> GetOverlappingMods(ModItem modA, ModItem modB)
        {
            ModAnalysis A = lookupModAnalysis![modA];
            ModAnalysis B = lookupModAnalysis![modB];


            var overlaps = new List<string>();

            foreach (var ra in A.Engineer.modData.Records!)
            {
                if (B.RecordLookup.TryGetValue(ra.StringId,out var rb))
                {
                    overlaps.Add(
                        $"{ra.Name}|{ra.StringId}|" +
                        $"[{ra.getModType()}|{ra.getChangeType()}] " +
                        $"vs [{rb.getModType()}|{rb.getChangeType()}]"
                    );
                }
            }
            return overlaps;
        }
    }
}
