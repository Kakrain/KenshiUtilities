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

        public ModAnalysis(ModItem mod)
        {
            //Mod = mod;
            Engineer = new ReverseEngineer();
            Engineer.LoadModFile(mod.getModFilePath()!);

            RecordLookup = Engineer.modData.Records!
                .ToDictionary(r => r.StringId, r => r);
        }
    }
}
