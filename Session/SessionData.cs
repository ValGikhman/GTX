using GTX.Models;
using System;

namespace GTX.Session {
    public class SessionData: ISessionData {

        #region Private Fields

        private Vehicle[] _inventory = null;
        private Employer[] _employers = null;
        private Vehicle _currentVehicle = null;
        private Filters _filters = null;

        private readonly HttpContextProvider _httpContext;

        #endregion Private Fields

        #region Public Constructors

        public SessionData(HttpContextProvider httpContext) {
            _httpContext = httpContext;
        }


        #endregion Public Constructors

        #region Public Properties
        public Vehicle[] Vehicles {
            get => GetSession(Constants.SESSION_INVENTORY, _inventory);
            set => SetSession(Constants.SESSION_INVENTORY, value);
        }

        public Employer[] Employers {
            get => GetSession(Constants.SESSION_EMPLOYERS, _employers);
            set => SetSession(Constants.SESSION_EMPLOYERS, value);
        }

        public Vehicle CurrentVehicle {
            get => GetSession(Constants.SESSION_CURRENT_VEHICLE, _currentVehicle);
            set => SetSession(Constants.SESSION_CURRENT_VEHICLE, value);
        }

        public Filters Filters {
            get => GetSession(Constants.SESSION_FILTERS, _filters);
            set => SetSession(Constants.SESSION_FILTERS, value);
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