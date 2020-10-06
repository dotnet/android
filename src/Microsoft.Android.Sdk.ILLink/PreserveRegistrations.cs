using System;
using System.Linq;

using Java.Interop.Tools.Cecil;

using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;
using MonoDroid.Tuner;

namespace Microsoft.Android.Sdk.ILLink
{
	class PreserveRegistrations : BaseSubStep
	{
		delegate void AddPreservedMethodDelegate (AnnotationStore store, MethodDefinition key, MethodDefinition method);

		static readonly AddPreservedMethodDelegate addPreservedMethod;

		readonly TypeDefinitionCache cache;

		public PreserveRegistrations (TypeDefinitionCache cache) => this.cache = cache;

		static PreserveRegistrations ()
		{
			// temporarily use reflection to get void AnnotationStore::AddPreservedMethod (MethodDefinition key, MethodDefinition method)
			// this can be removed once we have newer Microsoft.NET.ILLink containing https://github.com/mono/linker/commit/e6dadc995a834603e1178f9a1918f0ae38056b29
			var method = typeof (AnnotationStore).GetMethod ("AddPreservedMethod", new Type [] { typeof (MethodDefinition), typeof (MethodDefinition) });
			addPreservedMethod = (AddPreservedMethodDelegate)(method != null ? Delegate.CreateDelegate (typeof (AddPreservedMethodDelegate), null, method, false) : null);
		}

		public override bool IsActiveFor (AssemblyDefinition assembly)
		{
			return addPreservedMethod != null && (assembly.Name.Name == "Mono.Android" || assembly.MainModule.HasTypeReference ("Android.Runtime.RegisterAttribute"));
		}

		public override SubStepTargets Targets { get { return SubStepTargets.Method;  } }

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

		public override void ProcessMethod (MethodDefinition method)
		{
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
			addPreservedMethod.Invoke (Context.Annotations, key, method);
		}
	}
}
