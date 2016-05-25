using System;
using System.IO;

namespace MonoDroid.Generation {

	class XamarinAndroidCodeGenerator : CodeGenerator {

		internal override void WriteClassHandle (ClassGen type, StreamWriter sw, string indent, CodeGenerationOptions opt, bool requireNew)
		{

			sw.WriteLine ("{0}\tinternal static {1}IntPtr java_class_handle;", indent, requireNew ? "new " : string.Empty);
			sw.WriteLine ("{0}\tinternal static {1}IntPtr class_ref {{", indent, requireNew ? "new " : string.Empty);
			sw.WriteLine ("{0}\t\tget {{", indent);
			sw.WriteLine ("{0}\t\t\treturn JNIEnv.FindClass (\"{1}\", ref java_class_handle);", indent, type.RawJniName);
			sw.WriteLine ("{0}\t\t}}", indent);
			sw.WriteLine ("{0}\t}}", indent);
			sw.WriteLine ();
			if (type.BaseGen != null && type.InheritsObject) {
				sw.WriteLine ("{0}\tprotected override IntPtr ThresholdClass {{", indent);
				sw.WriteLine ("{0}\t\tget {{ return class_ref; }}", indent);
				sw.WriteLine ("{0}\t}}", indent);
				sw.WriteLine ();
				sw.WriteLine ("{0}\tprotected override global::System.Type ThresholdType {{", indent);
				sw.WriteLine ("{0}\t\tget {{ return typeof ({1}); }}", indent, type.Name);
				sw.WriteLine ("{0}\t}}", indent);
				sw.WriteLine ();
			}
		}

		internal override void WriteClassHandle (InterfaceGen type, StreamWriter sw, string indent, CodeGenerationOptions opt, string declaringType)
		{
			sw.WriteLine ("{0}static IntPtr class_ref = JNIEnv.FindClass (\"{1}\");", indent, type.RawJniName);
		}

		internal override void WriteClassInvokerHandle (ClassGen type, StreamWriter sw, string indent, CodeGenerationOptions opt, string declaringType)
		{
			sw.WriteLine ("{0}protected override global::System.Type ThresholdType {{", indent);
			sw.WriteLine ("{0}\tget {{ return typeof ({1}); }}", indent, declaringType);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
		}

		internal override void WriteInterfaceInvokerHandle (InterfaceGen type, StreamWriter sw, string indent, CodeGenerationOptions opt, string declaringType)
		{
			sw.WriteLine ("{0}static IntPtr java_class_ref = JNIEnv.FindClass (\"{1}\");", indent, type.RawJniName);
			sw.WriteLine ();
			sw.WriteLine ("{0}protected override IntPtr ThresholdClass {{", indent);
			sw.WriteLine ("{0}\tget {{ return class_ref; }}", indent);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
			sw.WriteLine ("{0}protected override global::System.Type ThresholdType {{", indent);
			sw.WriteLine ("{0}\tget {{ return typeof ({1}); }}", indent, declaringType);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
		}

		internal override void WriteConstructorIdField (Ctor ctor, StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			sw.WriteLine ("{0}static IntPtr {1};", indent, ctor.ID);
		}

