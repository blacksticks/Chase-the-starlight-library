using Nautilus.Json;
using Nautilus.Options.Attributes;
using UnityEngine;

namespace JustEnoughItems.Options
{
    [Menu("JustEnoughItems Input")]
    public class JeiInputConfig : ConfigFile
    {
        [Keybind("Open JEI")]
        public KeyCode OpenJei = KeyCode.H;
    }
}
 