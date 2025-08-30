using GTX.Session;
using Services;
using System.Web.Mvc;
using Unity;
using Unity.Mvc5;

namespace GTX
{
    public static class UnityConfig
    {
        public static void RegisterComponents()
        {
			var container = new UnityContainer();
            
           
            DependencyResolver.SetResolver(new UnityDependencyResolver(container));
            container.RegisterType<IEmployeesService, EmployyesService>();
            container.RegisterType<IContactService, ContactService>();
            container.RegisterType<ILogService, LogService>();
            container.RegisterType<IInventoryService, InventoryService>();
            container.RegisterType<IHttpContextProvider, HttpContextProvider>();
            container.RegisterType<ISessionData, SessionData>();
        }
    }
}