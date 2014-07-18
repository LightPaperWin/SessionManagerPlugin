#region Using

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using LightPaper.Infrastructure;
using LightPaper.Infrastructure.Contracts;
using Microsoft.Practices.Prism.Logging;
using Microsoft.Practices.Prism.Mvvm;
using Newtonsoft.Json;
using PuppyFramework.Helpers;
using PuppyFramework.Interfaces;
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
#pragma warning restore 649
        private readonly IDocumentsManager _documentsManager;
        private ObservableCollection<Session> _sessions;
        private string _sessionsFilePath;
        private bool _restoreLastSession;

        #endregion

        #region Properties

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
        public SessionManagerViewModel(ILogger logger, IDocumentsManager documentsManager) : base(logger)
        {
            _documentsManager = documentsManager;
            Initialize();
        }

        private void Initialize()
        {
            _logger.Log("Initialized {type}", Category.Info, MagicStrings.PLUGIN_CATEGORY, GetType().Name);
            RestoreLastSession = Settings.Default._restoreLastSession;
        }

        #endregion

        #region Methods

        public void Load()
        {
            if (!DoRestoreLastSession())
            {
                _logger.Log("Cannot restore last session. Delegating to DefaultSessionManager", Category.Info, MagicStrings.PLUGIN_CATEGORY, SessionsFilePath);
                _defaultSessionManager.Value.Load();
            }
        }

        private bool DoRestoreLastSession()
        {
            var lastSessionPaths = Settings.Default._lastSession;
            if (!Settings.Default._restoreLastSession || lastSessionPaths == null || lastSessionPaths.Count == 0) return false;
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
            if (Sessions == null || !Sessions.Any()) return;
            var serialized = JsonConvert.SerializeObject(Sessions, Formatting.Indented);
            WriteToFile(SessionsFilePath, serialized);
        }

        private void SaveLastSession()
        {
            var paths = new StringCollection();
            paths.AddRange(_documentsManager.WorkingDocuments.Select(d => d.SourcePath).Where(p => !string.IsNullOrWhiteSpace(p)).ToArray());
            Settings.Default._lastSession = paths;
            Settings.Default.Save();
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

    internal class Session : BindableBase
    {
        #region Fields 

        private DateTime _dateCreated;
        private List<string> _documentPaths;
        private string _title;

        #endregion

        #region Constructors 

        public Session()
        {
            DocumentPaths = new List<string>();
            _dateCreated = DateTime.UtcNow;
        }

        #endregion

        #region Properties 

        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        public List<string> DocumentPaths
        {
            get { return _documentPaths; }
            set { SetProperty(ref _documentPaths, value); }
        }

        public DateTime DateCreated { get; set; }

        #endregion

        #region Methods

        public bool Add(string path)
        {
            if (!File.Exists(path)) return false;
            DocumentPaths.Add(path);
            return true;
        }

        #endregion
    }
}