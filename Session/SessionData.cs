using GTX.Models;
using Services;
using System;
using System.Collections.Generic;

namespace GTX.Session {
    public class SessionData: ISessionData {

        #region Private Fields

        private readonly Log _logHeader = null;
        private readonly CommonUnit.Environment _environment = CommonUnit.Environment.Prod;
        private readonly CommonUnit.Responsibility _responsibility = CommonUnit.Responsibility.Site;
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
        public CommonUnit.Environment Environment {
            get => GetSession(Constants.SESSION_ENVIRONMENT, _environment);
            set => SetSession(Constants.SESSION_ENVIRONMENT, value);
        }

        public CommonUnit.Responsibility Responsibility {
            get => GetSession(Constants.SESSION_RESPONSIBILITY, _responsibility);
            set => SetSession(Constants.SESSION_RESPONSIBILITY, value);
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
            var value = _httpContext.Current.Session[key];
            return value is T ? (T)value : defaultValue;
        }

        public void SetSession(string key, Object data) {
            _httpContext.Current.Session[key] = data;
        }

        #endregion Public Methods
    }
}
