using System;
using System.IO;

namespace MonoDroid.Generation {

	class JavaInteropCodeGenerator : CodeGenerator {

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


		internal override void WriteClassHandle (ClassGen type, StreamWriter sw, string indent, CodeGenerationOptions opt, bool requireNew)
		{
			sw.WriteLine ("{0}\tinternal    {1}     static  readonly    JniPeerMembers  _members    = new {2} (\"{3}\", typeof ({4}));",
					indent,
					requireNew ? "new" : "   ",
					GetPeerMembersType (),
					type.RawJniName,
					type.Name);
			sw.WriteLine ("{0}\tinternal static {1}IntPtr class_ref {{", indent, requireNew ? "new " : string.Empty);
			sw.WriteLine ("{0}\t\tget {{", indent);
			sw.WriteLine ("{0}\t\t\treturn _members.JniPeerType.PeerReference.Handle;", indent);
			sw.WriteLine ("{0}\t\t}}", indent);
			sw.WriteLine ("{0}\t}}", indent);
			sw.WriteLine ();
			if (type.BaseGen != null && type.InheritsObject) {
				sw.WriteLine ("{0}\tpublic override global::Java.Interop.JniPeerMembers JniPeerMembers {{", indent);
				sw.WriteLine ("{0}\t\tget {{ return _members; }}", indent);
				sw.WriteLine ("{0}\t}}", indent);
				sw.WriteLine ();
				sw.WriteLine ("{0}\tprotected override IntPtr ThresholdClass {{", indent);
				sw.WriteLine ("{0}\t\tget {{ return _members.JniPeerType.PeerReference.Handle; }}", indent);
				sw.WriteLine ("{0}\t}}", indent);
				sw.WriteLine ();
				sw.WriteLine ("{0}\tprotected override global::System.Type ThresholdType {{", indent);
				sw.WriteLine ("{0}\t\tget {{ return _members.ManagedPeerType; }}", indent);
				sw.WriteLine ("{0}\t}}", indent);
				sw.WriteLine ();
			}
		}

		protected virtual string GetPeerMembersType ()
		{
			return "JniPeerMembers";
		}

		internal override void WriteClassHandle (InterfaceGen type, StreamWriter sw, string indent, CodeGenerationOptions opt, string declaringType)
		{
			sw.WriteLine ("{0}static JniPeerMembers _members = new JniPeerMembers (\"{1}\", typeof ({2}));",indent, type.RawJniName, declaringType);
		}

		internal override void WriteClassInvokerHandle (ClassGen type, StreamWriter sw, string indent, CodeGenerationOptions opt, string declaringType)
		{
			sw.WriteLine ("{0}internal    new     static  readonly    JniPeerMembers  _members    = new JniPeerMembers (\"{1}\", typeof ({2}));",
					indent,
					type.RawJniName,
					declaringType);
			sw.WriteLine ();
			sw.WriteLine ("{0}public override global::Java.Interop.JniPeerMembers JniPeerMembers {{", indent);
			sw.WriteLine ("{0}\tget {{ return _members; }}", indent);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
			sw.WriteLine ("{0}protected override global::System.Type ThresholdType {{", indent);
			sw.WriteLine ("{0}\tget {{ return _members.ManagedPeerType; }}", indent);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
		}

		internal override void WriteInterfaceInvokerHandle (InterfaceGen type, StreamWriter sw, string indent, CodeGenerationOptions opt, string declaringType)
		{
			sw.WriteLine ("{0}internal    new     static  readonly    JniPeerMembers  _members    = new JniPeerMembers (\"{1}\", typeof ({2}));",
					indent,
					type.RawJniName,
					declaringType);
			sw.WriteLine ();
			sw.WriteLine ("{0}static IntPtr java_class_ref {{", indent);
			sw.WriteLine ("{0}\tget {{ return _members.JniPeerType.PeerReference.Handle; }}", indent);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
			sw.WriteLine ("{0}public override global::Java.Interop.JniPeerMembers JniPeerMembers {{", indent);
			sw.WriteLine ("{0}\tget {{ return _members; }}", indent);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
			sw.WriteLine ("{0}protected override IntPtr ThresholdClass {{", indent);
			sw.WriteLine ("{0}\tget {{ return class_ref; }}", indent);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
			sw.WriteLine ("{0}protected override global::System.Type ThresholdType {{", indent);
			sw.WriteLine ("{0}\tget {{ return _members.ManagedPeerType; }}", indent, declaringType);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
		}

