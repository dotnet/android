using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Mono.Options;

namespace Xamarin.Android.JniEnv
{
	partial class Generator
	{
		static string monodroid_root = Path.GetDirectoryName (
				Path.GetDirectoryName (
					Path.GetDirectoryName (System.Reflection.Assembly.GetExecutingAssembly ().Location)));
		static string jnienv_g_cs;
		static bool useJavaInterop;

		public static int Main (string [] args)
		{
			var opts = new OptionSet {
				{ "use-java-interop", v => useJavaInterop = v != null },
				{ "root=",            v => monodroid_root = v },
				{ "o=",               v => jnienv_g_cs = v },
			};
			opts.Parse (args);
			if (string.IsNullOrEmpty (jnienv_g_cs)) {
				jnienv_g_cs = Path.Combine (monodroid_root, "Mono.Android", "src", "Runtime", "JNIEnv.g.cs");
			}
			try {
				using (TextWriter w = new StringWriter ()) {
					w.NewLine = "\n";
					GenerateFile (w);
					string content = w.ToString ();
					if (jnienv_g_cs == "-")
						Console.Out.WriteLine (content);
					else if (!File.Exists (jnienv_g_cs) || !string.Equals (content, File.ReadAllText (jnienv_g_cs)))
						File.WriteAllText (jnienv_g_cs, content);
				}
				return 0;
			} catch (Exception ex) {
				Console.WriteLine (ex);
				return 1;
			}
		}

