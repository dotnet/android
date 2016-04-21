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
			var eass = Assembly.GetExecutingAssembly ();
			Func<Assembly,Type> f = ass =>
				ass.GetCustomAttributes (typeof (ResourceDesignerAttribute), true)
					.Select (ca => ca as ResourceDesignerAttribute)
					.Where (ca => ca != null && ca.IsApplication)
					.Select (ca => ass.GetType (ca.FullName))
					.Where (ty => ty != null)
					.FirstOrDefault ();
			var t = eass != null ? f (eass) : null;
			if (t == null)
				t = AppDomain.CurrentDomain.GetAssemblies ().Select (ass => f (ass)).Where (ty => ty != null).FirstOrDefault ();
			if (t != null)
				t.GetMethod ("UpdateIdValues").Invoke (null, new object [0]);
			id_initialized = true;
		}
	}
}

