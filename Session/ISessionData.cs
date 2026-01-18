using GTX.Models;
using Services;
using System;
using System.Collections.Generic;

namespace GTX {

    public interface ISessionData {

        #region Public Properties
        string Environment { get; set; }

        bool? IsMajordome { get; set; }

        Log LogHeader { get; set; }

        #endregion Public Properties

        #region Public Methods

        T GetSession<T>(string key);

        T GetSession<T>(string key, T defaultValue);

        void SetSession(string key, Object data);

        #endregion Public Methods
    }
}