		static string Escape (string value)
		{
			switch (value) {
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
			o.WriteLine ("// To make changes, edit monodroid/tools/jnienv-gen and rerun");
			o.WriteLine ();
			o.WriteLine ("using System;");
			o.WriteLine ("using System.Runtime.ExceptionServices;");
			o.WriteLine ("using System.Runtime.InteropServices;");
			o.WriteLine ("using System.Threading;");
			if (useJavaInterop) {
				o.WriteLine ();
				o.WriteLine ("using Java.Interop;");
			}
			o.WriteLine ();
			o.WriteLine ("namespace Android.Runtime {");
			o.WriteLine ();
			GenerateJniEnv (o);
			o.WriteLine ();
			GenerateJniNativeInterface (o);
			o.WriteLine ();
			GenerateJniNativeInterfaceInvoker (o);
			o.WriteLine ("}");
		}

		static void GenerateJniNativeInterface (TextWriter o)
		{
			if (useJavaInterop)
				return;
			o.WriteLine ("\t[StructLayout (LayoutKind.Sequential)]");
			o.WriteLine ("\tpartial struct JniNativeInterfaceStruct {");
			o.WriteLine ();

			int maxName = JNIEnvEntries.Max (e => e.Name.Length);

			o.WriteLine ("#pragma warning disable 0649	// Field is assigned to, and will always have its default value `null`; ignore as it'll be set in native code.");
			o.WriteLine ("#pragma warning disable 0169	// Field never used; ignore since these fields make the structure have the right layout.");

			for (int i = 0; i < 4; i++)
				o.WriteLine ("\t\tprivate IntPtr  reserved{0};                      // void*", i);

			foreach (var e in JNIEnvEntries) {
				o.WriteLine ("\t\tpublic  IntPtr  {0};{1}  // {2}", e.Name, new string (' ', maxName - e.Name.Length), e.Prototype);
			}
			o.WriteLine ("#pragma warning restore 0169");
			o.WriteLine ("#pragma warning restore 0649");
			o.WriteLine ("\t}");
		}

		static string Initialize (JniFunction e, string prefix)
		{
			//return String.Format ("{2}{0} = GetDelegate<{1}>(JniEnv.{0});", e.Name, e.Delegate, prefix);
			return String.Format ("{2}{0} = ({1}) Marshal.GetDelegateForFunctionPointer (JniEnv.{0}, typeof ({1}));", e.Name, e.Delegate, prefix);
		}

		static void GenerateJniNativeInterfaceInvoker (TextWriter o)
		{
			if (useJavaInterop)
				return;
			o.WriteLine ("\tpartial class JniNativeInterfaceInvoker {");
			o.WriteLine ();
			o.WriteLine ("\t\tJniNativeInterfaceStruct JniEnv;");
			o.WriteLine ();
			o.WriteLine ("\t\tpublic unsafe JniNativeInterfaceInvoker (JniNativeInterfaceStruct* p)");
			o.WriteLine ("\t\t{");
			o.WriteLine ("\t\t\tJniEnv = *p;");

			foreach (var e in JNIEnvEntries) {
				if (e.Delegate == null)
					continue;
				if (!e.Prebind)
					continue;
				o.WriteLine ("\t\t\t{0}", Initialize (e, ""));
			}

			o.WriteLine ("\t\t}");
			o.WriteLine ();

			foreach (var e in JNIEnvEntries) {
				if (e.Delegate == null)
					continue;
				o.WriteLine ();
				if (e.Prebind)
					o.WriteLine ("\t\tpublic readonly {0} {1};\n", e.Delegate, e.Name);
				else {
					o.WriteLine ("\t\t{0} _{1};", e.Delegate, e.Name);
					o.WriteLine ("\t\tpublic {0} {1} {{", e.Delegate, e.Name);
					o.WriteLine ("\t\t\tget {");
					o.WriteLine ("\t\t\t\tif (_{0} == null)\n\t\t\t\t\t{1}", e.Name, Initialize (e, "_"));
					o.WriteLine ("\t\t\t\treturn _{0};\n\t\t\t}}", e.Name);
					o.WriteLine ("\t\t}");
				}
			}

			o.WriteLine ("\t}");
		}


		static HashSet<string> created_delegates = new HashSet<string> ();

		static void CreateDelegate (TextWriter o, JniFunction entry)
		{
			StringBuilder builder = new StringBuilder ();
			StringBuilder name = new StringBuilder ();
			bool has_char_array = false;

			Func<string, string> FixupType = t => t.Replace ("*", "Ref").Replace ("[]", "Array").Replace (" ", "");

			builder.AppendFormat ("\t\tinternal unsafe delegate {0} % (IntPtr env", entry.ReturnType.ManagedType, entry.Name);
			name.Append ("IntPtr");
			for (int i = 0; i < entry.Parameters.Length; i++) {
				if (i >= 0) {
					builder.Append (", ");
					builder.AppendFormat ("{0} {1}", entry.Parameters [i].Type.ManagedType, entry.Parameters [i].Name);
				}

				if (entry.Parameters [i].Type.ManagedType == "va_list")
					return;
				if (entry.Parameters [i].Type.ManagedType == "char[]")
					has_char_array = true;

				name.AppendFormat ("_").Append (FixupType (entry.Parameters [i].Type.ManagedType));
			}
			name.Append ("_").Append (FixupType (entry.ReturnType.ManagedType));
			name.Append ("_Delegate");
			builder.Append (");\n");
			builder.Replace ("%", name.ToString ());

			entry.Delegate = "JNIEnv." + name.ToString ();
			if (created_delegates.Contains (name.ToString ()))
				return;

			created_delegates.Add (name.ToString ());
			if (entry.Name == "NewString" || has_char_array)
				o.WriteLine ("\t\t[UnmanagedFunctionPointerAttribute (CallingConvention.Cdecl, CharSet=CharSet.Unicode)]");
			o.Write (builder.ToString ());
		}

		static void GenerateJniEnv (TextWriter o)
		{
			o.WriteLine ("\tpublic static partial class JNIEnv {");
			foreach (JniFunction entry in JNIEnvEntries) {
				if (entry.Parameters == null)
					continue;

				o.WriteLine ();
				if (!useJavaInterop) {
					CreateDelegate (o, entry);
				}

				if (entry.IsPrivate || entry.CustomWrapper)
					continue;

				switch (entry.ApiName) {
				case "NewArray":
					var copyArray = JNIEnvEntries.Single (e => e.Name.StartsWith ("Get") && e.Name.EndsWith ("ArrayRegion") &&
							e.Parameters [0].Type.Type == entry.ReturnType.Type);
					o.Write ("\t\t{2} static {0} {1} (", entry.ReturnType.ManagedType, entry.ApiName, entry.Visibility);
					o.WriteLine ("{0} array)", copyArray.Parameters [3].Type.ManagedType);
					o.WriteLine ("\t\t{");
					o.WriteLine ("\t\t\tif (array == null)");
					o.WriteLine ("\t\t\t\treturn IntPtr.Zero;");
					if (useJavaInterop) {
						o.WriteLine ("\t\t\tJniObjectReference result;");
						o.WriteLine ("\t\t\tresult = JniEnvironment.{0}.{1} (array.Length);", entry.DeclaringType, entry.Name);
						o.WriteLine ("\t\t\tCopyArray (array, result.Handle);");
						o.WriteLine ("\t\t\treturn result.Handle;");
						o.WriteLine ("\t\t}");
						break;
					}
					o.WriteLine ("\t\t\tIntPtr result;");
					o.WriteLine ("\t\t\tresult = Env.{0} (Handle, array.Length);", entry.Name);
					RaiseException (o, entry);
					o.WriteLine ("\t\t\tCopyArray (array, result);");
					o.WriteLine ("\t\t\treturn result;");
					o.WriteLine ("\t\t}");
					break;
				case "CopyArray":
					o.Write ("\t\t{2} static unsafe {0} {1} (", entry.ReturnType.ManagedType, entry.ApiName, entry.Visibility);
					if (entry.Name.StartsWith ("G")) {
						o.WriteLine ("IntPtr src, {0} dest)", entry.Parameters [3].Type.ManagedType);
						o.WriteLine ("\t\t{");
						o.WriteLine ("\t\t\tif (src == IntPtr.Zero)");
						o.WriteLine ("\t\t\t\treturn;");
						if (useJavaInterop) {
							var t = entry.Parameters [3].Type.ManagedType.Replace ("[]", "*");
							o.WriteLine ("\t\t\tfixed ({0} __p = dest)", t);
							o.WriteLine ("\t\t\t\tJniEnvironment.{0}.{1} (new JniObjectReference (src), 0, dest.Length, {2}__p);",
									entry.DeclaringType,
									entry.Name,
									t == "byte*" ? "(sbyte*) " : "");
							o.WriteLine ("\t\t}");
							break;
						}
						o.WriteLine ("\t\t\tEnv.{0} (Handle, src, 0, dest.Length, dest);", entry.Name);
					} else {
						o.WriteLine ("{0} src, IntPtr dest)", entry.Parameters [3].Type.ManagedType);
						o.WriteLine ("\t\t{");
						o.WriteLine ("\t\t\tif (src == null)");
						o.WriteLine ("\t\t\t\tthrow new ArgumentNullException (\"src\");");
						if (useJavaInterop) {
							var t = entry.Parameters [3].Type.ManagedType.Replace ("[]", "*");
							o.WriteLine ("\t\t\tfixed ({0} __p = src)", t);
							o.WriteLine ("\t\t\t\tJniEnvironment.{0}.{1} (new JniObjectReference (dest), 0, src.Length, {2}__p);",
									entry.DeclaringType,
									entry.Name,
									t == "byte*" ? "(sbyte*) " : "");
							o.WriteLine ("\t\t}");
							break;
						}
						o.WriteLine ("\t\t\tEnv.{0} (Handle, dest, 0, src.Length, src);", entry.Name);
					}
					RaiseException (o, entry);
					o.WriteLine ("\t\t}");
					break;
				default:
					GenerateDefaultJniFunction (o, entry);
					break;
				}
			}
			o.WriteLine ("\t}");
		}

		static void WriteJniFunctionParameters (TextWriter o, JniFunction entry, bool generateParamsOverload)
		{
			o.Write ("(");
			for (int i = 0; i < entry.Parameters.Length; i++) {
				if (i > 0)
					o.Write (", ");
				if (!entry.Parameters [i].IsParamArray) {
					o.Write ("{0} {1}", entry.Parameters [i].Type.ManagedType, Escape (entry.Parameters [i].Name));
				} else if (generateParamsOverload && i == entry.Parameters.Length -1) {
					o.Write ("{0} {1}", entry.Parameters [i].Type.ManagedType, Escape (entry.Parameters [i].Name));
				} else {
					o.Write ("params {0} {1}", entry.Parameters [i].Type.ManagedType.Replace ("*", "[]"), Escape (entry.Parameters [i].Name));
				}
			}
			o.WriteLine (")");
		}

		static void GenerateDefaultJniFunction (TextWriter o, JniFunction entry)
		{
			bool generateParamsOverload = entry.Parameters.Length > 1 &&
				entry.Parameters [entry.Parameters.Length-1].IsParamArray &&
				entry.Parameters [entry.Parameters.Length-1].Type.ManagedType == "JValue*";

			if (GenerateDefaultJavaInteropForwarder (o, entry, generateParamsOverload))
				return;

			o.Write ("\t\t{0} static {1}{2} {3} ",
					entry.Visibility, generateParamsOverload ? "unsafe " : "", entry.ReturnType.ManagedType, entry.ApiName);
			bool is_void = entry.ReturnType.ManagedType == "void";
			var lastName = entry.Parameters.Length == 0
				? ""
				: entry.Parameters[entry.Parameters.Length - 1].Name;
			WriteJniFunctionParameters (o, entry, generateParamsOverload);
			o.WriteLine ("\t\t{");

			NullCheckParameters (o, entry.Parameters);
			o.Write ("\t\t\t");
			if (!is_void)
				o.Write ("{0} tmp = ", entry.ReturnType.ManagedType);
			o.Write ("Env.{0} (Handle", entry.Name);
			for (int i = 0; i < entry.Parameters.Length; i++) {
				o.Write (", ");
				if (entry.Parameters [i].Type.ManagedType.StartsWith ("out "))
					o.Write ("out ");
				o.Write (Escape (entry.Parameters [i].Name));
			}
			o.WriteLine (");");
			RaiseException (o, entry);
			if (is_void) {
			}
			else if (entry.ReturnType.ManagedType == "IntPtr" && entry.ReturnType.Type.StartsWith ("j") && !entry.ReturnType.Type.EndsWith ("ID")) {
				o.WriteLine ("\t\t\treturn LogCreateLocalRef (tmp);");
			} else {
				o.WriteLine ("\t\t\treturn tmp;");
			}
			o.WriteLine ("\t\t}");

			if (!generateParamsOverload)
				return;
			o.WriteLine ();
			o.Write ("\t\t{0} static {1}{2} {3} ",
					entry.Visibility, "unsafe ", entry.ReturnType.ManagedType, entry.ApiName);
			WriteJniFunctionParameters (o, entry, false);
			o.WriteLine ("\t\t{");
			o.WriteLine ("\t\t\tfixed (JValue* __p = {0}) {{", Escape (entry.Parameters [entry.Parameters.Length-1].Name));
			o.WriteLine ("\t\t\t\t{0}{1} ({2}, __p);",
					is_void ? "" : "return ",
					entry.ApiName,
					string.Join (", ", entry.Parameters.Take (entry.Parameters.Length-1).Select (p => Escape (p.Name))));
			o.WriteLine ("\t\t\t}");
			o.WriteLine ("\t\t}");
		}

		static bool GenerateDefaultJavaInteropForwarder (TextWriter o, JniFunction entry, bool generateParamsOverload)
		{
			if (!useJavaInterop)
				return false;

			switch (entry.Name)	{
				// These must be hand-bound
				case "GetBooleanArrayRegion":
				case "GetJavaVM":
				case "GetStringChars":
				case "GetVersion":
				case "RegisterNatives":
				case "ReleaseStringChars":
				case "SetBooleanArrayRegion":
					return true;
			}

			o.Write ("\t\t{0} static unsafe {1} {2} ",
						entry.Visibility,
						entry.ReturnType.ManagedType,
						entry.ApiName);
			bool is_void = entry.ReturnType.ManagedType == "void";
			var lastName = entry.Parameters.Length == 0
				? ""
				: entry.Parameters[entry.Parameters.Length - 1].Name;
			WriteJniFunctionParameters (o, entry, generateParamsOverload);
			o.WriteLine ("\t\t{");
			o.Write ("\t\t\t");
			if (!is_void)
				o.Write ("return ");
			var n = entry.Name;
			if (n.StartsWith ("Call"))
				n = n.TrimEnd (new[]{'A'});
			o.Write ("JniEnvironment.{0}.{1} (", entry.DeclaringType, n);
			for (int i = 0; i < entry.Parameters.Length; i++) {
				if (i > 0)
					o.Write (", ");
				if (entry.Parameters [i].Type.ManagedType.StartsWith ("out "))
					o.Write ("out ");
				if (entry.Parameters [i].Type.ManagedType == "JValue*")
					o.Write ("(JniArgumentValue*) " + Escape (entry.Parameters [i].Name));
				else if (IsObjectReferenceType (entry.Parameters [i].Type))
					o.Write (string.Format ("new JniObjectReference ({0})", Escape (entry.Parameters [i].Name)));
				else if (IsMemberID (entry.Parameters [i].Type)) {
					string ctorFormat = null;
					switch (entry.Parameters [i].Type.Type) {
						case "jfieldID":        ctorFormat = "new JniFieldInfo ({0}, isStatic: false)";  break;
						case "jmethodID":       ctorFormat = "new JniMethodInfo ({0}, isStatic: false)"; break;
						case "jstaticfieldID":  ctorFormat = "new JniFieldInfo ({0}, isStatic: true)";    break;
						case "jstaticmethodID": ctorFormat = "new JniMethodInfo ({0}, isStatic: true)";   break;
						default:
							throw new NotSupportedException ("Don't know how to deal with: " + entry.Parameters [i].Type.Type);
					}
					o.Write (string.Format (ctorFormat, Escape (entry.Parameters [i].Name)));
				}
				else
					o.Write (Escape (entry.Parameters [i].Name));
			}
			o.Write (")");
			if (IsObjectReferenceType (entry.ReturnType))
				o.Write (".Handle");
			if (IsMemberID (entry.ReturnType))
				o.Write (".ID");
			o.WriteLine (";");
			o.WriteLine ("\t\t}");

			if (!generateParamsOverload)
				return true;
			o.WriteLine ();
			o.Write ("\t\t{0} static {1}{2} {3} ",
					entry.Visibility, "unsafe ", entry.ReturnType.ManagedType, entry.ApiName);
			WriteJniFunctionParameters (o, entry, false);
			o.WriteLine ("\t\t{");
			o.WriteLine ("\t\t\tfixed (JValue* __p = {0}) {{", Escape (entry.Parameters [entry.Parameters.Length-1].Name));
			o.WriteLine ("\t\t\t\t{0}{1} ({2}, __p);",
					is_void ? "" : "return ",
					entry.ApiName,
					string.Join (", ", entry.Parameters.Take (entry.Parameters.Length-1).Select (p => Escape (p.Name))));
			o.WriteLine ("\t\t\t}");
			o.WriteLine ("\t\t}");

			return true;
		}

		static bool IsObjectReferenceType (TypeInfo type)
		{
			switch (type.Type) {
				case "jobject":
				case "jclass":
				case "jthrowable":
				case "jstring":
				case "jarray":
				case "jweak":
					return true;
			}
			if (type.Type.StartsWith ("j") && type.Type.EndsWith("Array"))
				return true;
			return false;
		}

		static bool IsMemberID (TypeInfo type)
		{
			switch (type.Type) {
				case "jfieldID":
				case "jmethodID":
				case "jstaticfieldID":
				case "jstaticmethodID":
					return true;
			}
			return false;
		}

		static void NullCheckParameters (TextWriter o, ParamInfo[] ps)
		{
			bool haveChecks = false;
			for (int i = 0; i < ps.Length; i++) {
				var p = ps [i];
				if (p.CanBeNull)
					continue;
				if (p.Type.ManagedType == "IntPtr") {
					haveChecks = true;
					o.WriteLine ("\t\t\tif ({0} == IntPtr.Zero)", Escape (p.Name), p.Type.ManagedType);
					o.WriteLine ("\t\t\t\tthrow new ArgumentException (\"'{0}' must not be IntPtr.Zero.\", \"{0}\");", Escape (p.Name));
					continue;
				}
				if (p.Type.ManagedType == "string") {
					haveChecks = true;
					o.WriteLine ("\t\t\tif ({0} == null)", Escape (p.Name), p.Type.ManagedType);
					o.WriteLine ("\t\t\t\tthrow new ArgumentNullException (\"{0}\");", Escape (p.Name));
					continue;
				}
			}
			if (haveChecks)
				o.WriteLine ();
		}

		static void RaiseException (TextWriter o, JniFunction entry)
		{
			if (!entry.Throws)
				return;

			o.WriteLine ();
			o.WriteLine ("\t\t\tException __e = AndroidEnvironment.GetExceptionForLastThrowable ();");
			o.WriteLine ("\t\t\tif (__e != null)");
			o.WriteLine ("\t\t\t\tExceptionDispatchInfo.Capture (__e).Throw ();");
			o.WriteLine ();
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
		public bool Prebind;

		// If there is a custom wrapper in JNIEnv (so an automatic one shouldn't be generated)
		public bool CustomWrapper;

		// If the JNI function can throw an exception (ExceptionOccurred needs to be invoked)
		public bool Throws;

		// The signature of the C# delegate that we will use for the generated property
		private string @delegate;
		public string Delegate
		{
			get
			{
				StringBuilder d;

				if (@delegate != null)
					return @delegate;

				if (Parameters == null)
					return null;

				if (IsPrivate)
					return null;

				d = new StringBuilder ();
				bool is_void = ReturnType.Type == "void";

				if (is_void) {
					d.Append ("Action<IntPtr");
				} else {
					d.Append ("Func<IntPtr");
				}
				for (int i = 0; i < Parameters.Length; i++) {
					d.Append (",");
					//d.Append (Parameters [i].Type.GetManagedType (Parameters [i].Type.Type));
					d.Append (Parameters [i].Type.ManagedType);
				}
				if (!is_void) {
					d.Append (",");
					if (ReturnType.ManagedType == "void") {
						d.Append (ReturnType.GetManagedType (ReturnType.Type));
					} else {
						d.Append (ReturnType.ManagedType);
					}
				}
				d.Append (">");

				return d.ToString ();
			}
			set
			{
				@delegate = value;
			}
		}

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
	}

	class TypeInfo
	{
		public string Type;

		private string managed_type;
		public string ManagedType
		{
			get { return managed_type ?? GetManagedType (Type); }
			set { managed_type = value; }
		}

		public string GetManagedType (string native_type)
		{
			switch (native_type) {
			case "jvalue*":
				return "JValue*";
			case "jbyte": return "sbyte";
			case "jchar": return "char";
			case "jshort": return "short";
			case "jsize":
			case "jint": return "int";
			case "jlong": return "long";
			case "jfloat": return "float";
			case "jdouble": return "double";
			case "jboolean": return "bool";
			case "": return "void";
			case "jobject":
			case "jclass":
			case "void*":
			case "jfieldID":
			case "jmethodID":
			case "jstaticfieldID":
			case "jstaticmethodID":
			case "jmethod":
			case "jthrowable":
			case "jstring":
			case "jchar*":
			case "jarray":
			case "jweak":
				return "IntPtr";
			case "const jchar*":
			case "const char*":
				return "string";
			case "JavaVM**":
				return "out IntPtr";
			case "const JNINativeMethod*":
				return "JNINativeMethod []";
			case "jobjectRefType":
				return "int";
			default:
				if (native_type.EndsWith ("Array"))
					return "IntPtr";
				if (native_type.EndsWith ("*"))
					return "IntPtr";
				return native_type;
			}
		}

		public TypeInfo ()
		{
		}

		public TypeInfo (string Type, string ManagedType = null)
		{
			this.Type = Type;
			this.ManagedType = ManagedType;
		}

		public static implicit operator TypeInfo (string type)
		{
			return new TypeInfo (type);
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
}

