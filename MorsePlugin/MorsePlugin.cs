using Grabacr07.KanColleViewer.Composition;
using System.ComponentModel.Composition;

namespace MorsePlugin
{
    [Export(typeof(IPlugin))]
    [Export(typeof(ITool))]
    [ExportMetadata("Guid", "FE411521-0A7A-401D-8E40-70E671D057A3")]
    [ExportMetadata("Title", "Morse Plugin")]
    [ExportMetadata("Description", "提督業も忙しい！(KanColleViewer)用Twitterプラグイン")]
    [ExportMetadata("Version", "4.1.0")] // Major and minor version correlate with KanColleViewer version
    [ExportMetadata("Author", "@azamshul")]
    public class MorsePlugin : IPlugin, ITool
    {
        public string Name { get { return "Morse"; } }

        public object View { get { return new MorsePluginView { DataContext = this.vm }; } }

        private MorsePluginViewModel vm;

        public void Initialize()
        {
            this.vm = new MorsePluginViewModel { }; // Allow recreation of view model on initialize
        }

    }
}
