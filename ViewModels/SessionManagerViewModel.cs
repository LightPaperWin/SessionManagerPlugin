#region Using

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using LightPaper.Infrastructure;
using LightPaper.Infrastructure.Contracts;
using LightPaper.Infrastructure.Events;
using LightPaper.Infrastructure.Helpers;
using Microsoft.Practices.Prism.Commands;
using Microsoft.Practices.Prism.Logging;
using Microsoft.Practices.Prism.PubSubEvents;
using Newtonsoft.Json;
using PuppyFramework;
using PuppyFramework.Helpers;
using PuppyFramework.Interfaces;
using SessionManager.Models;
using SessionManager.Properties;

#endregion

namespace SessionManager.ViewModels
{
    [Export(typeof (ISessionManager))]
    [Export(typeof (SessionManagerViewModel))]
    internal class SessionManagerViewModel : ViewModelBase, ISessionManager, IDisposable
    {
        #region Fields

#pragma warning disable 649
        [Import(LightPaper.Infrastructure.MagicStrings.ExportContractNames.DEFAULT_SESSION_MANAGER)] private Lazy<ISessionManager> _defaultSessionManager;
        [Import] private Lazy<IUserInteraction> _userInteraction;
#pragma warning restore 649
        private readonly IEventAggregator _eventAggregator;
        private readonly IDocumentsManager _documentsManager;
        private ObservableCollection<Session> _sessions;
        private ICollectionView _sessionsCollectionView;
        private string _sessionsFilePath;
        private bool _restoreLastSession;
        private IDocument _currentDocument;

        #endregion

        #region Properties

        public ICommand DeleteSessionCommand { get; private set; }
        public DelegateCommand SaveCurrentSessionCommand { get; private set; }

        public bool RestoreLastSession
        {
            get { return _restoreLastSession; }
            set
            {
                SetProperty(ref _restoreLastSession, value);
                Settings.Default._restoreLastSession = value;
            }
        }

        public ObservableCollection<Session> Sessions
        {
            get { return _sessions; }
            set { SetProperty(ref _sessions, value); }
        }

        public string SessionsFilePath
        {
            get { return _sessionsFilePath ?? (_sessionsFilePath = Path.Combine(MagicStrings.PathNames.SESSIONS_FOLDER.CombineWithLocalAppDataPath(), MagicStrings.PathNames.ALL_SESSIONS_FILE)); }
        }

        #endregion

        #region Constructors

        [ImportingConstructor]
        public SessionManagerViewModel(ILogger logger, IEventAggregator eventAggregator, IDocumentsManager documentsManager) : base(logger)
        {
            _eventAggregator = eventAggregator;
            _documentsManager = documentsManager;
            Initialize();
            HookEvents();
        }

        #endregion

        #region Methods

        private void HookEvents()
        {
            _eventAggregator.GetEvent<SelectedDocumentChangedEvent>().Subscribe(SelectedDocumentChangedEventHandler);
            _eventAggregator.GetEvent<DocumentWillCloseEvent>().Subscribe(DocumentWillCloseEventHandler);
        }

        public void DocumentWillCloseEventHandler(IDocument document)
        {
            if (document != _currentDocument) return;
            UnHookDocumentEvents(document);
            _currentDocument = null;
        }

        private void UnHookDocumentEvents(IDocument document)
        {
            if (document != null)
            {
                document.PropertyChanged -= DocumentPropertyChangedHandler;
            }
        }

        private void SelectedDocumentChangedEventHandler(SelectedDocumentChangedEvent.Payload payload)
        {
            UnHookDocumentEvents(payload.OldSelection);
            _currentDocument = payload.NewSelection;
            _currentDocument.PropertyChanged += DocumentPropertyChangedHandler;
        }

        private void DocumentPropertyChangedHandler(object sender, PropertyChangedEventArgs e)
        {
            SaveCurrentSessionCommand.RaiseCanExecuteChanged();
        }

        private void Initialize()
        {
            _logger.Log("Initialized {type}", Category.Info, MagicStrings.PLUGIN_CATEGORY, GetType().Name);
            RestoreLastSession = Settings.Default._restoreLastSession;
            DeleteSessionCommand = new DelegateCommand<Session>(DeleteSessionCommandHandler);
            SaveCurrentSessionCommand = new DelegateCommand(SaveCurrentSessionCommandHandler, CanSaveCurrentSession);
            _documentsManager.WorkingDocuments.CollectionChanged += (sender, args) => SaveCurrentSessionCommand.RaiseCanExecuteChanged();
        }

