using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Practices.Prism.Mvvm;

namespace SessionManager.Models
{
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