using GTX.Models;
using Services;
using System;

namespace GTX {

    public interface ISessionData {

        #region Public Properties
        Inventory Inventory { get; set; }

        Employer[] Employers { get; set; }

        Filters Filters { get; set; }

        Filters CurrentFilter { get; set; }
        Log LogHeader { get; set; }

        #endregion Public Properties

        #region Public Methods

        T GetSession<T>(String key);

        T GetSession<T>(String key, T defaultValue);

        void SetSession(String key, Object data);

        #endregion Public Methods
    }
}