using GTX.Models;
using Services;
using System;

namespace GTX.Session {
    public class SessionData: ISessionData {

        #region Private Fields

        private Inventory _inventory = null;
        private Employer[] _employers = null;
        private Filters _currentFilter = null;
        private Filters _filters = null;
        private Log _logHeader = null;
        private OpenHours[] _openHours = null;

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
        public Log LogHeader {
            get => GetSession(Constants.SESSION_LOG_HEADER, _logHeader);
            set => SetSession(Constants.SESSION_LOG_HEADER, value);
        }

        public Inventory Inventory {
            get => GetSession(Constants.SESSION_INVENTORY, _inventory);
            set => SetSession(Constants.SESSION_INVENTORY, value);
        }

        public Filters CurrentFilter {
            get => GetSession(Constants.SESSION_CURRENT_FILTER, _currentFilter);
            set => SetSession(Constants.SESSION_CURRENT_FILTER, value);
        }

        public Employer[] Employers {
            get => GetSession(Constants.SESSION_EMPLOYERS, _employers);
            set => SetSession(Constants.SESSION_EMPLOYERS, value);
        }

        public Filters Filters {
            get => GetSession(Constants.SESSION_FILTERS, _filters);
            set => SetSession(Constants.SESSION_FILTERS, value);
        }
        public OpenHours[] OpenHours {
            get => GetSession(Constants.SESSION_OOPEN_HOURS, _openHours);
            set => SetSession(Constants.SESSION_OOPEN_HOURS, value);
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