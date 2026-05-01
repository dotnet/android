using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Android.Runtime
{
	public static class ResourceIdManager
	{
		static bool id_initialized;
		[RequiresUnreferencedCode ("Resource designer lookup uses a type name from ResourceDesignerAttribute that cannot be statically traced.")]
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

		[RequiresUnreferencedCode ("Resource designer lookup uses a type name from ResourceDesignerAttribute that cannot be statically traced.")]
		[return: DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
		static Type? GetResourceTypeFromAssembly (Assembly assembly)
		{
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