		internal override void WriteConstructorBody (Ctor ctor, StreamWriter sw, string indent, CodeGenerationOptions opt, System.Collections.Specialized.StringCollection call_cleanup)
		{
			sw.WriteLine ("{0}if (Handle != IntPtr.Zero)", indent);
			sw.WriteLine ("{0}\treturn;", indent);
			sw.WriteLine ();
			foreach (string prep in ctor.Parameters.GetCallPrep (opt))
				sw.WriteLine ("{0}{1}", indent, prep);
			sw.WriteLine ("{0}try {{", indent);
			var oldindent = indent;
			indent += "\t";
			ctor.Parameters.WriteCallArgs (sw, indent, opt, invoker:false);
			sw.WriteLine ("{0}if (GetType () != typeof ({1})) {{", indent, ctor.Name);
			sw.WriteLine ("{0}\tSetHandle (", indent);
			sw.WriteLine ("{0}\t\t\tglobal::Android.Runtime.JNIEnv.StartCreateInstance (GetType (), \"{1}\"{2}),",
					indent,
					ctor.IsNonStaticNestedType ? "(" + ctor.Parameters.JniNestedDerivedSignature + ")V" : ctor.JniSignature,
					ctor.Parameters.GetCallArgs (opt, invoker:false));
			sw.WriteLine ("{0}\t\t\tJniHandleOwnership.TransferLocalRef);", indent);
			sw.WriteLine ("{0}\tglobal::Android.Runtime.JNIEnv.FinishCreateInstance (Handle, \"{1}\"{2});",
					indent,
					ctor.IsNonStaticNestedType ? "(" + ctor.Parameters.JniNestedDerivedSignature + ")V" : ctor.JniSignature,
					ctor.Parameters.GetCallArgs (opt, invoker:false));
			sw.WriteLine ("{0}\treturn;", indent);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
			sw.WriteLine ("{0}if ({1} == IntPtr.Zero)", indent, ctor.ID);
			sw.WriteLine ("{0}\t{1} = JNIEnv.GetMethodID (class_ref, \"<init>\", \"{2}\");", indent, ctor.ID, ctor.JniSignature);
			sw.WriteLine ("{0}SetHandle (", indent);
			sw.WriteLine ("{0}\t\tglobal::Android.Runtime.JNIEnv.StartCreateInstance (class_ref, {1}{2}),",
					indent, ctor.ID, ctor.Parameters.GetCallArgs (opt, invoker:false));
			sw.WriteLine ("{0}\t\tJniHandleOwnership.TransferLocalRef);", indent);
			sw.WriteLine ("{0}JNIEnv.FinishCreateInstance (Handle, class_ref, {1}{2});",
					indent, ctor.ID, ctor.Parameters.GetCallArgs (opt, invoker:false));
			indent = oldindent;
			sw.WriteLine ("{0}}} finally {{", indent);
			foreach (string cleanup in call_cleanup)
				sw.WriteLine ("{0}\t{1}", indent, cleanup);
			sw.WriteLine ("{0}}}", indent);
		}

		internal override void WriteMethodIdField (Method method, StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			sw.WriteLine ("{0}static IntPtr {1};", indent, method.EscapedIdName);
		}

		void GenerateJNICall (Method method, StreamWriter sw, string indent, CodeGenerationOptions opt, string call, bool declare_ret)
		{
			if (method.IsVoid)
				sw.WriteLine ("{0}{1};", indent, call);
			else if (method.Parameters.HasCleanup)
				sw.WriteLine ("{0}{1}__ret = {2};", indent, declare_ret ? opt.GetOutputName (method.RetVal.FullName) + " " : String.Empty, method.RetVal.FromNative (opt, call, true));
			else
				sw.WriteLine ("{0}return {1};", indent, method.RetVal.FromNative (opt, call, true));
		}

		internal override void WriteMethodBody (Method method, StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			sw.WriteLine ("{0}if ({1} == IntPtr.Zero)", indent, method.EscapedIdName);
			sw.WriteLine ("{0}\t{1} = JNIEnv.Get{2}MethodID (class_ref, \"{3}\", \"{4}\");", indent, method.EscapedIdName, method.IsStatic ? "Static" : String.Empty, method.JavaName, method.JniSignature);
			bool use_non_virtual = method.IsVirtual && !method.IsAbstract;
			foreach (string prep in method.Parameters.GetCallPrep (opt))
				sw.WriteLine ("{0}{1}", indent, prep);
			sw.WriteLine ("{0}try {{", indent);
			var oldindent = indent;
			indent += "\t";
			method.Parameters.WriteCallArgs (sw, indent, opt, invoker: false);
			if (method.IsStatic) {
				GenerateJNICall (method, sw, indent, opt, "JNIEnv.CallStatic" + method.RetVal.CallMethodPrefix + "Method  (class_ref, " + method.EscapedIdName + method.Parameters.GetCallArgs (opt, invoker:false) + ")", true);
			} else if (use_non_virtual) {
				sw.WriteLine ();
				if (!method.IsVoid && method.Parameters.HasCleanup)
					sw.WriteLine ("{0}{1} __ret;", indent, opt.GetOutputName (method.RetVal.FullName));
				sw.WriteLine ("{0}if (GetType () == ThresholdType)", indent);
				GenerateJNICall (method, sw, indent + "\t", opt, "JNIEnv.Call" + method.RetVal.CallMethodPrefix + "Method  (Handle, " + method.EscapedIdName + method.Parameters.GetCallArgs (opt, invoker:false) + ")", false);
				sw.WriteLine ("{0}else", indent);
				GenerateJNICall (method, sw, indent + "\t", opt,
						"JNIEnv.CallNonvirtual" + method.RetVal.CallMethodPrefix + "Method  (Handle, ThresholdClass, " +
						string.Format ("JNIEnv.GetMethodID (ThresholdClass, \"{0}\", \"{1}\")", method.JavaName, method.JniSignature) +
						method.Parameters.GetCallArgs (opt, invoker:false) + ")",
						false);
			} else {
				GenerateJNICall (method, sw, indent, opt, "JNIEnv.Call" + method.RetVal.CallMethodPrefix + "Method  (Handle, " + method.EscapedIdName + method.Parameters.GetCallArgs (opt, invoker:false) + ")", true);
			}

			if (!method.IsVoid && method.Parameters.HasCleanup)
				sw.WriteLine ("{0}return __ret;", indent);
			indent = oldindent;
			sw.WriteLine ("{0}}} finally {{", indent);
			foreach (string cleanup in method.Parameters.GetCallCleanup (opt))
				sw.WriteLine ("{0}\t{1}", indent, cleanup);
			sw.WriteLine ("{0}}}", indent);
		}

