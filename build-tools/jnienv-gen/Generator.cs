using System;
using System.IO;
using System.Linq;

using Mono.Options;

namespace Xamarin.Android.JniEnv
{
	partial class Generator
	{
		static string monodroid_root = Path.GetDirectoryName (
				Path.GetDirectoryName (
					Path.GetDirectoryName (System.Reflection.Assembly.GetExecutingAssembly ().Location)));
		static string jnienv_g_cs;

		public static int Main (string [] args)
		{
			var opts = new OptionSet {
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
			o.WriteLine ();
			o.WriteLine ("using Java.Interop;");
			o.WriteLine ();
			o.WriteLine ("namespace Android.Runtime {");
			o.WriteLine ();
			GenerateJniEnv (o);
			o.WriteLine ();
			o.WriteLine ();
			o.WriteLine ("}");
		}

		static void GenerateJniEnv (TextWriter o)
		{
			o.WriteLine ("\tpublic static partial class JNIEnv {");
			foreach (JniFunction entry in JNIEnvEntries) {
				if (entry.Parameters == null)
					continue;

				o.WriteLine ();
				if (entry.IsPrivate || entry.CustomWrapper)
					continue;

				switch (entry.ApiName) {
				case "NewArray":
					var copyArray = JNIEnvEntries.Single (e => e.Name.StartsWith ("Get", StringComparison.Ordinal) && e.Name.EndsWith ("ArrayRegion", StringComparison.Ordinal) &&
							e.Parameters [0].Type.Type == entry.ReturnType.Type);
					o.Write ("\t\t{2} static {0} {1} (", entry.ReturnType.ManagedType, entry.ApiName, entry.Visibility);
					o.WriteLine ("{0} array)", copyArray.Parameters [3].Type.ManagedType);
					o.WriteLine ("\t\t{");
					o.WriteLine ("\t\t\tif (array == null)");
					o.WriteLine ("\t\t\t\treturn IntPtr.Zero;");
					o.WriteLine ("\t\t\tJniObjectReference result;");
					o.WriteLine ("\t\t\tresult = JniEnvironment.{0}.{1} (array.Length);", entry.DeclaringType, entry.Name);
					o.WriteLine ("\t\t\tCopyArray (array, result.Handle);");
					o.WriteLine ("\t\t\treturn result.Handle;");
					o.WriteLine ("\t\t}");
					break;
				case "CopyArray":
					o.Write ("\t\t{2} static unsafe {0} {1} (", entry.ReturnType.ManagedType, entry.ApiName, entry.Visibility);
					if (entry.Name.StartsWith ("G", StringComparison.Ordinal)) {
						o.WriteLine ("IntPtr src, {0} dest)", entry.Parameters [3].Type.ManagedType);
						o.WriteLine ("\t\t{");
						o.WriteLine ("\t\t\tif (src == IntPtr.Zero)");
						o.WriteLine ("\t\t\t\treturn;");
						var t = entry.Parameters [3].Type.ManagedType.Replace ("[]", "*");
						o.WriteLine ("\t\t\tfixed ({0} __p = dest)", t);
						o.WriteLine ("\t\t\t\tJniEnvironment.{0}.{1} (new JniObjectReference (src), 0, dest.Length, {2}__p);",
								entry.DeclaringType,
								entry.Name,
								t == "byte*" ? "(sbyte*) " : "");
					} else {
						o.WriteLine ("{0} src, IntPtr dest)", entry.Parameters [3].Type.ManagedType);
						o.WriteLine ("\t\t{");
						o.WriteLine ("\t\t\tif (src == null)");
						o.WriteLine ("\t\t\t\tthrow new ArgumentNullException (\"src\");");
						var t = entry.Parameters [3].Type.ManagedType.Replace ("[]", "*");
						o.WriteLine ("\t\t\tfixed ({0} __p = src)", t);
						o.WriteLine ("\t\t\t\tJniEnvironment.{0}.{1} (new JniObjectReference (dest), 0, src.Length, {2}__p);",
								entry.DeclaringType,
								entry.Name,
								t == "byte*" ? "(sbyte*) " : "");
					}
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

			switch (entry.Name)	{
				// These must be hand-bound
				case "GetBooleanArrayRegion":
				case "GetJavaVM":
				case "GetStringChars":
				case "GetVersion":
				case "RegisterNatives":
				case "ReleaseStringChars":
				case "SetBooleanArrayRegion":
					return;
			}

			o.Write ("\t\t{0} static unsafe {1} {2} ",
						entry.Visibility,
						entry.ReturnType.ManagedType,
						entry.ApiName);
			bool is_void = entry.ReturnType.ManagedType == "void";
			WriteJniFunctionParameters (o, entry, generateParamsOverload);
			o.WriteLine ("\t\t{");
			o.Write ("\t\t\t");
			if (!is_void)
				o.Write ("return ");
			var n = entry.Name;
			if (n.StartsWith ("Call", StringComparison.Ordinal))
				n = n.TrimEnd (new[]{'A'});
			o.Write ("JniEnvironment.{0}.{1} (", entry.DeclaringType, n);
			for (int i = 0; i < entry.Parameters.Length; i++) {
				if (i > 0)
					o.Write (", ");
				if (entry.Parameters [i].Type.ManagedType.StartsWith ("out ", StringComparison.Ordinal))
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
			if (type.Type.StartsWith ("j", StringComparison.Ordinal) && type.Type.EndsWith("Array", StringComparison.Ordinal))
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

		public TypeInfo ReturnType;
		public ParamInfo [] Parameters;

		// If there is a custom wrapper in JNIEnv (so an automatic one shouldn't be generated)
		public bool CustomWrapper;

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
				if (native_type.EndsWith ("Array", StringComparison.Ordinal))
					return "IntPtr";
				if (native_type.EndsWith ("*", StringComparison.Ordinal))
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

		public ParamInfo (TypeInfo Type, string Name, bool IsParamArray)
		{
			this.Type = Type;
			this.Name = Name;
			this.IsParamArray = IsParamArray;
		}

		public ParamInfo (TypeInfo Type, string Name = null)
		{
			this.Type = Type;
			this.Name = Name;
		}
	}
}
