using System;
using System.Linq;

using Java.Interop.Tools.Cecil;

using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;
using MonoDroid.Tuner;

namespace Microsoft.Android.Sdk.ILLink
{
	class PreserveRegistrations : BaseMarkHandler
	{
		public override void Initialize (LinkContext context, MarkContext markContext)
		{
			base.Initialize (context, markContext);
			markContext.RegisterMarkMethodAction (method => ProcessMethod (method));
		}

		bool IsActiveFor (AssemblyDefinition assembly)
		{
			return assembly.Name.Name == "Mono.Android" || assembly.MainModule.HasTypeReference ("Android.Runtime.RegisterAttribute");
		}

		bool PreserveJniMarshalMethods ()
		{
			if (Context.TryGetCustomData ("XAPreserveJniMarshalMethods", out var boolValue))
				return bool.Parse (boolValue);

			return false;
		}

		protected int PreserveNamedMethod (TypeDefinition type, string method_name, MethodDefinition key)
		{
			if (!type.HasMethods)
				return 0;

			int count = 0;
			foreach (MethodDefinition method in type.Methods) {
				if (method.Name != method_name)
					continue;

				AddPreservedMethod (key, method);
				count++;
			}

			return count;
		}

		void PreserveRegisteredMethod (TypeDefinition type, string member, MethodDefinition key)
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

			while (PreserveNamedMethod (type, member, key) == 0 && type.BaseType != null)
				type = type.BaseType.Resolve ();
		}

		void ProcessMethod (MethodDefinition method)
		{
			if (!IsActiveFor (method.Module.Assembly))
				return;

			bool preserveJniMarshalMethodOnly = false;
			if (!method.TryGetRegisterMember (out var member, out var nativeMethod, out var signature)) {
				if (PreserveJniMarshalMethods () &&
						method.DeclaringType.GetMarshalMethodsType () != null &&
						method.TryGetBaseOrInterfaceRegisterMember (cache, out member, out nativeMethod, out signature)) {
					preserveJniMarshalMethodOnly = true;
				} else {
					return;
				}
			}

			if (PreserveJniMarshalMethods () && method.TryGetMarshalMethod (nativeMethod, signature, out var marshalMethod)) {
				AddPreservedMethod (method, marshalMethod);
				// TODO: collect marshalTypes and process after MarkStep
				// marshalTypes.Add (marshalMethod.DeclaringType);
			}

			if (preserveJniMarshalMethodOnly)
				return;

			PreserveRegisteredMethod (method.DeclaringType, member, method);
		}

		void AddPreservedMethod (MethodDefinition key, MethodDefinition method)
		{
			Annotations.AddPreservedMethod (key, method);
		}
	}
}
