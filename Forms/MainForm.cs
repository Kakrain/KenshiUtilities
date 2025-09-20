using KenshiUtilities.Core;
using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace KenshiUtilities
{
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

        public MainForm()
        {
            Text = "Kenshi Utilities";
            AddButton("Refresh File Conflicts", SeekConflictsButton_Click);

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
        }
        private void MainForm_Resize(object? sender, EventArgs e)
        {
            if (fileConflictIndicator != null)
            {
                fileConflictIndicator.Invalidate();
            }
        }
        public string[] getAllFiles(ModItem mod)
        {
            string modpath = Path.GetDirectoryName(mod.getModFilePath());
            return Directory.GetFiles(modpath, "*.*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(modpath, f)).ToArray();
        }
        private void SeekConflictsButton_Click(object? sender, EventArgs e)
        {
            ModItem selectedmod = getSelectedMod();
            HashSet<string> selectedModFiles = new HashSet<string>(getAllFiles(getSelectedMod()));
            var conflictIndices = new List<int>();

            var modsToCheck = modsListView.Items.Cast<ListViewItem>().Where(item => item.Text != selectedmod.Name);
            RunWithProgress(modsToCheck, (item, index, total) =>
            {
                string[] overlap = getAllFiles((ModItem)item.Tag).Where(f => selectedModFiles.Contains(f)).ToArray();
                item.ForeColor = overlap.Length > 0 ? Color.Red : Color.Black;
                if(overlap.Length > 0)
                {
                    conflictIndices.Add(item.Index);
                    item.ForeColor = Color.Red;
                }
                else
                {
                    item.ForeColor = Color.Black;
                }
            });
            modsListView.Refresh();
            fileConflictIndicator.UpdateConflicts(conflictIndices, modsListView.Items.Count);
        }
}
}
