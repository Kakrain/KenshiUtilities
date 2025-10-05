using KenshiCore;
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
            Engineer.LoadModFile(mod.getModFilePath()!);

            RecordLookup = Engineer.modData.Records!
                .ToDictionary(r => r.StringId, r => r);
            RecordChangedFields = Engineer.modData.Records!
            .ToDictionary(
                r => r.StringId,
                r => r.getChangedFields()
            );
        }
        public static List<string> GetOverlappingRecords(ModAnalysis A, ModAnalysis B)
        {
            var overlaps = new List<string>();

            foreach (var ra in A.Engineer.modData.Records!)
            {
                if (B.RecordLookup.TryGetValue(ra.StringId, out var rb))
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

        // Conflict = both mods change the same field of the same record
        public static List<string> GetConflictingRecords(ModAnalysis A, ModAnalysis B)
        {
            var conflicts = new List<string>();

            foreach (var ra in A.Engineer.modData.Records!)
            {
                if (B.RecordLookup.TryGetValue(ra.StringId, out var rb))
                {
                    var aFields = A.RecordChangedFields[ra.StringId];
                    var bFields = B.RecordChangedFields[rb.StringId];

                    foreach (var f in aFields.Intersect(bFields))
                    {
                        conflicts.Add(
                            $"{ra.Name}|{ra.StringId}|Field '{f}' modified differently"
                        );
                    }
                }
            }

            return conflicts;
        }
    }
}
