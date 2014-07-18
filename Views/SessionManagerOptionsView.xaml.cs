#region Using

using System.ComponentModel.Composition;
using LightPaper.Infrastructure.Contracts;
using Microsoft.Practices.Prism.Regions;
using SessionManager.ViewModels;

#endregion

namespace SessionManager.Views
{
    [Export(typeof (IQuickOptionControl))]
    [Export(typeof (SessionManagerOptionsView))]
    [ViewSortHint("5")]
    public partial class SessionManagerOptionsView : IQuickOptionControl, IPartImportsSatisfiedNotification
    {
#pragma warning disable 649
        [Import] private SessionManagerViewModel _viewModel;
#pragma warning restore 649

        public SessionManagerOptionsView()
        {
            InitializeComponent();
        }

        #region IPartImportsSatisfiedNotification Members

        public void OnImportsSatisfied()
        {
            DataContext = _viewModel;
        }

        #endregion
    }
}