		internal override void WriteFieldIdField (Field field, StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			sw.WriteLine ("{0}static IntPtr {1};", indent, field.ID);
		}

		internal override void WriteFieldGetBody (Field field, StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			sw.WriteLine ("{0}if ({1} == IntPtr.Zero)", indent, field.ID);
			sw.WriteLine ("{0}\t{1} = JNIEnv.Get{2}FieldID (class_ref, \"{3}\", \"{4}\");", indent, field.ID, field.IsStatic ? "Static" : String.Empty, field.JavaName, field.Symbol.JniName);
			string call = String.Format ("JNIEnv.Get{0}{1}Field ({2}, {3})", field.IsStatic ? "Static" : String.Empty, field.GetMethodPrefix, field.IsStatic ? "class_ref" : "Handle", field.ID);

			//var asym = Symbol as ArraySymbol;
			if (field.Symbol.IsArray) {
				sw.WriteLine ("{0}return global::Android.Runtime.JavaArray<{1}>.FromJniHandle ({2}, JniHandleOwnership.TransferLocalRef);", indent, opt.GetOutputName (field.Symbol.ElementType), call);
			}
			else if (field.Symbol.NativeType != field.Symbol.FullName) {
				sw.WriteLine ("{0}{1} __ret = {2};", indent, field.Symbol.NativeType, call);
				sw.WriteLine ("{0}return {1};", indent, field.Symbol.FromNative (opt, "__ret", true));
			} else {
				sw.WriteLine ("{0}return {1};", indent, call);
			}
		}

		internal override void WriteFieldSetBody (Field field, StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			sw.WriteLine ("{0}if ({1} == IntPtr.Zero)", indent, field.ID);
			sw.WriteLine ("{0}\t{1} = JNIEnv.Get{2}FieldID (class_ref, \"{3}\", \"{4}\");", indent, field.ID, field.IsStatic ? "Static" : String.Empty, field.JavaName, field.Symbol.JniName);

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
					sw.WriteLine ("{0}IntPtr {1} = JNIEnv.ToLocalJniHandle (value);", indent, arg);
				}
			}

			sw.WriteLine ("{0}try {{", indent);

			sw.WriteLine ("{0}\tJNIEnv.Set{1}Field ({2}, {3}, {4});", indent, field.IsStatic ? "Static" : String.Empty, field.IsStatic ? "class_ref" : "Handle", field.ID, arg);

			sw.WriteLine ("{0}}} finally {{", indent);
			if (field.Symbol.IsArray) {
				sw.WriteLine ("{0}\tJNIEnv.DeleteLocalRef ({1});", indent, arg);

			} else {
				foreach (string cleanup in field.SetParameters.GetCallCleanup (opt))
					sw.WriteLine ("{0}\t{1}", indent, cleanup);
				if (field.SetParameters.HasCleanup && !have_prep) {
					sw.WriteLine ("{0}\tJNIEnv.DeleteLocalRef ({1});", indent, arg);
				}
			}
			sw.WriteLine ("{0}}}", indent);
		}
	}
}

