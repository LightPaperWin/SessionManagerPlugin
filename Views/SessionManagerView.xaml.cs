#region Using

using System.ComponentModel.Composition;
using LightPaper.Infrastructure.Contracts;
using Microsoft.Practices.Prism.Regions;

#endregion

namespace SessionManager.Views
{
    [Export(typeof (ISidebarControl))]
    [Export(typeof (SessionManagerView))]
    [ViewSortHint("2")]
    public partial class SessionManagerView : ISidebarControl
    {
        public SessionManagerView()
        {
            InitializeComponent();
        }
    }
}