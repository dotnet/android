using System;
using System.IO;
using Mono.Options;

namespace MonoDroid.Generation {

	class JavaInteropCodeGenerator : CodeGenerator {

		public JavaInteropCodeGenerator (TextWriter writer, CodeGenerationOptions options) : base (writer, options)
		{
		}

		static string GetInvokeType (string type)
		{
			switch (type) {
			case "Bool":            return "Boolean";
			case "Byte":            return "SByte";
			case "Int":             return "Int32";
			case "Short":           return "Int16";
			case "Long":            return "Int64";
			case "Float":           return "Single";
			default:                return type;
			}
		}

		internal override string GetAllInterfaceImplements () => "IJavaObject, IJavaPeerable";

		protected virtual string GetPeerMembersType () => "JniPeerMembers";

		internal override void WriteClassHandle (ClassGen type, string indent, bool requireNew)
		{
			WritePeerMembers (indent + '\t', true, requireNew, type.RawJniName, type.Name, false);

			writer.WriteLine ("{0}\tinternal static {1}IntPtr class_ref {{", indent, requireNew ? "new " : string.Empty);
			writer.WriteLine ("{0}\t\tget {{", indent);
			writer.WriteLine ("{0}\t\t\treturn _members.JniPeerType.PeerReference.Handle;", indent);
			writer.WriteLine ("{0}\t\t}}", indent);
			writer.WriteLine ("{0}\t}}", indent);
			writer.WriteLine ();
			if (type.BaseGen != null && type.InheritsObject) {
				writer.WriteLine ("{0}\tpublic override global::Java.Interop.JniPeerMembers JniPeerMembers {{", indent);
				writer.WriteLine ("{0}\t\tget {{ return _members; }}", indent);
				writer.WriteLine ("{0}\t}}", indent);
				writer.WriteLine ();
				writer.WriteLine ("{0}\tprotected override IntPtr ThresholdClass {{", indent);
				writer.WriteLine ("{0}\t\tget {{ return _members.JniPeerType.PeerReference.Handle; }}", indent);
				writer.WriteLine ("{0}\t}}", indent);
				writer.WriteLine ();
				writer.WriteLine ("{0}\tprotected override global::System.Type ThresholdType {{", indent);
				writer.WriteLine ("{0}\t\tget {{ return _members.ManagedPeerType; }}", indent);
				writer.WriteLine ("{0}\t}}", indent);
				writer.WriteLine ();
			}
		}

		internal override void WriteClassHandle (InterfaceGen type, string indent, string declaringType)
		{
			WritePeerMembers (indent, false, true, type.RawJniName, declaringType, type.Name == declaringType);
		}

