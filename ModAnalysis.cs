using KenshiCore.Mods;
using KenshiCore.ReverseEngineering;
using KenshiCore.UI;
using KenshiCore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KenshiUtilities
{
    class ModAnalysis
    {
        public ReverseEngineer Engineer { get; }
        public Dictionary<string, ModRecord> RecordLookup { get; }
        public Dictionary<string, HashSet<string>> RecordChangedFields { get; }
        public ModAnalysis(ModItem mod)
        {
            Engineer = new ReverseEngineer();
            string? modpath= mod.getModFilePath();
            if (!File.Exists(modpath))
            {
                Engineer.InitializeEmptyMod();
            }
            else
            {
                Engineer.LoadModFile(mod.getModFilePath()!);
            }
            RecordLookup = Engineer.modData.Records!.Where(r=>r.isNew()).ToDictionary(r => r.StringId, r => r);
            RecordChangedFields = Engineer.modData.Records!
            .ToDictionary(
                r => r.StringId,
                r => r.getChangedFields()
            );
        }
        public static List<string> GetOverlappingNewRecords(ModAnalysis A, ModAnalysis B)
        {
            var overlaps = new List<string>();

            foreach (var ra in A.Engineer.modData.Records!.Where(r => r.isNew()))
            {
                if (B.RecordLookup.TryGetValue(ra.StringId, out var rb))
                {
                    overlaps.Add(
                        $"{ra.Name}|{ra.StringId}|{ra.getRecordType()}" +
                        $"[info score: {ra.GetRecordCompleteness()}] " +
                        $"vs [info score: {rb.GetRecordCompleteness()}]"
                    );
                }
            }

            return overlaps;
        }
    }
}
