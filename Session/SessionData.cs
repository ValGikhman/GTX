using GTX.EZ360;
using GTX.Models;
using Services;
using System;
using System.Collections.Generic;

namespace GTX.Session {
    public class SessionData: ISessionData {

        #region Private Fields

        private readonly Log _logHeader = null;
        private readonly string _environment = null;
        private readonly bool? _isMajordome = false;

        private readonly HttpContextProvider _httpContext;

        #endregion Private Fields

        #region Public Constructors

        public SessionData(HttpContextProvider httpContext) {
            _httpContext = httpContext;

            LogHeader = new Log {
                Url = _httpContext.Current.Request.Path,
                HttpMethod = _httpContext.Current.Request.HttpMethod,
                UserAgent = _httpContext.Current.Request.Headers["User-Agent"].ToString(),
                IPAddress = _httpContext.Current?.Request?.UserHostAddress.ToString()
            };
        }


        #endregion Public Constructors

        #region Public Properties
        public string Environment {
            get => GetSession(Constants.SESSION_ENVIRONMENT, _environment);
            set => SetSession(Constants.SESSION_ENVIRONMENT, value);
        }

        public bool? IsMajordome {
            get => (bool)GetSession(Constants.SESSION_MAJORDOME, _isMajordome);
            set => SetSession(Constants.SESSION_MAJORDOME, value);
        }


        public Log LogHeader {
            get => GetSession(Constants.SESSION_LOG_HEADER, _logHeader);
            set => SetSession(Constants.SESSION_LOG_HEADER, value);
        }

        #endregion Public Properties

        #region Public Methods

        public T GetSession<T>(string key) {
            return GetSession<T>(key, default(T));
        }

        public T GetSession<T>(string key, T defaultValue) {
            T retVal;

            retVal = (T)_httpContext.Current.Session[key];
            if (retVal == null) {
                retVal = defaultValue;
            };

            return retVal;
        }

        public void SetSession(string key, Object data) {
            _httpContext.Current.Session[key] = data;
        }

        #endregion Public Methods
    }
}