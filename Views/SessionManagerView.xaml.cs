#region Using

using System.ComponentModel.Composition;
using LightPaper.Infrastructure.Contracts;
using Microsoft.Practices.Prism.Regions;
using SessionManager.ViewModels;

#endregion

namespace SessionManager.Views
{
    [Export(typeof (ISidebarControl))]
    [Export(typeof (SessionManagerView))]
    [ViewSortHint("2")]
    public partial class SessionManagerView : ISidebarControl, IPartImportsSatisfiedNotification
    {
#pragma warning disable 649
        [Import] private SessionManagerViewModel _viewModel;
#pragma warning restore 649
        
        public SessionManagerView()
        {
            InitializeComponent();
        }

        public void OnImportsSatisfied()
        {
            DataContext = _viewModel;
        }
    }
}