		internal override void WriteClassInvokerHandle (ClassGen type, string indent, string declaringType)
		{
			WritePeerMembers (indent, true, true, type.RawJniName, declaringType, false);

			writer.WriteLine ();
			writer.WriteLine ("{0}public override global::Java.Interop.JniPeerMembers JniPeerMembers {{", indent);
			writer.WriteLine ("{0}\tget {{ return _members; }}", indent);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
			writer.WriteLine ("{0}protected override global::System.Type ThresholdType {{", indent);
			writer.WriteLine ("{0}\tget {{ return _members.ManagedPeerType; }}", indent);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		internal override void WriteInterfaceInvokerHandle (InterfaceGen type, string indent, string declaringType)
		{
			WritePeerMembers (indent, true, true, type.RawJniName, declaringType, false);

			writer.WriteLine ();
			writer.WriteLine ("{0}static IntPtr java_class_ref {{", indent);
			writer.WriteLine ("{0}\tget {{ return _members.JniPeerType.PeerReference.Handle; }}", indent);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
			writer.WriteLine ("{0}public override global::Java.Interop.JniPeerMembers JniPeerMembers {{", indent);
			writer.WriteLine ("{0}\tget {{ return _members; }}", indent);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
			writer.WriteLine ("{0}protected override IntPtr ThresholdClass {{", indent);
			writer.WriteLine ("{0}\tget {{ return class_ref; }}", indent);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
			writer.WriteLine ("{0}protected override global::System.Type ThresholdType {{", indent);
			writer.WriteLine ("{0}\tget {{ return _members.ManagedPeerType; }}", indent, declaringType);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		internal override void WriteConstructorIdField (Ctor ctor, string indent)
		{
			// No method id_ctor field required; it's now an `id` constant in the binding.
		}

		internal override void WriteConstructorBody (Ctor ctor, string indent, System.Collections.Specialized.StringCollection call_cleanup)
		{
			writer.WriteLine ("{0}{1}string __id = \"{2}\";",
					indent,
					ctor.IsNonStaticNestedType ? "" : "const ",
					ctor.IsNonStaticNestedType
					? "(" + ctor.Parameters.JniNestedDerivedSignature + ")V"
					: ctor.JniSignature);
			writer.WriteLine ();
			writer.WriteLine ("{0}if ({1} != IntPtr.Zero)", indent, Context.ContextType.GetObjectHandleProperty ("this"));
			writer.WriteLine ("{0}\treturn;", indent);
			writer.WriteLine ();
			foreach (string prep in ctor.Parameters.GetCallPrep (opt))
				writer.WriteLine ("{0}{1}", indent, prep);
			writer.WriteLine ("{0}try {{", indent);
			var oldindent = indent;
			indent += "\t";
			WriteParameterListCallArgs (ctor.Parameters, indent, invoker: false);
			writer.WriteLine ("{0}var __r = _members.InstanceMethods.StartCreateInstance (__id, ((object) this).GetType (){1});", indent, ctor.Parameters.GetCallArgs (opt, invoker:false));
			writer.WriteLine ("{0}SetHandle (__r.Handle, JniHandleOwnership.TransferLocalRef);", indent);
			writer.WriteLine ("{0}_members.InstanceMethods.FinishCreateInstance (__id, this{1});", indent, ctor.Parameters.GetCallArgs (opt, invoker:false));
			indent = oldindent;
			writer.WriteLine ("{0}}} finally {{", indent);
			foreach (string cleanup in call_cleanup)
				writer.WriteLine ("{0}\t{1}", indent, cleanup);
			writer.WriteLine ("{0}}}", indent);
		}

		internal override void WriteMethodIdField (Method method, string indent)
		{
			// No method id_ field required; it's now an `id` constant in the binding.
		}

		internal override void WriteMethodBody (Method method, string indent, GenBase type)
		{
			writer.WriteLine ("{0}const string __id = \"{1}.{2}\";", indent, method.JavaName, method.JniSignature);
			foreach (string prep in method.Parameters.GetCallPrep (opt))
				writer.WriteLine ("{0}{1}", indent, prep);
			writer.WriteLine ("{0}try {{", indent);
			var oldindent = indent;
			indent += "\t";
			WriteParameterListCallArgs (method.Parameters, indent, invoker: false);

			var invokeType  = GetInvokeType (method.RetVal.CallMethodPrefix);

			writer.Write (indent);
			if (!method.IsVoid) {
				writer.Write ("var __rm = ");
			}

			if (method.IsStatic) {
				writer.WriteLine ("_members.StaticMethods.Invoke{0}Method (__id{1});",
						invokeType,
						method.Parameters.GetCallArgs (opt, invoker: false));
			} else if (method.IsFinal) {
				writer.WriteLine ("_members.InstanceMethods.InvokeNonvirtual{0}Method (__id, this{1});",
						invokeType,
						method.Parameters.GetCallArgs (opt, invoker: false));
			} else if ((method.IsVirtual && !method.IsAbstract) || method.IsInterfaceDefaultMethod) {
				writer.WriteLine ("_members.InstanceMethods.InvokeVirtual{0}Method (__id, this{1});",
						invokeType,
						method.Parameters.GetCallArgs (opt, invoker: false));
			} else {
				writer.WriteLine ("_members.InstanceMethods.InvokeAbstract{0}Method (__id, this{1});",
						invokeType,
						method.Parameters.GetCallArgs (opt, invoker: false));
			}

			if (!method.IsVoid) {
				var r   = invokeType == "Object" ? "__rm.Handle" : "__rm";
				writer.WriteLine ("{0}return {1};", indent, method.RetVal.FromNative (opt, r, true));
			}

			indent = oldindent;
			writer.WriteLine ("{0}}} finally {{", indent);
			foreach (string cleanup in method.Parameters.GetCallCleanup (opt))
				writer.WriteLine ("{0}\t{1}", indent, cleanup);
			writer.WriteLine ("{0}}}", indent);
		}

		internal override void WriteFieldIdField (Field field, string indent)
		{
			// No field id_ field required
		}

		internal override void WriteFieldGetBody (Field field, string indent, GenBase type)
		{
			writer.WriteLine ("{0}const string __id = \"{1}.{2}\";", indent, field.JavaName, field.Symbol.JniName);
			writer.WriteLine ();

			var invokeType  = GetInvokeType (field.GetMethodPrefix);
			var indirect    = field.IsStatic ? "StaticFields" : "InstanceFields";
			var invoke      = "Get{0}Value";
			invoke          = string.Format (invoke, invokeType);

			writer.WriteLine ("{0}var __v = _members.{1}.{2} (__id{3});",
					indent,
					indirect,
					invoke,
					field.IsStatic ? "" : ", this");

			if (field.Symbol.IsArray) {
				writer.WriteLine ("{0}return global::Android.Runtime.JavaArray<{1}>.FromJniHandle (__v.Handle, JniHandleOwnership.TransferLocalRef);", indent, opt.GetOutputName (field.Symbol.ElementType));
			}
			else if (field.Symbol.NativeType != field.Symbol.FullName) {
				writer.WriteLine ("{0}return {1};",
						indent,
						field.Symbol.FromNative (opt, invokeType != "Object" ? "__v" : "__v.Handle", true));
			} else {
				writer.WriteLine ("{0}return __v;", indent);
			}
		}

		internal override void WriteFieldSetBody (Field field, string indent, GenBase type)
		{
			writer.WriteLine ("{0}const string __id = \"{1}.{2}\";", indent, field.JavaName, field.Symbol.JniName);
			writer.WriteLine ();

			var invokeType  = GetInvokeType (field.GetMethodPrefix);
			var indirect    = field.IsStatic ? "StaticFields" : "InstanceFields";

			string arg;
			bool have_prep = false;
			if (field.Symbol.IsArray) {
				arg = opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName ("value"));
				writer.WriteLine ("{0}IntPtr {1} = global::Android.Runtime.JavaArray<{2}>.ToLocalJniHandle (value);", indent, arg, opt.GetOutputName (field.Symbol.ElementType));
			} else {
				foreach (string prep in field.SetParameters.GetCallPrep (opt)) {
					have_prep = true;
					writer.WriteLine ("{0}{1}", indent, prep);
				}

				arg = field.SetParameters [0].ToNative (opt);
				if (field.SetParameters.HasCleanup && !have_prep) {
					arg = opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName ("value"));
					writer.WriteLine ("{0}IntPtr {1} = global::Android.Runtime.JNIEnv.ToLocalJniHandle (value);", indent, arg);
				}
			}

			writer.WriteLine ("{0}try {{", indent);

			writer.WriteLine ("{0}\t_members.{1}.SetValue (__id{2}, {3});",
					indent,
					indirect,
					field.IsStatic ? "" : ", this",
					invokeType != "Object" ? arg : "new JniObjectReference (" + arg + ")");

			writer.WriteLine ("{0}}} finally {{", indent);
			if (field.Symbol.IsArray) {
				writer.WriteLine ("{0}\tglobal::Android.Runtime.JNIEnv.DeleteLocalRef ({1});", indent, arg);

			} else {
				foreach (string cleanup in field.SetParameters.GetCallCleanup (opt))
					writer.WriteLine ("{0}\t{1}", indent, cleanup);
				if (field.SetParameters.HasCleanup && !have_prep) {
					writer.WriteLine ("{0}\tglobal::Android.Runtime.JNIEnv.DeleteLocalRef ({1});", indent, arg);
				}
			}
			writer.WriteLine ("{0}}}", indent);
		}

		void WritePeerMembers (string indent, bool isInternal, bool isNew, string rawJniType, string declaringType, bool isInterface)
		{
			var signature = $"{(isInternal ? "internal " : "")}static {(isNew ? "new " : "")}readonly JniPeerMembers _members = ";
			var type = $"new {GetPeerMembersType ()} (\"{rawJniType}\", typeof ({declaringType}){(isInterface ? ", isInterface: true" : string.Empty)});";

			writer.WriteLine ($"{indent}{signature}{type}");
		}
	}
}

