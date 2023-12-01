using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Java.Interop
{
	static class StringCoda {
		public static string FixupType (this string t)
		{
			return t.Replace ("*", "Ptr").Replace ("[]", "Array").Replace (" ", "");
		}
	}

	partial class Generator
	{
		static string jnienv_g_c;
		static string jnienv_g_h;
		static string jnienv_g_cs;

		public static int Main (string [] args)
		{
			jnienv_g_c  = "JniEnvironment.g.c";
			jnienv_g_h  = "JniEnvironment.g.h";
			jnienv_g_cs = "JniEnvironment.g.cs";
			if (args.Length > 0)
				jnienv_g_cs = args [0];
			if (args.Length > 1) {
				jnienv_g_c = args [1];
				if (jnienv_g_c != "-") {
					jnienv_g_h = Path.Combine (Path.GetDirectoryName (jnienv_g_c), $"{Path.GetFileNameWithoutExtension(jnienv_g_c)}-api.h");
				} else {
					jnienv_g_h = "-";
				}
			}
			if (args.Length > 2) {
				jnienv_g_h = args [2];
			}

			try {
				using (TextWriter w = new StringWriter ()) {
					w.NewLine = "\n";
					GenerateFile (w);
					string content = w.ToString ();
					if (jnienv_g_cs == "-")
						Console.WriteLine (content);
					else
						File.WriteAllText (jnienv_g_cs, content);
				}
				using (TextWriter sw = new StringWriter ()) {
				using (TextWriter hw = new StringWriter ()) {
					sw.NewLine = "\n";
					GenerateNativeLibSource (sw, hw, jnienv_g_h);
					string sourceContent = sw.ToString ();
					string headerContent = hw.ToString ();
					if (jnienv_g_c == "-" || jnienv_g_cs == "-") {
						Console.WriteLine (headerContent);
						Console.WriteLine ();
						Console.WriteLine (sourceContent);
					} else {
						File.WriteAllText (jnienv_g_h, headerContent);
						File.WriteAllText (jnienv_g_c, sourceContent);
					}
				}}
				return 0;
			} catch (Exception ex) {
				Console.WriteLine (ex);
				return 1;
			}
		}

		static string Escape (string value)
		{
			switch (value) {
			case "object":
			case "string":
			case "ref":
				return "@" + value;
			default: return value;
			}
		}

		static void GenerateFile (TextWriter o)
		{
			o.WriteLine ("// Generated file; DO NOT EDIT!");
			o.WriteLine ("//");
			o.WriteLine ("// To make changes, edit monodroid/tools/jnienv-gen-interop and rerun");
			o.WriteLine ("#nullable enable");
			o.WriteLine ();
			o.WriteLine ("#if !FEATURE_JNIENVIRONMENT_SAFEHANDLES && !FEATURE_JNIENVIRONMENT_JI_INTPTRS && !FEATURE_JNIENVIRONMENT_JI_PINVOKES && !FEATURE_JNIENVIRONMENT_XA_INTPTRS && !FEATURE_JNIENVIRONMENT_JI_FUNCTION_POINTERS");
			o.WriteLine ("#define FEATURE_JNIENVIRONMENT_JI_PINVOKES");
			o.WriteLine ("#endif  // !FEATURE_JNIENVIRONMENT_SAFEHANDLES && !FEATURE_JNIENVIRONMENT_JI_INTPTRS && !FEATURE_JNIENVIRONMENT_JI_PINVOKES && !FEATURE_JNIENVIRONMENT_XA_INTPTRS");
			o.WriteLine ();
			o.WriteLine ("#if FEATURE_JNIENVIRONMENT_SAFEHANDLES && FEATURE_JNIENVIRONMENT_JI_INTPTRS");
			o.WriteLine ("#define _NAMESPACE_PER_HANDLE");
			o.WriteLine ("#endif  // FEATURE_JNIENVIRONMENT_SAFEHANDLES && FEATURE_JNIENVIRONMENT_JI_INTPTRS");
			o.WriteLine ("#if FEATURE_JNIENVIRONMENT_SAFEHANDLES && FEATURE_JNIENVIRONMENT_JI_PINVOKES");
			o.WriteLine ("#define _NAMESPACE_PER_HANDLE");
			o.WriteLine ("#endif  // FEATURE_JNIENVIRONMENT_SAFEHANDLES && FEATURE_JNIENVIRONMENT_JI_PINVOKES");
			o.WriteLine ("#if FEATURE_JNIENVIRONMENT_SAFEHANDLES && FEATURE_JNIENVIRONMENT_XA_INTPTRS");
			o.WriteLine ("#define _NAMESPACE_PER_HANDLE");
			o.WriteLine ("#endif  // FEATURE_JNIENVIRONMENT_SAFEHANDLES && FEATURE_JNIENVIRONMENT_XA_INTPTRS");
			o.WriteLine ();
			o.WriteLine ("using System;");
			o.WriteLine ("using System.Linq;");
			o.WriteLine ("using System.Runtime.ExceptionServices;");
			o.WriteLine ("using System.Runtime.InteropServices;");
			o.WriteLine ("using System.Threading;");
			o.WriteLine ();
			o.WriteLine ("using Java.Interop;");
			o.WriteLine ();
			o.WriteLine ("using JNIEnvPtr          = System.IntPtr;");
			o.WriteLine ();
			o.WriteLine ("#if FEATURE_JNIENVIRONMENT_JI_INTPTRS || FEATURE_JNIENVIRONMENT_JI_PINVOKES || FEATURE_JNIENVIRONMENT_JI_FUNCTION_POINTERS");
			o.WriteLine ("\tusing jinstanceFieldID   = System.IntPtr;");
			o.WriteLine ("\tusing jstaticFieldID     = System.IntPtr;");
			o.WriteLine ("\tusing jinstanceMethodID  = System.IntPtr;");
			o.WriteLine ("\tusing jstaticMethodID    = System.IntPtr;");
			o.WriteLine ("\tusing jobject            = System.IntPtr;");
			o.WriteLine ("#endif  // FEATURE_JNIENVIRONMENT_JI_INTPTRS || FEATURE_JNIENVIRONMENT_JI_PINVOKES || FEATURE_JNIENVIRONMENT_JI_FUNCTION_POINTERS");
			o.WriteLine ();
			o.WriteLine ("namespace Java.Interop {");
			GenerateJniNativeInterface (o);
			o.WriteLine ("}");
			WriteSection (o, HandleStyle.SafeHandle,                "FEATURE_JNIENVIRONMENT_SAFEHANDLES",               "Java.Interop.SafeHandles");
			WriteSection (o, HandleStyle.JIIntPtr,                  "FEATURE_JNIENVIRONMENT_JI_INTPTRS",                "Java.Interop.JIIntPtrs");
			WriteSection (o, HandleStyle.JIIntPtrPinvokeWithErrors, "FEATURE_JNIENVIRONMENT_JI_PINVOKES",               "Java.Interop.JIPinvokes");
			WriteSection (o, HandleStyle.XAIntPtr,                  "FEATURE_JNIENVIRONMENT_XA_INTPTRS",                "Java.Interop.XAIntPtrs");
			WriteSection (o, HandleStyle.JIFunctionPtrWithErrors,   "FEATURE_JNIENVIRONMENT_JI_FUNCTION_POINTERS",      "Java.Interop.JIFunctionPointers");
		}

		static void WriteSection (TextWriter o, HandleStyle style, string define, string specificNamespace)
		{
			o.WriteLine ("#if {0}", define);
			o.WriteLine ("namespace");
			o.WriteLine ("#if _NAMESPACE_PER_HANDLE");
			o.WriteLine ("\t{0}", specificNamespace);
			o.WriteLine ("#else");
			o.WriteLine ("\tJava.Interop");
			o.WriteLine ("#endif");
			o.WriteLine ("{");
			o.WriteLine ();
			if (style != HandleStyle.JIIntPtrPinvokeWithErrors && style != HandleStyle.JIFunctionPtrWithErrors) {
				GenerateDelegates (o, style);
				o.WriteLine ();
			}
			GenerateTypes (o, style);
			o.WriteLine ();
			switch (style) {
			case HandleStyle.JIIntPtr:
			case HandleStyle.SafeHandle:
			case HandleStyle.XAIntPtr:
				GenerateJniNativeInterfaceInvoker (o, style);
				break;
			}
			o.WriteLine ("}");
			o.WriteLine ("#endif  // {0}", define);
		}
		
		static void GenerateDelegates (TextWriter o, HandleStyle style)
		{
			created_delegates   = new HashSet<string> ();
			foreach (var e in JNIEnvEntries) {
				CreateDelegate (o, e, style);
			}
		}

		static void GenerateJniNativeInterface (TextWriter o)
		{
			o.WriteLine ("#pragma warning disable 0649	// Field is assigned to, and will always have its default value `null`; ignore as it'll be set in native code.");
			o.WriteLine ("#pragma warning disable 0169	// Field never used; ignore since these fields make the structure have the right layout.");
			o.WriteLine ();

			o.WriteLine ("#if FEATURE_JNIENVIRONMENT_SAFEHANDLES || FEATURE_JNIENVIRONMENT_JI_INTPTRS || FEATURE_JNIENVIRONMENT_XA_INTPTRS");
			o.WriteLine ("\t[StructLayout (LayoutKind.Sequential)]");
			o.WriteLine ("\tpartial struct JniNativeInterfaceStruct {");
			o.WriteLine ();

			int maxName = JNIEnvEntries.Max (e => e.Name.Length);

			for (int i = 0; i < 4; i++)
				o.WriteLine ("\t\tprivate IntPtr  reserved{0};                      // void*", i);

			foreach (var e in JNIEnvEntries) {
				o.WriteLine ("\t\tpublic  IntPtr  {0};{1}  // {2}", e.Name, new string (' ', maxName - e.Name.Length), e.Prototype);
			}
			o.WriteLine ("\t}");
			o.WriteLine ("#endif  // FEATURE_JNIENVIRONMENT_SAFEHANDLES || FEATURE_JNIENVIRONMENT_JI_INTPTRS || FEATURE_JNIENVIRONMENT_XA_INTPTRS");
			o.WriteLine ();

			o.WriteLine ("#if FEATURE_JNIENVIRONMENT_JI_FUNCTION_POINTERS");
			o.WriteLine ("\t[StructLayout (LayoutKind.Sequential)]");
			o.WriteLine ("\tunsafe partial struct JNIEnv {");

			for (int i = 0; i < 4; i++)
				o.WriteLine ("\t\tprivate IntPtr  reserved{0};                      // void*", i);

			foreach (var e in JNIEnvEntries) {
				if (e.Parameters.Length > 0 &&
						"va_list" == e.Parameters [e.Parameters.Length-1].Type.GetManagedType (HandleStyle.JIFunctionPtrWithErrors, isReturn: false, isPinvoke: true)) {
					o.WriteLine ("\t\tpublic  IntPtr  {0};{1}  // {2}", e.Name, new string (' ', maxName - e.Name.Length), e.Prototype);
					continue;
				}
				o.Write ("\t\tpublic  delegate* unmanaged <IntPtr /* env */");
				foreach (var p in e.Parameters) {
					o.Write (", ");
					o.Write (p.Type.GetMarshalType (HandleStyle.JIFunctionPtrWithErrors, isReturn: false, isPinvoke: true));
					o.Write ($" /* {p.Name} */");
				}
				o.Write (", ");
				o.Write (e.ReturnType.GetMarshalType (HandleStyle.JIFunctionPtrWithErrors, isReturn: true, isPinvoke: true));
				o.WriteLine ($"> {e.Name};");
			}
			o.WriteLine ("\t}");
			o.WriteLine ("#endif  // FEATURE_JNIENVIRONMENT_JI_FUNCTION_POINTERS");

			o.WriteLine ();
			o.WriteLine ("#pragma warning restore 0169");
			o.WriteLine ("#pragma warning restore 0649");
		}

		static string Initialize (JniFunction e, string prefix, string delegateType)
		{
			return string.Format ("{0}{1} = ({2}) Marshal.GetDelegateForFunctionPointer (env.{1}, typeof ({2}));",
					prefix,	e.Name, delegateType);
		}

		static void GenerateJniNativeInterfaceInvoker (TextWriter o, HandleStyle style)
		{
			o.WriteLine ("\tpartial class JniEnvironmentInvoker {");
			o.WriteLine ();
			o.WriteLine ("\t\tinternal JniNativeInterfaceStruct env;");
			o.WriteLine ();
			o.WriteLine ("\t\tpublic unsafe JniEnvironmentInvoker (JniNativeInterfaceStruct* p)");
			o.WriteLine ("\t\t{");
			o.WriteLine ("\t\t\tenv = *p;");

			foreach (var e in JNIEnvEntries) {
				if (!e.Prebind)
					continue;
				var d   = e.GetDelegateTypeName (style);
				if (e.GetDelegateTypeName (style) == null)
					continue;
				o.WriteLine ("\t\t\t{0}", Initialize (e, "", d));
			}

			o.WriteLine ("\t\t}");
			o.WriteLine ();

			foreach (var e in JNIEnvEntries) {
				var d = e.GetDelegateTypeName (style);
				if (d == null)
					continue;
				o.WriteLine ();
				if (e.Prebind)
					o.WriteLine ("\t\tpublic readonly {0} {1};\n", d, e.Name);
				else {
					o.WriteLine ("\t\t{0}? _{1};", d, e.Name);
					o.WriteLine ("\t\tpublic {0} {1} {{", d, e.Name);
					o.WriteLine ("\t\t\tget {");
					o.WriteLine ("\t\t\t\tif (_{0} == null)\n\t\t\t\t\t{1}", e.Name, Initialize (e, "_", d));
					o.WriteLine ("\t\t\t\treturn _{0};\n\t\t\t}}", e.Name);
					o.WriteLine ("\t\t}");
				}
			}

			o.WriteLine ("\t}");
		}


		static HashSet<string> created_delegates = new HashSet<string> ();

		static void CreateDelegate (TextWriter o, JniFunction entry, HandleStyle style)
		{
			StringBuilder builder = new StringBuilder ();
			bool has_char_array = false;

			string name = entry.GetDelegateTypeName (style);
			if (name == null)
				return;

			builder.AppendFormat ("\tunsafe delegate {0} {1} ({2} env", entry.GetMarshalReturnType (style), name, GetJniEnvironmentPointerType (style));
			for (int i = 0; i < entry.Parameters.Length; i++) {
				if (i >= 0) {
					builder.Append (", ");
					builder.AppendFormat ("{0} {1}",
							entry.Parameters [i].Type.GetMarshalType (style, isReturn: false, isPinvoke: true),
							Escape (entry.Parameters [i].Name));
				} 
				
				var ptype   = entry.Parameters [i].Type.GetManagedType (style, isReturn: false, isPinvoke: true);
				if (ptype == "va_list")
					return;
				if (ptype == "char[]")
					has_char_array = true;
			}
			builder.Append (");");

			if (created_delegates.Contains (name))
				return;

			created_delegates.Add (name);
			if (entry.Name == "NewString" || has_char_array)
				o.WriteLine ("\t[UnmanagedFunctionPointerAttribute (CallingConvention.Cdecl, CharSet=CharSet.Unicode)]");
			o.WriteLine (builder.ToString ());
		}

		static string GetJniEnvironmentPointerType (HandleStyle style)
		{
			return "JNIEnvPtr";
		}

		static void GenerateTypes (TextWriter o, HandleStyle style)
		{
			if (style == HandleStyle.JIIntPtrPinvokeWithErrors) {
				GenerateNativeMethods (o, style);
			}
			if (style == HandleStyle.JIFunctionPtrWithErrors) {
				GenerateJniNativeMethods (o, style);
			}

			var visibilities = new Dictionary<string, string> {
				{ ArrayOperationsCategory,      "public" },
				{ ClassesCategory,              "public" },
				{ ExceptionsCategory,           "public" },
				{ InstanceFieldsCategory,       "public" },
				{ InstanceMethodsCategory,      "public" },
				{ MonitorOperationsCategory,    "public" },
				{ NIOSupportCategory,           "public" },
				{ ObjectOperationsCategory,     "public" },
				{ ReferencesCatgeory,           "public" },
				{ StaticFieldsCategory,         "public" },
				{ StaticMethodsCategory,        "public" },
				{ StringOperationsCategory,     "public" },
			};
			o.WriteLine ("\tpartial class JniEnvironment {");
			foreach (var t in JNIEnvEntries
					.Select (e => e.DeclaringType ?? "JniEnvironment")
					.Distinct ()
					.OrderBy (t => t)) {
				string visibility;
				if (!visibilities.TryGetValue (t, out visibility))
					visibility = "internal";
				GenerateJniEnv (o, t, visibility, style);
			}
			o.WriteLine ("\t}");
		}

		static void GenerateNativeMethods (TextWriter o, HandleStyle style)
		{
			o.WriteLine ("\tstatic partial class NativeMethods {");
			o.WriteLine ();
			o.WriteLine ("\t\tconst string JavaInteropLib = \"java-interop\";");
			foreach (var entry in JNIEnvEntries) {
				if (entry.Parameters == null)
					continue;
				if (entry.IsPrivate || entry.CustomWrapper)
					continue;

				o.WriteLine ();
				o.WriteLine ("\t\t[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl, CharSet=CharSet.Ansi)]");
				o.WriteLine ("\t\tinternal static extern unsafe {0} {1} (IntPtr jnienv{2}{3}{4});",
					entry.ReturnType.GetMarshalType (style, isReturn: true, isPinvoke: true),
					GetPinvokeName (entry.Name),
					entry.Throws ? ", out IntPtr thrown" : "",
					entry.Parameters.Length != 0 ? ", " : "",
					string.Join (", ", entry.Parameters.Select (p => string.Format ("{0} {1}", p.Type.GetMarshalType (style, isReturn: false, isPinvoke: true), Escape (p.Name)))));
			}
			o.WriteLine ("\t}");
			o.WriteLine ();
		}

		static void GenerateJniNativeMethods (TextWriter o, HandleStyle style)
		{
			o.WriteLine ("\tstatic partial class JniNativeMethods {");
			o.WriteLine ();
			foreach (var entry in JNIEnvEntries) {
				if (entry.Parameters == null)
					continue;
				if (entry.IsPrivate || entry.CustomWrapper)
					continue;

				var returnType = entry.ReturnType.GetMarshalType (HandleStyle.JIFunctionPtrWithErrors, isReturn: true, isPinvoke: true);

				o.WriteLine ();
				o.WriteLine ("\t\t[System.Runtime.CompilerServices.MethodImpl (System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
				o.Write ("\t\tinternal static unsafe ");
				o.Write (entry.ReturnType.GetMarshalType (HandleStyle.JIFunctionPtrWithErrors, isReturn: true, isPinvoke: true));
				o.Write ($" {entry.Name} (IntPtr env");
				foreach (var p in entry.Parameters) {
					o.Write (", ");
					o.Write (p.Type.GetMarshalType (HandleStyle.JIFunctionPtrWithErrors, isReturn: false, isPinvoke: true));
					o.Write ($" {Escape (p.Name)}");
				}
				o.WriteLine (")");
				o.WriteLine ("\t\t{");
				o.Write ("\t\t\t");
				if (returnType != "void") {
					o.Write ("return ");
				}
				o.Write ($"(*((JNIEnv**)env))->{entry.Name} (env");
				foreach (var p in entry.Parameters) {
					o.Write (", ");
					o.Write (Escape (p.Name));
				}
				o.WriteLine (");");
				o.WriteLine ("\t\t}");
			}
			o.WriteLine ("\t}");
			o.WriteLine ();
		}

		static string GetPinvokeName (string name)
		{
			var sb = new StringBuilder ("java_interop_jnienv_".Length + name.Length * 2);
			sb.Append ("java_interop_jnienv");
			var uc = false;
			foreach (var c in name) {
				if (!uc && char.IsUpper (c)) {
					sb.Append ("_");
					uc = true;
				} else {
					uc = false;
				}
				sb.Append (char.ToLower (c));
			}
			return sb.ToString ();
		}

		static void GenerateJniEnv (TextWriter o, string type, string visibility, HandleStyle style)
		{
			o.WriteLine ();

			o.WriteLine ("\t{0} static partial class {1} {{", visibility, type);
			foreach (JniFunction entry in JNIEnvEntries) {
				if ((entry.DeclaringType ?? "JniEnvironment") != type)
					continue;
				if (entry.Parameters == null)
					continue;
				if (entry.IsPrivate || entry.CustomWrapper)
					continue;

				o.WriteLine ();
				o.Write ("\t\t{2} static unsafe {0} {1} (", entry.GetManagedReturnType (style), entry.ApiName, entry.Visibility);
				switch (entry.ApiName) {
				default:
					bool is_void = entry.ReturnType.JniType == "void";
					for (int i = 0; i < entry.Parameters.Length; i++) {
						if (i > 0)
							o.Write (", ");
						o.Write ("{0} {1}", entry.Parameters [i].Type.GetManagedType (style, isReturn: false), Escape (entry.Parameters [i].Name));
					}
					o.WriteLine (")");
					o.WriteLine ("\t\t{");
					NullCheckParameters (o, entry.Parameters, style);
					PrepareParameters (o, entry.Parameters, style);
					if (style == HandleStyle.JIIntPtrPinvokeWithErrors) {
						if (entry.Throws)
							o.WriteLine ("\t\t\tIntPtr thrown;");
					} else if (style == HandleStyle.JIFunctionPtrWithErrors) {
						o.WriteLine ($"\t\t\tIntPtr __env = JniEnvironment.EnvironmentPointer;");
					} else {
						o.WriteLine ("\t\t\tvar __info = JniEnvironment.CurrentInfo;");
					}
					o.Write ("\t\t\t");
					if (!is_void)
						o.Write ("var tmp = ");
					if (style == HandleStyle.JIIntPtrPinvokeWithErrors) {
						o.Write ("NativeMethods.{0} (JniEnvironment.EnvironmentPointer{1}",
								GetPinvokeName (entry.Name),
								entry.Throws ? ", out thrown" : "");
					} else if (style == HandleStyle.JIFunctionPtrWithErrors) {
						o.Write ($"JniNativeMethods.{entry.Name} (__env");
					} else {
						o.Write ("__info.Invoker.{0} (__info.EnvironmentPointer", entry.Name);
					}
					for (int i = 0; i < entry.Parameters.Length; i++) {
						var p = entry.Parameters [i];
						o.Write (", ");
						var needOut = p.Type.GetManagedType (style, isReturn: false).StartsWith ("out ", StringComparison.Ordinal);
						if (needOut && style == HandleStyle.JIFunctionPtrWithErrors) {
							o.Write ("&");
						} else if (needOut) {
							o.Write ("out ");
						}
						o.Write (p.Type.GetManagedToMarshalExpression (style, Escape (entry.Parameters [i].Name)));
					}
					o.WriteLine (");");
					if (style == HandleStyle.JIFunctionPtrWithErrors && entry.Throws) {
						o.WriteLine ("\t\t\tIntPtr thrown = JniNativeMethods.ExceptionOccurred (__env);");
					}
					CleanupParameters (o, entry.Parameters, style);
					RaiseException (o, entry, style);
					if (is_void) {
					} else {
						foreach (var line in entry.ReturnType.GetHandleCreationLogStatements (style, entry.Name, "tmp"))
							o.WriteLine ("\t\t\t{0}", line);
						foreach (var line in entry.ReturnType.GetMarshalToManagedStatements (style, "tmp", entry))
							o.WriteLine ("\t\t\t{0}", line);
					}
					break;
				}

				o.WriteLine ("\t\t}");
			}
			o.WriteLine ("\t}");
		}

		static void NullCheckParameters (TextWriter o, ParamInfo[] ps, HandleStyle style)
		{
			bool haveChecks = false;
			for (int i = 0; i < ps.Length; i++) {
				var p = ps [i];
				if (p.CanBeNull)
					continue;
				var pn = Escape (p.Name);
				foreach (var line in p.Type.VerifyParameter (style, pn)) {
					haveChecks  = true;
					o.WriteLine ("\t\t\t{0}", line);
				}
			}
			if (haveChecks)
				o.WriteLine ();
		}

		static void PrepareParameters (TextWriter o, ParamInfo[] ps, HandleStyle style)
		{
			bool haveChecks = false;
			foreach (var e in ps) {
				foreach (var s in e.Type.GetManagedToMarshalPrepareStatements (style, Escape (e.Name))) {
					haveChecks = true;
					o.WriteLine ($"\t\t\t{s}");
				}
			}
			if (haveChecks)
				o.WriteLine ();
		}

		static void CleanupParameters (TextWriter o, ParamInfo[] ps, HandleStyle style)
		{
			foreach (var e in ps) {
				foreach (var s in e.Type.GetManagedToMarshalCleanupStatements (style, Escape (e.Name))) {
					o.WriteLine ($"\t\t\t{s}");
				}
			}
		}

		static void RaiseException (TextWriter o, JniFunction entry, HandleStyle style)
		{
			if (!entry.Throws)
				return;

			o.WriteLine ();
			o.WriteLine ("\t\t\tException? __e = JniEnvironment.GetExceptionForLastThrowable ({0});",
					(style == HandleStyle.JIIntPtrPinvokeWithErrors || style == HandleStyle.JIFunctionPtrWithErrors)
					? "thrown"
					: "");
			o.WriteLine ("\t\t\tif (__e != null)");
			o.WriteLine ("\t\t\t\tExceptionDispatchInfo.Capture (__e).Throw ();");
			o.WriteLine ();
		}

		static void WriteNativeFileHeader (TextWriter o)
		{
			o.WriteLine ("/*");
			o.WriteLine (" * Generated file; DO NOT EDIT!");
			o.WriteLine (" *");
			o.WriteLine (" * To make changes, edit Java.Interop/build-tools/jnienv-gen and rerun");
			o.WriteLine (" */");
			o.WriteLine ();
		}

		static void GenerateNativeLibSource (TextWriter source, TextWriter header, string headerName)
		{
			WriteNativeFileHeader (source);
			WriteNativeFileHeader (header);

			header.WriteLine ("#if !defined (__JAVA_INTEROP_NATIVE_H)");
			header.WriteLine ("#define __JAVA_INTEROP_NATIVE_H");
			header.WriteLine ();
			header.WriteLine ("#include <jni.h>");
			header.WriteLine ();
			header.WriteLine ("typedef jmethodID jstaticmethodID;");
			header.WriteLine ("typedef jfieldID  jstaticfieldID;");
			header.WriteLine ("typedef jobject   jglobal;");
			header.WriteLine ();
			header.WriteLine ("#if !defined(JI_NO_VISIBILITY)");
			header.WriteLine ("\t/* VS 2010 and later have stdint.h */");
			header.WriteLine ("\t#if defined(_MSC_VER)");
			header.WriteLine ();
			header.WriteLine ("\t\t#define JI_API_EXPORT __declspec(dllexport)");
			header.WriteLine ("\t\t#define JI_API_IMPORT __declspec(dllimport)");
			header.WriteLine ();
			header.WriteLine ("\t#else   /* defined(_MSC_VER */");
			header.WriteLine ();
			header.WriteLine ("\t\t#define JI_API_EXPORT __attribute__ ((visibility (\"default\")))");
			header.WriteLine ("\t\t#define JI_API_IMPORT");
			header.WriteLine ();
			header.WriteLine ("\t#endif  /* !defined(_MSC_VER) */");
			header.WriteLine ();
			header.WriteLine ("\t#if defined(JI_DLL_EXPORT)");
			header.WriteLine ("\t\t#define JI_API JI_API_EXPORT");
			header.WriteLine ("\t#elif defined(JI_DLL_IMPORT)");
			header.WriteLine ("\t\t#define JI_API JI_API_IMPORT");
			header.WriteLine ("\t#else   /* !defined(JI_DLL_IMPORT) && !defined(JI_API_IMPORT) */");
			header.WriteLine ("\t\t#define JI_API");
			header.WriteLine ("\t#endif  /* JI_DLL_EXPORT... */");
			header.WriteLine ("#else // JI_NO_VISIBILITY");
			header.WriteLine ("\t#define JI_API");
			header.WriteLine ("#endif // JI_NO_VISIBILITY");
			header.WriteLine ();

			if (headerName != "-") {
				source.WriteLine ($"#include \"{Path.GetFileName(headerName)}\"");
			}

			foreach (JniFunction entry in JNIEnvEntries) {
				if (entry.IsPrivate || entry.CustomWrapper)
					continue;

				header.WriteLine (
					"JI_API {0} {1} (JNIEnv *env{2}{3}{4});",
					entry.ReturnType.JniType,
					GetPinvokeName (entry.Name),
					entry.Throws ? ", jthrowable *_thrown" : "",
					entry.Parameters.Length != 0 ? ", " : "",
					string.Join (", ", entry.Parameters.Select (p => string.Format ("{0} {1}", p.Type.JniType, p.Name)))
				);

				source.WriteLine ();
				source.WriteLine ("JI_API {0}", entry.ReturnType.JniType);
				source.WriteLine ("{0} (JNIEnv *env{1}{2}{3})",
					GetPinvokeName (entry.Name),
					entry.Throws ? ", jthrowable *_thrown" : "",
					entry.Parameters.Length != 0 ? ", " : "",
					string.Join (", ", entry.Parameters.Select (p => string.Format ("{0} {1}", p.Type.JniType, p.Name))));
				source.WriteLine ("{");
				bool isVoid = entry.ReturnType.JniType == "void";
				if (entry.Throws)
					source.WriteLine ("\t*_thrown = 0;");
				source.Write ("\t");
				if (!isVoid)
					source.Write ("{0} _r_ = ", entry.ReturnType.JniType);
				source.WriteLine ("(*env)->{0} (env{1}{2});",
					entry.Name,
					entry.Parameters.Length != 0 ? ", " : "",
					string.Join (", ", entry.Parameters.Select (p => p.Name)));
				if (entry.Throws)
					source.WriteLine ("\t*_thrown = (*env)->ExceptionOccurred (env);");
				if (!isVoid)
					source.WriteLine ("\treturn _r_;");
				source.WriteLine ("}");
			}

			header.WriteLine ();
			header.WriteLine ("#endif // __JAVA_INTEROP_NATIVE_H");
		}
	}

	class JniFunction {

		public string DeclaringType;

		// The java name
		public string Name;

		// The name of the property/method we will generate. Defaults to the (java) name.
		private string api_name;
		public string ApiName
		{
			get { return api_name ?? Name; }
			set { api_name = value; }
		}
		
		// The C prototype that we are binding (for diagnostic purposes)
		public string Prototype;
		
		public TypeInfo ReturnType;
		public ParamInfo [] Parameters;

		// If true, then we initialize the binding on the static ctor, we dont lazy-define it
		public bool Prebind = false;

		// If there is a custom wrapper in JNIEnv (so an automatic one shouldn't be generated)
		public bool CustomWrapper = false;

		// If the JNI function can throw an exception (ExceptionOccurred needs to be invoked)
		public bool Throws;

		private string visibility;
		public string Visibility {
			get {
				if (visibility == null)
					return "public";
				return visibility;
			}
			set {
				visibility = value;
			}
		}

		public bool IsPublic { get { return Visibility == "public"; } }
		public bool IsPrivate { get { return visibility == "private"; } }
		
		public string GetManagedReturnType (HandleStyle style)
		{
			if (ReturnType == null)
				return "void";
			return ReturnType.GetManagedType (style, isReturn:true);
		}

		public string GetMarshalReturnType (HandleStyle style)
		{
			if (ReturnType == null)
				return "void";
			return ReturnType.GetMarshalType (style, isReturn:true);
		}
		
		public string GetDelegateTypeName (HandleStyle style)
		{
			StringBuilder name = new StringBuilder ();

			if (ReturnType == null || ReturnType.JniType == "void")
				name.Append ("JniAction_");
			else
				name.Append ("JniFunc_");
			name.Append ("JNIEnvPtr");
			for (int i = 0; i < Parameters.Length; i++) {				
				var pt = Parameters [i].Type.GetMarshalType (style, isReturn: false);
				if (pt == "va_list")
					return null;

				name.AppendFormat ("_").Append (pt.FixupType ());
			}
			
			string rt = GetMarshalReturnType (style);
			if (rt != "void")
				name.Append ("_").Append (rt.FixupType ());
			
			return name.ToString ();
		}
	}

	abstract class TypeInfo
	{
		static readonly Dictionary<string, TypeInfo> types = new Dictionary<string, TypeInfo> () {
			{ "jvalue*",                    new BuiltinTypeInfo ("jvalue*",                 "JniArgumentValue*") },
			{ "jbyte",                      new BuiltinTypeInfo ("jbyte",                   "sbyte") },
			{ "jchar",                      new BuiltinTypeInfo ("jchar",                   "char") },
			{ "jchar*",                     new BuiltinTypeInfo ("jchar*",                  "char*") },
			{ "jshort",                     new BuiltinTypeInfo ("jshort",                  "short") },
			{ "jsize",                      new BuiltinTypeInfo ("jsize",                   "int") },
			{ "jint",                       new BuiltinTypeInfo ("jint",                    "int") },
			{ "jlong",                      new BuiltinTypeInfo ("jlong",                   "long") },
			{ "jfloat",                     new BuiltinTypeInfo ("jfloat",                  "float") },
			{ "jdouble",                    new BuiltinTypeInfo ("jdouble",                 "double") },
			{ "jboolean",                   new BooleanTypeInfo ("jboolean") },
			{ "void*",                      new BuiltinTypeInfo ("void*",                   "IntPtr") },
			{ "const jchar*",               new StringTypeInfo ("const jchar*") },
			{ "const char*",                new StringTypeInfo ("const char*") },
			{ "const JNINativeMethod*",     new BuiltinTypeInfo ("const JNINativeMethod*",  "JniNativeMethodRegistration []") },
			{ "jobjectRefType",             new BuiltinTypeInfo ("jobjectRefType",          "JniObjectReferenceType") },
			{ "jfieldID",                   new InstanceFieldTypeInfo ("jfieldID") },
			{ "jstaticfieldID",             new StaticFieldTypeInfo ("jstaticfieldID") },
			{ "jmethodID",                  new InstanceMethodTypeInfo ("jmethodID") },
			{ "jstaticmethodID",            new StaticMethodTypeInfo ("jstaticmethodID") },
			{ "jstring",                    new LocalReferenceTypeInfo ("jstring") },
			{ "jarray",                     new LocalReferenceTypeInfo ("jarray") },
			{ "jobject",                    new LocalReferenceTypeInfo ("jobject") },
			{ "jthrowable",                 new LocalReferenceTypeInfo ("jthrowable") },
			{ "jclass",                     new LocalReferenceTypeInfo ("jclass") },
			{ "jweak",                      new WeakGlobalReferenceTypeInfo ("jweak") },
			{ "jglobal",                    new GlobalReferenceTypeInfo ("jglobal") },
			{ "JavaVM**",                   new JavaVMPointerTypeInfo ("JavaVM**") },
			{ "JniReleaseArrayElementsMode",    new JniReleaseArrayElementsModeTypeInfo () },
		};

		static readonly Dictionary<string, string> pointerMapping = new Dictionary<string, string> {
			{ "jboolean*",  "bool*" },
			{ "jbyte*",     "sbyte*" },
			{ "jchar*",     "char*" },
			{ "jdouble*",   "double*" },
			{ "jfloat*",    "float*" },
			{ "jint*",      "int*" },
			{ "jlong*",     "long*" },
			{ "jshort*",    "short*" },
		};

		public static TypeInfo Create (string type, string managedType = null)
		{
			if (managedType != null)
				return new BuiltinTypeInfo (type, managedType);
			TypeInfo t;
			if (types.TryGetValue (type, out t))
				return t;
			if (type.EndsWith ("Array", StringComparison.Ordinal))
				return new ArrayTypeInfo (type);
			string p;
			if (pointerMapping.TryGetValue (type, out p))
				return new BuiltinTypeInfo (type, p);
			if (type.EndsWith ("*", StringComparison.Ordinal))
				return new BuiltinTypeInfo (type, "IntPtr");
			return new BuiltinTypeInfo (type, type);
		}

		public static implicit operator TypeInfo (string jniType)
		{
			return Create (jniType);
		}

		public readonly string JniType;

		protected TypeInfo (string jniType)
		{
			JniType = jniType;
		}

		public  abstract    string      GetMarshalType (HandleStyle style, bool isReturn, bool isPinvoke = false);
		public  abstract    string      GetManagedType (HandleStyle style, bool isReturn, bool isPinvoke = false);

		public  virtual     string[]    GetHandleCreationLogStatements (HandleStyle style, string method, string variable)
		{
			return new string [0];
		}

		public  virtual     string      GetManagedToMarshalExpression (HandleStyle style, string variable)
		{
			return variable;
		}

		public  virtual     string[]    GetMarshalToManagedStatements (HandleStyle style, string variable, JniFunction entry)
		{
			return new[] {
				string.Format ("return {0};", variable),
			};
		}

		public virtual string[] VerifyParameter (HandleStyle style, string variable)
		{
			return new string [0];
		}

		public virtual string[] GetManagedToMarshalPrepareStatements (HandleStyle style, string variable) => Array.Empty<string> ();
		public virtual string[] GetManagedToMarshalCleanupStatements (HandleStyle style, string variable) => Array.Empty<string> ();
	}

	class BuiltinTypeInfo : TypeInfo {

		/// <summary>
		/// NOTE: .NET framework can't marshal this
		/// </summary>
		const string JniArgumentValue = "JniArgumentValue*";
		string managed;

		public BuiltinTypeInfo (string jni, string managed)
			: base (jni)
		{
			this.managed    = managed;
		}

		public override string GetMarshalType (HandleStyle style, bool isReturn, bool isPinvoke)
		{
			if (isPinvoke && managed == JniArgumentValue) {
				return "IntPtr";
			}
			return managed;
		}

		public override string GetManagedType (HandleStyle style, bool isReturn, bool isPinvoke)
		{
			if (isPinvoke && managed == JniArgumentValue) {
				return "IntPtr";
			}
			return managed;
		}

		public override string GetManagedToMarshalExpression (HandleStyle style, string variable)
		{
			var value = base.GetManagedToMarshalExpression (style, variable);
			if (managed == JniArgumentValue) {
				value = "(IntPtr) " + value;
			}
			return value;
		}

		public override string[] VerifyParameter (HandleStyle style, string variable)
		{
			if (managed != "IntPtr")
				return new string [0];
			var variableName = variable.StartsWith ("@", StringComparison.Ordinal)
				? variable.Substring (1)
				: variable;
			return new[] {
				string.Format ("if ({0} == IntPtr.Zero)", variable),
				string.Format ("\tthrow new ArgumentException (\"'{0}' must not be IntPtr.Zero.\", \"{0}\");", variableName),
			};
		}
	}

	class BooleanTypeInfo : TypeInfo {

		public BooleanTypeInfo (string jni)
			: base (jni)
		{
		}

		public override string GetMarshalType (HandleStyle style, bool isReturn, bool isPinvoke)
		{
			return "byte";
		}

		public override string GetManagedType (HandleStyle style, bool isReturn, bool isPinvoke)
		{
			return "bool";
		}

		public override string[] GetMarshalToManagedStatements (HandleStyle style, string variable, JniFunction entry)
		{
			return new string[] {
				string.Format ("return ({0} != 0) ? true : false;", variable),
			};
		}

		public override string GetManagedToMarshalExpression (HandleStyle style, string variable)
		{
			return string.Format ("({0} ? (byte) 1 : (byte) 0)", variable);
		}
	}

	class StringTypeInfo : TypeInfo {

		public StringTypeInfo (string jni)
			: base (jni)
		{
		}

		public override string GetMarshalType (HandleStyle style, bool isReturn, bool isPinvoke)
		{
			if (style == HandleStyle.JIFunctionPtrWithErrors && isPinvoke) {
				return "IntPtr";
			}
			return "string";
		}

		public override string GetManagedType (HandleStyle style, bool isReturn, bool isPinvoke)
		{
			return "string";
		}

		public override string GetManagedToMarshalExpression (HandleStyle style, string variable)
		{
			switch (style) {
			case HandleStyle.JIFunctionPtrWithErrors:
				return $"_{variable}_ptr";
			default:
				return variable;
			}
		}

		public override string[] GetMarshalToManagedStatements (HandleStyle style, string variable, JniFunction entry)
		{
			switch (style) {
			case HandleStyle.SafeHandle:
			case HandleStyle.XAIntPtr:
				return new [] {
					string.Format ("JniEnvironment.LogCreateLocalRef ({0});", variable),
					string.Format ("return {0};", variable),
				};
			case HandleStyle.JIIntPtr:
			case HandleStyle.JIIntPtrPinvokeWithErrors:
			case HandleStyle.JIFunctionPtrWithErrors:
				return new [] {
					string.Format ("JniEnvironment.LogCreateLocalRef ({0});", variable),
					string.Format ("return new JniObjectReference ({0}, JniObjectReferenceType.Local);", variable),
				};
			}
			return null;
		}

		public override string[] VerifyParameter (HandleStyle style, string variable)
		{
			var variableName = variable.StartsWith ("@", StringComparison.Ordinal)
				? variable.Substring (1)
				: variable;
			return new[] {
				string.Format ("if ({0} == null)", variable),
				string.Format ("\tthrow new ArgumentNullException (\"{0}\");", variableName),
			};
		}

		public override string[] GetManagedToMarshalPrepareStatements (HandleStyle style, string variable)
		{
			switch (style) {
			case HandleStyle.JIFunctionPtrWithErrors:
				return new[]{
					$"var _{variable}_ptr = Marshal.StringToCoTaskMemUTF8 ({variable});",
				};
			default:
				return base.GetManagedToMarshalPrepareStatements (style, variable);
			}
		}

		public override string[] GetManagedToMarshalCleanupStatements (HandleStyle style, string variable)
		{
			switch (style) {
			case HandleStyle.JIFunctionPtrWithErrors:
				return new[]{
					$"Marshal.ZeroFreeCoTaskMemUTF8 (_{variable}_ptr);",
				};
			default:
				return base.GetManagedToMarshalCleanupStatements (style, variable);
			}
		}
	}

	class JniReleaseArrayElementsModeTypeInfo : TypeInfo {

		public JniReleaseArrayElementsModeTypeInfo ()
			: base ("jint")
		{
		}

		public override string GetMarshalType (HandleStyle style, bool isReturn, bool isPinvoke)
		{
			return "int";
		}

		public override string GetManagedType (HandleStyle style, bool isReturn, bool isPinvoke)
		{
			return "JniReleaseArrayElementsMode";
		}

		public override string[] GetMarshalToManagedStatements (HandleStyle style, string variable, JniFunction entry)
		{
			return new string[] {
				string.Format ("return (JniReleaseArrayElementsMode) {0};", variable),
			};
		}

		public override string GetManagedToMarshalExpression (HandleStyle style, string variable)
		{
			return string.Format ("((int) {0})", variable);
		}
	}

	class ArrayTypeInfo : LocalReferenceTypeInfo {

		public ArrayTypeInfo (string jni)
			: base (jni)
		{
		}
	}

	class IdTypeInfo : TypeInfo {

		string  type;

		public IdTypeInfo (string jni, string type)
			: base (jni)
		{
			this.type   = type;
		}

		public override string GetMarshalType (HandleStyle style, bool isReturn, bool isPinvoke)
		{
			return "IntPtr";
		}

		public override string GetManagedType (HandleStyle style, bool isReturn, bool isPinvoke)
		{
			switch (style) {
			case HandleStyle.SafeHandle:
			case HandleStyle.JIIntPtr:
			case HandleStyle.JIIntPtrPinvokeWithErrors:
			case HandleStyle.JIFunctionPtrWithErrors:
				return type;
			case HandleStyle.XAIntPtr:
				return "IntPtr";
			}
			return "TODO_" + style;;
		}

		public override string GetManagedToMarshalExpression (HandleStyle style, string variable)
		{
			switch (style) {
			case HandleStyle.SafeHandle:
			case HandleStyle.JIIntPtr:
			case HandleStyle.JIIntPtrPinvokeWithErrors:
			case HandleStyle.JIFunctionPtrWithErrors:
				return string.Format ("{0}.ID", variable);
			}
			return variable;
		}

		public override string[] VerifyParameter (HandleStyle style, string variable)
		{
			var variableName = variable.StartsWith ("@", StringComparison.Ordinal)
				? variable.Substring (1)
				: variable;
			switch (style) {
			case HandleStyle.SafeHandle:
			case HandleStyle.JIIntPtr:
			case HandleStyle.JIIntPtrPinvokeWithErrors:
			case HandleStyle.JIFunctionPtrWithErrors:
				return new [] {
					string.Format ("if ({0} == null)", variable),
					string.Format ("\tthrow new ArgumentNullException (\"{0}\");", variableName),
					string.Format ("if (!{0}.IsValid)", variable),
					string.Format ("\tthrow new ArgumentException (\"Handle value is not valid.\", \"{0}\");", variableName),
					string.Format ("System.Diagnostics.Debug.Assert ({0}{1}.IsStatic);", IsStatic ? "" : "!", variableName),
				};
			case HandleStyle.XAIntPtr:
				return new[] {
					string.Format ("if ({0} == IntPtr.Zero)", variable),
					string.Format ("\tthrow new ArgumentException (\"Handle value cannot be null.\", \"{0}\");", variableName),
				};
			}
			return new string [0];
		}

		public override string[] GetMarshalToManagedStatements (HandleStyle style, string variable, JniFunction entry)
		{
			switch (style) {
			case HandleStyle.SafeHandle:
			case HandleStyle.JIIntPtr:
			case HandleStyle.JIIntPtrPinvokeWithErrors:
			case HandleStyle.JIFunctionPtrWithErrors:
				return new[] {
					string.Format ("if ({0} == IntPtr.Zero)", variable),
					string.Format ($"\tthrow new InvalidOperationException (\"Should not be reached; `{entry.Name}` should have thrown!\");"),
					string.Format ("return new {0} ({1}, {2}, {3}, isStatic: {4});", type, entry.Parameters [1].Name, entry.Parameters [2].Name, variable, IsStatic ? "true" : "false"),
				};
			case HandleStyle.XAIntPtr:
				return new[]{
					string.Format ("return {0};", variable),
				};
			}
			return new string [0];
		}

		protected virtual bool IsStatic {
			get {return false;}
		}
	}

	class InstanceFieldTypeInfo : IdTypeInfo {

		public InstanceFieldTypeInfo (string jni)
			: base (jni, "JniFieldInfo")
		{
		}
	}

	class InstanceMethodTypeInfo : IdTypeInfo {

		public InstanceMethodTypeInfo (string jni)
			: base (jni, "JniMethodInfo")
		{
		}
	}

	class StaticFieldTypeInfo : IdTypeInfo {

		public StaticFieldTypeInfo (string jni)
			: base (jni, "JniFieldInfo")
		{
		}

		protected override bool IsStatic {
			get {return true;}
		}
	}

	class StaticMethodTypeInfo : IdTypeInfo {

		public StaticMethodTypeInfo (string jni)
			: base (jni, "JniMethodInfo")
		{
		}

		protected override bool IsStatic {
			get {return true;}
		}
	}

	abstract class ObjectReferenceTypeInfo : TypeInfo {

		string  safeType, refType;

		public ObjectReferenceTypeInfo (string jni, string safeType, string refType)
			: base (jni)
		{
			this.safeType   = safeType;
			this.refType    = refType;
		}

		public override string GetMarshalType (HandleStyle style, bool isReturn, bool isPinvoke)
		{
			switch (style) {
			case HandleStyle.SafeHandle:
				return isReturn ? safeType : "JniReferenceSafeHandle";
			case HandleStyle.JIIntPtr:
			case HandleStyle.JIIntPtrPinvokeWithErrors:
			case HandleStyle.JIFunctionPtrWithErrors:
			case HandleStyle.XAIntPtr:
				return "jobject";
			}
			return null;
		}

		public override string GetManagedType (HandleStyle style, bool isReturn, bool isPinvoke)
		{
			switch (style) {
			case HandleStyle.SafeHandle:
			case HandleStyle.JIIntPtr:
			case HandleStyle.JIIntPtrPinvokeWithErrors:
			case HandleStyle.JIFunctionPtrWithErrors:
				return "JniObjectReference";
			case HandleStyle.XAIntPtr:
				return "IntPtr";
			}
			return "TODO";
		}

		public override string GetManagedToMarshalExpression (HandleStyle style, string variable)
		{
			switch (style) {
			case HandleStyle.SafeHandle:
				return string.Format ("{0}.SafeHandle", variable);
			case HandleStyle.JIIntPtr:
			case HandleStyle.JIIntPtrPinvokeWithErrors:
			case HandleStyle.JIFunctionPtrWithErrors:
				return string.Format ("{0}.Handle", variable);
			case HandleStyle.XAIntPtr:
				return variable;
			}
			return null;
		}

		public override string[] GetMarshalToManagedStatements (HandleStyle style, string variable, JniFunction entry)
		{
			switch (style) {
			case HandleStyle.SafeHandle:
			case HandleStyle.JIIntPtr:
			case HandleStyle.JIIntPtrPinvokeWithErrors:
			case HandleStyle.JIFunctionPtrWithErrors:
				return new [] {
					string.Format ("return new JniObjectReference ({0}, {1});", variable, refType),
				};
			case HandleStyle.XAIntPtr:
				return new[] {
					string.Format ("return {0};", variable),
				};
			}
			return new string [0];
		}

		public override string[] VerifyParameter (HandleStyle style, string variable)
		{
			var variableName = variable.StartsWith ("@", StringComparison.Ordinal)
				? variable.Substring (1)
				: variable;
			switch (style) {
			case HandleStyle.SafeHandle:
			case HandleStyle.JIIntPtr:
			case HandleStyle.JIIntPtrPinvokeWithErrors:
			case HandleStyle.JIFunctionPtrWithErrors:
				return new [] {
					string.Format ("if (!{0}.IsValid)", variable),
					string.Format ("\tthrow new ArgumentException (\"Handle must be valid.\", \"{0}\");", variableName),
				};
			case HandleStyle.XAIntPtr:
				return new [] {
					string.Format ("if ({0} == IntPtr.Zero)", variable),
					string.Format ("\tthrow new ArgumentException (\"`{0}` must not be IntPtr.Zero.\", \"{0}\");", variableName),
				};
			}
			return new string [0];
		}
	}

	class LocalReferenceTypeInfo : ObjectReferenceTypeInfo {

		public LocalReferenceTypeInfo (string jni)
			: base (jni, "JniLocalReference", "JniObjectReferenceType.Local")
		{
		}

		public override string[] GetHandleCreationLogStatements (HandleStyle style, string method, string variable)
		{
			if (method == "NewLocalRef" || method == "ExceptionOccurred")
				return base.GetHandleCreationLogStatements (style, method, variable);
			return new[] {
				string.Format ("JniEnvironment.LogCreateLocalRef ({0});", variable),
			};
		}
	}

	class WeakGlobalReferenceTypeInfo : ObjectReferenceTypeInfo {

		public WeakGlobalReferenceTypeInfo (string jni)
			: base (jni, "JniWeakGlobalReference", "JniObjectReferenceType.WeakGlobal")
		{
		}
	}

	class GlobalReferenceTypeInfo : ObjectReferenceTypeInfo {

		public GlobalReferenceTypeInfo (string jni)
			: base (jni, "JniGlobalReference", "JniObjectReferenceType.Global")
		{
		}
	}

	class JavaVMPointerTypeInfo : TypeInfo {

		public JavaVMPointerTypeInfo (string jni)
			: base (jni)
		{
		}

		public override string GetMarshalType (HandleStyle style, bool isReturn, bool isPinvoke)
		{
			if (style == HandleStyle.JIFunctionPtrWithErrors && isPinvoke) {
				return "IntPtr*";
			}
			return "out IntPtr";
		}

		public override string GetManagedType (HandleStyle style, bool isReturn, bool isPinvoke)
		{
			return "out IntPtr";
		}

		public override string GetManagedToMarshalExpression (HandleStyle style, string variable)
		{
			switch (style) {
			case HandleStyle.JIFunctionPtrWithErrors:
				return $"_{variable}_ptr";
			default:
				return variable;
			}
		}

		public override string[] GetManagedToMarshalPrepareStatements (HandleStyle style, string variable)
		{
			switch (style) {
			case HandleStyle.JIFunctionPtrWithErrors:
				return new[]{
					$"IntPtr _{variable}_ptr = IntPtr.Zero;",
				};
			default:
				return base.GetManagedToMarshalPrepareStatements (style, variable);
			}
		}

		public override string[] GetManagedToMarshalCleanupStatements (HandleStyle style, string variable)
		{
			switch (style) {
			case HandleStyle.JIFunctionPtrWithErrors:
				return new[]{
					$"{variable} = _{variable}_ptr;",
				};
			default:
				return base.GetManagedToMarshalCleanupStatements (style, variable);
			}
		}
	}

	class ParamInfo
	{
		public TypeInfo Type;
		public string Name;
		public bool IsParamArray;
		public bool CanBeNull;

		public ParamInfo (TypeInfo Type, string Name, bool IsParamArray)
		{
			this.Type = Type;
			this.Name = Name;
			this.IsParamArray = IsParamArray;
		}

		public ParamInfo (TypeInfo Type, string Name = null, Modifier m = 0)
		{
			this.Type = Type;
			this.Name = Name;
			IsParamArray  = (m & Modifier.Params) != 0;
			CanBeNull     = (m & Modifier.CanBeNull) != 0;
		}
	}

	[Flags]
	enum Modifier {
		None      = 0,
		Params    = 1,
		CanBeNull = 2,
	}

	enum HandleStyle {
		SafeHandle,
		JIIntPtr,
		JIIntPtrPinvokeWithErrors,
		XAIntPtr,
		JIFunctionPtrWithErrors,
	}
}

