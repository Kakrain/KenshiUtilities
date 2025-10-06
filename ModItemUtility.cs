using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KenshiCore;
using System.Threading.Tasks;

namespace KenshiUtilities
{
    public class ModItemUtility 
    {
        private int Wversion = -1;
        private int Gversion = -1;
        private ModItem inner;
        public ModItemUtility(ModItem baseItem)
        {
            inner = baseItem;
        }
        public string getVersionString()
        {
            return (isWorkshopNewer()?"yes":"no")+" : "+((Wversion==-1)?"_":Wversion.ToString())+"|"+((Gversion == -1)?"_":Gversion.ToString());
        }
        public void findVersions()
        {
            string? Wpath = Base.getWorkshopModPath();
            Wversion = Wpath == null ? -1 : ReverseEngineer.readJustVersion(Wpath);
            string? Gpath = Base.getGamedirModPath();
            Gversion = Gpath == null ? -1 : ReverseEngineer.readJustVersion(Gpath);
        }
        private Boolean isWorkshopNewer()
        {
            return (Wversion > Gversion) && (Wversion != -1) && (Gversion != -1);
        }
        public Color getColorVersions()
        {
            return isWorkshopNewer ()? Color.OrangeRed : Color.Green;
        }
        public ModItem Base => inner;
    }
}
