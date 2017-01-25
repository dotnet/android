using System;
using System.Linq;
using Mono.Cecil;

using Java.Interop.Tools.Cecil;

using Mono.Linker.Steps;

namespace MonoDroid.Tuner
{
	class MonoDroidMarkStep : MarkStep
	{
		const string RegisterAttribute = "Android.Runtime.RegisterAttribute";
		const string ICustomMarshalerName = "System.Runtime.InteropServices.ICustomMarshaler";

		// If this is one of our infrastructure methods that has [Register], like:
		// [Register ("hasWindowFocus", "()Z", "GetHasWindowFocusHandler")],
		// we need to preserve the "GetHasWindowFocusHandler" method as well.
		protected override void DoAdditionalMethodProcessing (MethodDefinition method)
		{
			string member;

			if (!TryGetRegisterMember (method, out member))
				return;

			PreserveRegisteredMethod (method.DeclaringType, member);
		}

		protected override void DoAdditionalTypeProcessing (TypeDefinition type)
		{
			// If we are preserving a Mono.Android interface,
			// preserve all members on the interface.
			if (!type.IsInterface)
				return;

			// Mono.Android interfaces will always inherit IJavaObject
			if (!type.ImplementsIJavaObject ())
				return;

			foreach (MethodReference method in type.Methods)
				MarkMethod (method);

			// We also need to preserve the interface adapter
			string adapter;

			if (!TryGetRegisterMember (type, out adapter))
				return;

			// This is a fast path for finding the adapter type
			var adapter_type = type.DeclaringType.GetNestedType (adapter);

			// It wasn't in the same containing type, look through the whole assembly
			if (adapter_type == null)
				adapter_type = type.Module.FindType (adapter);

			if (adapter_type == null)
				return;

			MarkType (adapter_type);

			// We also need to preserve every member on the interface adapter
			foreach (MethodReference m in adapter_type.Methods)
				MarkMethod (m);
		}
		
		private void PreserveRegisteredMethod (TypeDefinition type, string member)
		{
			var type_ptr = type;
			var pos = member.IndexOf (':');

			if (pos > 0) {
				var type_name = member.Substring (pos + 1);
				member = member.Substring (0, pos);
				type_ptr = type_ptr.Module.Types.FirstOrDefault (t => t.FullName == type_name);
			}

			if (type_ptr == null)
				return;

			while (MarkNamedMethod (type, member) == 0 && type.BaseType != null)
				type = type.BaseType.Resolve ();
		}

		private static bool TryGetRegisterMember (ICustomAttributeProvider provider, out string method)
		{
			CustomAttribute register;
			method = null;

			if (!TryGetRegisterAttribute (provider, out register))
				return false;

			if (register.ConstructorArguments.Count != 3)
				return false;

			method = (string) register.ConstructorArguments [2].Value;

			if (string.IsNullOrEmpty (method))
				return false;

			return true;
		}

		private static bool TryGetRegisterAttribute (ICustomAttributeProvider provider, out CustomAttribute register)
		{
			register = null;

			if (!provider.HasCustomAttributes)
				return false;

			foreach (CustomAttribute attribute in provider.CustomAttributes) {
				if (!IsRegisterAttribute (attribute))
					continue;

				register = attribute;
				return true;
			}

			return false;
		}

		private static bool IsRegisterAttribute (CustomAttribute attribute)
		{
			var constructor = attribute.Constructor;

			if (constructor.DeclaringType.FullName != RegisterAttribute)
				return false;

			if (!constructor.HasParameters)
				return false;

			if (constructor.Parameters.Count != 3)
				return false;

			return true;
		}

		protected override TypeDefinition MarkType (TypeReference reference)
		{
			TypeDefinition type = base.MarkType (reference);
			if (type == null)
				return null;

			if (type.Module.Assembly.Name.Name == "System.Core")
				ProcessSystemCore (type);

			if (type.HasMethods && type.HasInterfaces && type.Implements (ICustomMarshalerName)) {
				foreach (MethodDefinition method in type.Methods) {
					if (method.Name == "GetInstance" && method.IsStatic && method.HasParameters && method.Parameters.Count == 1 && method.ReturnType.FullName == ICustomMarshalerName && method.Parameters.First ().ParameterType.FullName == "System.String") {
						MarkMethod (method);
						break;
					}
				}
			}

			return type;
		}

		void ProcessSystemCore (TypeDefinition type)
		{
			switch (type.Namespace) {
			case "System.Linq.Expressions":
				switch (type.Name) {
				case "LambdaExpression":
					var expr_t = type.Module.GetType ("System.Linq.Expressions.Expression`1");
					if (expr_t != null)
						MarkNamedMethod (expr_t, "Create");
					break;
				}
				break;
			case "System.Linq.Expressions.Compiler":
				switch (type.Name) {
				case "LambdaCompiler":
					MarkNamedMethod (type.Module.GetType ("System.Runtime.CompilerServices.RuntimeOps"), "Quote");
					break;
				}
				break;
			}
		}

		public override void Process (Mono.Linker.LinkContext context)
		{
			base.Process (context);

			// deal with [TypeForwardedTo] pseudo-attributes
			foreach (AssemblyDefinition assembly in _context.GetAssemblies ()) {
				if (!assembly.MainModule.HasExportedTypes)
					continue;

				foreach (var exported in assembly.MainModule.ExportedTypes) {
					bool isForwarder = exported.IsForwarder;
					var declaringType = exported.DeclaringType;
					while (!isForwarder && (declaringType != null)) {
						isForwarder = declaringType.IsForwarder;
						declaringType = declaringType.DeclaringType;
					}

					if (!isForwarder)
						continue;
					var type = exported.Resolve ();
					if (!Annotations.IsMarked (type))
						continue;
					Annotations.Mark (exported);
					Annotations.Mark (assembly.MainModule);
				}
			}
		}
	}
}
