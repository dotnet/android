using System;
using System.Linq;
using System.Reflection;

namespace Android.Runtime
{
	public static class ResourceIdManager
	{
		static bool id_initialized;
		public static void UpdateIdValues ()
		{
			if (id_initialized)
				return;
			var executingAssembly = Assembly.GetExecutingAssembly ();
			Func<Assembly,Type> f = assembly =>
				assembly.GetCustomAttributes (typeof (ResourceDesignerAttribute), true)
					.Select (ca => ca as ResourceDesignerAttribute)
					.Where (ca => ca != null && ca.IsApplication)
					.Select (ca => assembly.GetType (ca.FullName))
					.Where (ty => ty != null)
					.FirstOrDefault ();
			var t = executingAssembly != null ? f (executingAssembly) : null;
			if (t == null) {
				t = AppDomain.CurrentDomain.GetAssemblies ()
					.Select (assembly => f (assembly))
					.Where (ty => ty != null)
					.FirstOrDefault ();
			}
			if (t != null)
				t.GetMethod ("UpdateIdValues").Invoke (null, new object [0]);
			id_initialized = true;
		}
	}
}

