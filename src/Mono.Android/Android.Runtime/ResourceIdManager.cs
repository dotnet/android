using System;
using System.Diagnostics.CodeAnalysis;
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
			id_initialized = true;
			var executingAssembly = Assembly.GetExecutingAssembly ();
			var type = executingAssembly != null ? GetResourceTypeFromAssembly (executingAssembly) : null;
			if (type == null) {
				foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies ()) {
					type = GetResourceTypeFromAssembly (assembly);
					if (type != null) {
						break;
					}
				}
			}
			if (type != null) {
				var method = type.GetMethod ("UpdateIdValues");
				if (method != null) {
					var action = (Action) method.CreateDelegate (typeof (Action));
					action ();
				}
			}
		}

		[return: DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
		static Type? GetResourceTypeFromAssembly (Assembly assembly)
		{
			const string rootAssembly = "Resources.UpdateIdValues() methods are trimmed away by the LinkResourceDesigner trimmer step. This codepath is not called unless $(AndroidUseDesignerAssembly) is disabled.";

			[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = rootAssembly)]
			[UnconditionalSuppressMessage ("Trimming", "IL2073", Justification = rootAssembly)]
			[return: DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
			static Type AssemblyGetType (Assembly a, string name) => a.GetType (name);

			foreach (var customAttribute in assembly.GetCustomAttributes (typeof (ResourceDesignerAttribute), true)) {
				if (customAttribute is ResourceDesignerAttribute resourceDesignerAttribute && resourceDesignerAttribute.IsApplication) {
					var type = AssemblyGetType (assembly, resourceDesignerAttribute.FullName);
					if (type != null)
						return type;
				}
			}
			return null;
		}
	}
}