		internal override void WriteConstructorIdField (Ctor ctor, StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			// No method id_ctor field required; it's now an `id` constant in the binding.
		}

		internal override void WriteConstructorBody (Ctor ctor, StreamWriter sw, string indent, CodeGenerationOptions opt, System.Collections.Specialized.StringCollection call_cleanup)
		{
			sw.WriteLine ("{0}{1}string __id = \"{2}\";",
					indent,
					ctor.IsNonStaticNestedType ? "" : "const ",
					ctor.IsNonStaticNestedType
					? "(" + ctor.Parameters.JniNestedDerivedSignature + ")V"
					: ctor.JniSignature);
			sw.WriteLine ();
			sw.WriteLine ("{0}if ({1} != IntPtr.Zero)", indent, opt.ContextType.GetObjectHandleProperty ("this"));
			sw.WriteLine ("{0}\treturn;", indent);
			sw.WriteLine ();
			foreach (string prep in ctor.Parameters.GetCallPrep (opt))
				sw.WriteLine ("{0}{1}", indent, prep);
			sw.WriteLine ("{0}try {{", indent);
			var oldindent = indent;
			indent += "\t";
			ctor.Parameters.WriteCallArgs (sw, indent, opt, invoker:false);
			sw.WriteLine ("{0}var __r = _members.InstanceMethods.StartCreateInstance (__id, ((object) this).GetType (){1});", indent, ctor.Parameters.GetCallArgs (opt, invoker:false));
			sw.WriteLine ("{0}SetHandle (__r.Handle, JniHandleOwnership.TransferLocalRef);", indent);
			sw.WriteLine ("{0}_members.InstanceMethods.FinishCreateInstance (__id, this{1});", indent, ctor.Parameters.GetCallArgs (opt, invoker:false));
			indent = oldindent;
			sw.WriteLine ("{0}}} finally {{", indent);
			foreach (string cleanup in call_cleanup)
				sw.WriteLine ("{0}\t{1}", indent, cleanup);
			sw.WriteLine ("{0}}}", indent);
		}

		internal override void WriteMethodIdField (Method method, StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			// No method id_ field required; it's now an `id` constant in the binding.
		}

		internal override void WriteMethodBody (Method method, StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			sw.WriteLine ("{0}const string __id = \"{1}.{2}\";", indent, method.JavaName, method.JniSignature);
			foreach (string prep in method.Parameters.GetCallPrep (opt))
				sw.WriteLine ("{0}{1}", indent, prep);
			sw.WriteLine ("{0}try {{", indent);
			var oldindent = indent;
			indent += "\t";
			method.Parameters.WriteCallArgs (sw, indent, opt, invoker: false);

			var invokeType  = GetInvokeType (method.RetVal.CallMethodPrefix);

			sw.Write (indent);
			if (!method.IsVoid) {
				sw.Write ("var __rm = ");
			}

			if (method.IsStatic) {
				sw.WriteLine ("_members.StaticMethods.Invoke{0}Method (__id{1});",
						invokeType,
						method.Parameters.GetCallArgs (opt, invoker: false));
			} else if (method.IsFinal) {
				sw.WriteLine ("_members.InstanceMethods.InvokeNonvirtual{0}Method (__id, this{1});",
						invokeType,
						method.Parameters.GetCallArgs (opt, invoker: false));
			} else if (method.IsVirtual && !method.IsAbstract) {
				sw.WriteLine ("_members.InstanceMethods.InvokeVirtual{0}Method (__id, this{1});",
						invokeType,
						method.Parameters.GetCallArgs (opt, invoker: false));
			} else {
				sw.WriteLine ("_members.InstanceMethods.InvokeAbstract{0}Method (__id, this{1});",
						invokeType,
						method.Parameters.GetCallArgs (opt, invoker: false));
			}

			if (!method.IsVoid) {
				var r   = invokeType == "Object" ? "__rm.Handle" : "__rm";
				sw.WriteLine ("{0}return {1};", indent, method.RetVal.FromNative (opt, r, true));
			}

			indent = oldindent;
			sw.WriteLine ("{0}}} finally {{", indent);
			foreach (string cleanup in method.Parameters.GetCallCleanup (opt))
				sw.WriteLine ("{0}\t{1}", indent, cleanup);
			sw.WriteLine ("{0}}}", indent);
		}

