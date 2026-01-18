using GTX.EZ360;
using GTX.Models;
using Services;
using System;
using System.Collections.Generic;

namespace GTX.Session {
    public class SessionData: ISessionData {

        #region Private Fields

        private readonly Inventory _inventory = null;
        private readonly Employer[] _employers = null;
        private readonly Filters _currentFilter = null;
        private readonly Models.Vehicle _currentVehicle = null;
        private readonly Filters _filters = null;
        private readonly Log _logHeader = null;
        private readonly OpenHours[] _openHours = null;
        private readonly string _environment = null;
        private readonly bool? _isMajordome = false;
        private readonly List<BlogPostModel> _blogs = null;

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

        public Inventory Inventory {
            get => GetSession(Constants.SESSION_INVENTORY, _inventory);
            set => SetSession(Constants.SESSION_INVENTORY, value);
        }

        public Filters CurrentFilter {
            get => GetSession(Constants.SESSION_CURRENT_FILTER, _currentFilter);
            set => SetSession(Constants.SESSION_CURRENT_FILTER, value);
        }

        public Models.Vehicle CurrentVehicle {
            get => GetSession(Constants.SESSION_CURRENT_VEHICLE, _currentVehicle);
            set => SetSession(Constants.SESSION_CURRENT_VEHICLE, value);
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
            get => GetSession(Constants.SESSION_OPEN_HOURS, _openHours);
            set => SetSession(Constants.SESSION_OPEN_HOURS, value);
        }

        public List<BlogPostModel> Blogs
        {
            get => GetSession(Constants.SESSION_BLOGS, _blogs);
            set => SetSession(Constants.SESSION_BLOGS, value);
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