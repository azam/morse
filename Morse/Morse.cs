using Grabacr07.KanColleViewer.Composition;
using System.ComponentModel.Composition;

namespace Morse
{
    [Export(typeof(IPlugin))]
    [Export(typeof(ITool))]
    [ExportMetadata("Guid", "FE24AC45-A463-49B1-9462-F51EE1178AB3")]
    [ExportMetadata("Title", "Morse")]
    [ExportMetadata("Description", "提督業も忙しい！(KanColleViewer)用Twitterプラグイン")]
    [ExportMetadata("Version", "4.1.0")] // Major and minor version correlate with KanColleViewer version
    [ExportMetadata("Author", "@azamshul")]
    public class Morse : IPlugin, ITool
    {
        public string Name { get { return MorseResources.pluginName; } }

        public object View { get { return new MorseView { DataContext = this.vm }; } }

        private MorseViewModel vm;

        public void Initialize()
        {
            this.vm = new MorseViewModel { }; // Allow recreation of view model on initialize
        }
    }
}
