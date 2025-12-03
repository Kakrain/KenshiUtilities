using KenshiCore;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing.Interop;
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

        private Dictionary<ModItem, ModItemUtility> ModUtilitiesCache = new();

        private const string ConflictFileCachepath = "conflict_cache_file.txt";
        private const string ConflictModCachepath = "conflict_cache_mod.txt";
        private Dictionary<ModItem, ModAnalysis>? lookupModAnalysis = null;
        private Dictionary<ModItem, HashSet<string>>? lookupFiles = null;

        private TextBox searchTextBox;
        private Button searchButton;
        public MainForm()
        {
            Text = "Kenshi Utilities";
            setColors(Color.SteelBlue, Color.SkyBlue);
            AddButton("Refresh File Overrides", SeekFileConflictsButton_Click);
            AddButton("Refresh Mod Overrides", SeekModConflictsButton_Click);
            this.Width = 1000;
            this.Height = 700;

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
            listHost.Controls.Add(listContainer);

            this.Resize += MainForm_Resize;
            conflictFileCache=LoadConflictCache(ConflictFileCachepath);
            conflictModCache=LoadConflictCache(ConflictModCachepath);

            AddColumn("File Overlaps", mod => getNumberFileOverlaps(mod));
            AddColumn("Mod Record Override", mod => getNumberRecordOverride(mod));
            AddColumn("is Workshop newer?", mod => getVersions(mod));
            AddButton("Refresh Versions", SeekModVersions_Click);

            AddToggle("Show File overrides", (mod)=>ShowConflictsForSelectedMod(mod,conflictFileCache, fileConflictIndicator, Color.Yellow, Color.LightYellow));
            AddToggle("Show Mod Record overrides", (mod) => ShowConflictsForSelectedMod(mod,conflictModCache, modConflictIndicator, Color.Red, Color.IndianRed));
            AddToggle("Show Header", (mod) => ShowHeader(mod));
            AddToggle("Show Records", (mod) => ShowRecords(mod));
            AddToggle("Show not found dependencies", (mod) => ShowNotFoundDependencies(mod));


            searchTextBox = new TextBox
            {
                PlaceholderText = "Search string in all mods...",
                Width = 200,
                Location = new Point(10, 10)
            };

            searchButton = new Button
            {
                Text = "Search",
                Location = new Point(220, 8),
                Height = 32,
                Margin = new Padding(4),
            };

            searchButton.Click += SearchButton_Click;

            // Add to form
            buttonPanel.Controls.Add(searchTextBox);
            buttonPanel.Controls.Add(searchButton);
        }
        private void HighlightModItem(ListViewItem item)
        {
            item.BackColor = Color.Yellow;
            item.ForeColor = Color.Black;
        }

        private void ClearHighlight(ListViewItem item)
        {
            item.BackColor = SystemColors.Window;
            item.ForeColor = SystemColors.WindowText;
        }
        private void SearchButton_Click(object? sender, EventArgs e)
        {
            string query = searchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(query))
                return;
            foreach (ListViewItem it in modsListView.Items)
                ClearHighlight(it);

            modsListView.BeginUpdate();

            foreach (ListViewItem item in modsListView.Items)
            {
                if (item.Tag is not ModItem mod)
                    continue;
                if (BinaryContains(mod.getModFilePath()!,query))  
                {
                    HighlightModItem(item);
                }
            }
            modsListView.EndUpdate();
            modsListView.Refresh();
        }
        bool BinaryContains(string filePath, string search)
        {
            byte[] data = File.ReadAllBytes(filePath);
            byte[] pattern = Encoding.UTF8.GetBytes(search);

            // naive search (fast enough for small files)
            for (int i = 0; i <= data.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (data[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return true;
            }

            return false;
        }
        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            await OnShownAsync(e);
        }
        private async Task OnShownAsync(EventArgs e)
        {
            if (InitializationTask != null)
                await InitializationTask;
            foreach (var mod in mergedMods)
            {
                ModUtilitiesCache[mod.Value] = new ModItemUtility(mod.Value);
            }
        }
        private string getVersions(ModItem mod)
        {
            if (ModUtilitiesCache.ContainsKey(mod))
                return ModUtilitiesCache[mod].getVersionString();
            return "_|_";
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
        private int getNumberFileOverlaps(ModItem mod)
        {
            if (mod == null) return -1;
            int totalConflicts = 0;
            foreach (var kvp in conflictFileCache)
            {
                var (modA, modB) = kvp.Key;
                var conflicts = kvp.Value;
                if (conflicts == null || conflicts.Count == 0) continue;

                if (mod.Name.Equals(modA) || mod.Name.Equals(modB))
                {
                    totalConflicts += conflicts.Count;
                }
            }

            return totalConflicts;
        }

        private int getNumberRecordOverride(ModItem mod)
        {
            if (mod == null) return -1;
            int total = 0;
            foreach (var kvp in conflictModCache)
            {
                var (mod1, mod2) = kvp.Key;
                var conflicts = kvp.Value;

                if (mod.Name == mod1 || mod.Name == mod2)
                {
                    total += conflicts?.Count ?? 0;
                }
            }
            return total;
        }
        private void ShowHeader(ModItem mod)
        {
            ReverseEngineer re = new ReverseEngineer();
            re.LoadModFile(mod.getModFilePath()!);
            var logform = getLogForm();
            logform.LogString(re.GetHeaderAsString(),Color.Orange);
        }
        private void ShowRecords(ModItem mod)
        {
            ReverseEngineer re = new ReverseEngineer();
            re.LoadModFile(mod.getModFilePath()!);
            var logform = getLogForm();
            logform.LogString(re.GetRecordsAsString());
        }
        private void ShowNotFoundDependencies(ModItem mod)
        {
            var logform = getLogForm();
            ReverseEngineer re = new ReverseEngineer();
            re.LoadModFile(mod.getModFilePath()!);
            List<string> notfounddeps = new();
            foreach (string d in re.getDependencies())
            {
                mergedMods.TryGetValue(d, out var m);
                if (m == null)
                {
                    notfounddeps.Add(d);
                }
            }
            logform.LogString("not found Dependencies: " + (notfounddeps.Count == 0 ? "none" : string.Join("|", notfounddeps)),Color.Red);
        }
        public string[] GetAllFiles(ModItem mod)
        {
            string modpath = Path.GetDirectoryName(mod.getModFilePath())!;
            return Directory.GetFiles(modpath, "*.*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(modpath, f)).ToArray();
        }
        private async void SeekModVersions_Click(object? sender, EventArgs e)
        {
            await Task.Run(() =>
            {
                foreach (var mod in ModUtilitiesCache)
                {
                    ModUtilitiesCache[mod.Key].findVersions();
                    modsListView.BeginInvoke(new Action(() =>
                    {
                        var item = modsListView.Items
                            .Cast<ListViewItem>()
                            .FirstOrDefault(i => ((ModItem)i.Tag!).Name == mod.Key.Name);

                        if (item != null)
                        {
                            ModItemUtility mu = ModUtilitiesCache[mod.Key];
                            item.SubItems[3].ForeColor = mu.getColorVersions();
                        }
                    }));
                }
                
            });
            RefreshColumn(3);
        }
        
        private async void SeekFileConflictsButton_Click(object? sender, EventArgs e)
        {
            var mods = modsListView.Items.Cast<ListViewItem>().Select(item => (ModItem)item.Tag!).ToList();
            var totalPairs = mods.Count * (mods.Count - 1) / 2;
            InitializeProgress(0, totalPairs);
            if (lookupFiles == null)
            {
                lookupFiles = mods.ToDictionary(m => m, m => new HashSet<string>(GetAllFiles(m)));
            }

            await Task.Run(() => BuildConflictCache(conflictFileCache, ConflictFileCachepath, mods, GetOverlappingFiles));
            ModsListView_SelectedIndexChanged(null, null);
            TryInitialize();
        }
        private async void SeekModConflictsButton_Click(object? sender, EventArgs e)
        {
            var mods = modsListView.Items.Cast<ListViewItem>().Select(item => (ModItem)item.Tag!).ToList();
            var totalPairs = mods.Count * (mods.Count - 1) / 2;
            InitializeProgress(0, totalPairs);

            if (lookupModAnalysis == null)
            {
                lookupModAnalysis = mods.ToDictionary(m => m, m => new ModAnalysis(m));
            }
            Func<ModItem, ModItem, List<string>> conflictFunc = (modA, modB) => ModAnalysis.GetOverlappingNewRecords(lookupModAnalysis![modA], lookupModAnalysis![modB]);
            await Task.Run(() => BuildConflictCache(conflictModCache, ConflictModCachepath, mods, conflictFunc));
            ModsListView_SelectedIndexChanged(null, null);
            TryInitialize();
        }

        private void ShowConflictsForSelectedMod(ModItem mod,Dictionary<(string, string), List<string>> conflictCache, ConflictIndicatorPanel conflict_panel,Color main,Color secondary)
        {
            if (modsListView.SelectedItems.Count == 0)
                return;

            var selectedMod = mod;
            var conflictIndices = new List<int>();
            var logForm = getLogForm();

            modsListView.BeginUpdate();
            //var blocks = new List<(string, Color)>();
            StringBuilder conflicts = new StringBuilder();
            foreach (ListViewItem item in modsListView.Items)
            {
                string mod1 = selectedMod.Name;
                string mod2 = item.Text;
                var key = mod1.CompareTo(mod2) < 0 ? (mod1, mod2) : (mod2, mod1);
                if (item.Text == selectedMod.Name) continue;

                if (conflictCache.TryGetValue(key, out var overlap))
                {
                    bool hasConflict = overlap.Count > 0;
                    item.ForeColor = hasConflict ? Color.Red : Color.Black;
                    if (hasConflict) { 
                        conflictIndices.Add(item.Index);
                        conflicts.AppendLine(($"[{selectedMod.Name}] vs [{item.Text}]"));
                        var sb = new StringBuilder();
                        foreach (var ov in overlap)
                            sb.AppendLine($"{ov}");
                        conflicts.AppendLine((sb.ToString()));
                    }
                }
                else
                {
                    item.ForeColor = Color.Black;
                }
            }
            logForm.LogString(conflicts.ToString(),secondary);
            modsListView.EndUpdate();
            conflict_panel.UpdateConflicts(conflictIndices, modsListView.Items.Count);
            
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
    }
}
