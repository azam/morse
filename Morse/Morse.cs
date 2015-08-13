using Grabacr07.KanColleViewer.Composition;
using System.ComponentModel.Composition;

namespace Morse
{
    [Export(typeof(IPlugin))]
    [Export(typeof(ITool))]
    [ExportMetadata("Guid", "FE411521-0A7A-401D-8E40-70E671D057A3")]
    [ExportMetadata("Title", "Morse Plugin")]
    [ExportMetadata("Description", "提督業も忙しい！(KanColleViewer)用Twitterプラグイン")]
    [ExportMetadata("Version", "4.1.0")] // Major and minor version correlate with KanColleViewer version
    [ExportMetadata("Author", "@azamshul")]
    public class Morse : IPlugin, ITool
    {
        public string Name { get { return "Morse"; } }

        public object View { get { return new MorseView { DataContext = this.vm }; } }

        private MorseViewModel vm;

        public void Initialize()
        {
            this.vm = new MorseViewModel { }; // Allow recreation of view model on initialize
        }

    }
}