        private bool CanSaveCurrentSession()
        {
            return _documentsManager.WorkingDocuments.FilterValidPaths().Any();
        }

        private async void SaveCurrentSessionCommandHandler()
        {
            var paths = _documentsManager.WorkingDocuments.FilterValidPaths().ToList();
            if (!paths.Any()) return;
            var title = await _userInteraction.Value.PromptInputAsync(Resources._enterSessionNameTitle, Resources._enterSessionNameMessage);
            if (string.IsNullOrWhiteSpace(title)) return;
            var session = new Session
            {
                Title = title,
                DateCreated = DateTime.UtcNow,
                DocumentPaths = paths
            };
            Sessions.Add(session);
            _logger.Log("Saved session {Title}", Category.Info, MagicStrings.PLUGIN_CATEGORY, title);
        }

        private void DeleteSessionCommandHandler(Session session)
        {
            Sessions.Remove(session);
            _logger.Log("Deleted session {Title}", Category.Info, MagicStrings.PLUGIN_CATEGORY, session.Title);
        }

        public void Load()
        {
            if (!DoRestoreLastSession())
            {
                _logger.Log("Cannot restore last session. Delegating to DefaultSessionManager", Category.Info, MagicStrings.PLUGIN_CATEGORY, SessionsFilePath);
                _defaultSessionManager.Value.Load();
            }
            LoadOldSessions();
        }

        private void LoadOldSessions()
        {
            Sessions = new ObservableCollection<Session>();
            if (File.Exists(SessionsFilePath))
            {
                var data = File.ReadAllText(SessionsFilePath);
                Sessions = JsonConvert.DeserializeObject<ObservableCollection<Session>>(data);
            }
            _sessionsCollectionView = CollectionViewSource.GetDefaultView(Sessions);
            _sessionsCollectionView.MoveCurrentTo(null);
            _sessionsCollectionView.CurrentChanged += SessionCurrentChangedHandler;
        }

        private async void SessionCurrentChangedHandler(object sender, EventArgs e)
        {
            var session = _sessionsCollectionView.CurrentItem as Session;
            if (session == null) return;
            _sessionsCollectionView.CurrentChanged -= SessionCurrentChangedHandler;
            var confirmation = await _documentsManager.CloseAllAsync();
            if (confirmation == UserPromptResult.Yes)
            {
                _documentsManager.AddFromPaths(session.DocumentPaths);
            }
            _logger.Log("Loaded session {Title}", Category.Info, MagicStrings.PLUGIN_CATEGORY, session.Title);
            await Task.Delay(100);
            _sessionsCollectionView.MoveCurrentTo(null);
            _sessionsCollectionView.CurrentChanged += SessionCurrentChangedHandler;
        }

        private bool DoRestoreLastSession()
        {
            var lastSessionPaths = Settings.Default._lastSession;
            if (!RestoreLastSession || lastSessionPaths == null || lastSessionPaths.Count == 0) return false;
            try
            {
                _documentsManager.AddFromPaths(lastSessionPaths.Cast<string>());
                return true;
            }
            catch (Exception ex)
            {
                Settings.Default._lastSession = new StringCollection();
                _logger.Log("Error restoring last session. {message}", Category.Exception, MagicStrings.PLUGIN_CATEGORY, ex.Message);
                return false;
            }
        }

        public void Dispose()
        {
            SaveLastSession();
            SaveAllSessions();
        }

        private void SaveAllSessions()
        {
            if (Sessions == null || !Sessions.Any())
            {
                File.Delete(SessionsFilePath);
                _logger.Log("No sessions. Deleting session file", Category.Info, MagicStrings.PLUGIN_CATEGORY);
                return;
            }
            var serialized = JsonConvert.SerializeObject(Sessions, Formatting.Indented);
            WriteToFile(SessionsFilePath, serialized);
            _logger.Log("{count} sessions saved", Category.Info, MagicStrings.PLUGIN_CATEGORY, Sessions.Count);
        }

        private void SaveLastSession()
        {
            var paths = new StringCollection();
            paths.AddRange(_documentsManager.RecentlyClosedDocuments.ToArray());
            Settings.Default._lastSession = paths;
            Settings.Default.Save();
            _logger.Log("Saved last session", Category.Info, MagicStrings.PLUGIN_CATEGORY);
        }

        public static bool WriteToFile(string path, string data)
        {
            try
            {
                var directoryName = Path.GetDirectoryName(path);
                if (directoryName == null) return false;
                if (!Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }

                File.WriteAllText(path, data);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion
    }
}