		internal override void WriteFieldIdField (Field field, StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			// No field id_ field required
		}

		internal override void WriteFieldGetBody (Field field, StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			sw.WriteLine ("{0}const string __id = \"{1}.{2}\";", indent, field.JavaName, field.Symbol.JniName);
			sw.WriteLine ();

			var invokeType  = GetInvokeType (field.GetMethodPrefix);
			var indirect    = field.IsStatic ? "StaticFields" : "InstanceFields";
			var invoke      = "Get{0}Value";
			invoke          = string.Format (invoke, invokeType);

			sw.WriteLine ("{0}var __v = _members.{1}.{2} (__id{3});",
					indent,
					indirect,
					invoke,
					field.IsStatic ? "" : ", this");

			if (field.Symbol.IsArray) {
				sw.WriteLine ("{0}return global::Android.Runtime.JavaArray<{1}>.FromJniHandle (__v.Handle, JniHandleOwnership.TransferLocalRef);", indent, opt.GetOutputName (field.Symbol.ElementType));
			}
			else if (field.Symbol.NativeType != field.Symbol.FullName) {
				sw.WriteLine ("{0}return {1};",
						indent,
						field.Symbol.FromNative (opt, invokeType != "Object" ? "__v" : "__v.Handle", true));
			} else {
				sw.WriteLine ("{0}return __v;", indent);
			}
		}

		internal override void WriteFieldSetBody (Field field, StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			sw.WriteLine ("{0}const string __id = \"{1}.{2}\";", indent, field.JavaName, field.Symbol.JniName);
			sw.WriteLine ();

			var invokeType  = GetInvokeType (field.GetMethodPrefix);
			var indirect    = field.IsStatic ? "StaticFields" : "InstanceFields";

			string arg;
			bool have_prep = false;
			if (field.Symbol.IsArray) {
				arg = opt.GetSafeIdentifier (SymbolTable.GetNativeName ("value"));
				sw.WriteLine ("{0}IntPtr {1} = global::Android.Runtime.JavaArray<{2}>.ToLocalJniHandle (value);", indent, arg, opt.GetOutputName (field.Symbol.ElementType));
			} else {
				foreach (string prep in field.SetParameters.GetCallPrep (opt)) {
					have_prep = true;
					sw.WriteLine ("{0}{1}", indent, prep);
				}

				arg = field.SetParameters [0].ToNative (opt);
				if (field.SetParameters.HasCleanup && !have_prep) {
					arg = opt.GetSafeIdentifier (SymbolTable.GetNativeName ("value"));
					sw.WriteLine ("{0}IntPtr {1} = global::Android.Runtime.JNIEnv.ToLocalJniHandle (value);", indent, arg);
				}
			}

			sw.WriteLine ("{0}try {{", indent);

			sw.WriteLine ("{0}\t_members.{1}.SetValue (__id{2}, {3});",
					indent,
					indirect,
					field.IsStatic ? "" : ", this",
					invokeType != "Object" ? arg : "new JniObjectReference (" + arg + ")");

			sw.WriteLine ("{0}}} finally {{", indent);
			if (field.Symbol.IsArray) {
				sw.WriteLine ("{0}\tglobal::Android.Runtime.JNIEnv.DeleteLocalRef ({1});", indent, arg);

			} else {
				foreach (string cleanup in field.SetParameters.GetCallCleanup (opt))
					sw.WriteLine ("{0}\t{1}", indent, cleanup);
				if (field.SetParameters.HasCleanup && !have_prep) {
					sw.WriteLine ("{0}\tglobal::Android.Runtime.JNIEnv.DeleteLocalRef ({1});", indent, arg);
				}
			}
			sw.WriteLine ("{0}}}", indent);
		}
	}
}

