using System.Collections.Generic;

namespace TA.ToolSuite
{
    public sealed class TTS_QuickCheckItem
    {
        public string Name;
        public bool Available;
        public string Reason;
    }

    public interface ITTSModule
    {
        string Name { get; }
        void Draw();
        TTS_QuickCheckItem Check();
    }

    public static class TTS_QuickCheck
    {
        public static List<TTS_QuickCheckItem> Run(IEnumerable<ITTSModule> modules)
        {
            var list = new List<TTS_QuickCheckItem>();
            foreach (var module in modules) list.Add(module.Check());
            return list;
        }
    }
}
