using Community.VisualStudio.Toolkit;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CodeMindMap
{
    internal partial class OptionsProvider
    {
        // Register the options with this attribute on your package class:
        // [ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "CodeMindMap", "General", 0, 0, true, SupportsProfiles = true)]
        [ComVisible(true)]
        public class GeneralOptions : BaseOptionPage<General> { }
    }

    public class General : BaseOptionModel<General>
    {
        [Category("General")]
        [DisplayName("Show when solution opens")]
        [Description("Show Code Mind Map window when solution with saved map opens.")]
        [DefaultValue(false)]
        public bool ShowCodeMindMapOnSolutionOpen { get; set; } = false;
    }
}
