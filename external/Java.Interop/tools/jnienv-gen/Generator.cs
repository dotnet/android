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
			return t.Replace ("*", "Ref").Replace ("[]", "Array").Replace (" ", "");
		}
	}

	partial class Generator
	{
		static string jnienv_g_cs;

		public static int Main (string [] args)
		{
			jnienv_g_cs = "JniEnvironment.g.cs";
			if (args.Length > 0)
				jnienv_g_cs = args [0];
			try {
				using (TextWriter w = new StringWriter ()) {
					w.NewLine = "\n";
					GenerateFile (w);
					string content = w.ToString ();
					if (jnienv_g_cs == "-")
						Console.WriteLine (content);
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
			o.WriteLine ("// To make changes, edit monodroid/tools/jnienv-gen-interop and rerun");
			o.WriteLine ();
			o.WriteLine ("#if !FEATURE_HANDLES_ARE_SAFE_HANDLES && !FEATURE_HANDLES_ARE_INTPTRS");
			o.WriteLine ("#define FEATURE_HANDLES_ARE_SAFE_HANDLES");
			o.WriteLine ("#endif  // !FEATURE_HANDLES_ARE_SAFE_HANDLES && !FEATURE_HANDLES_ARE_INTPTRS");
			o.WriteLine ();
			o.WriteLine ("#if FEATURE_HANDLES_ARE_SAFE_HANDLES && FEATURE_HANDLES_ARE_INTPTRS");
			o.WriteLine ("#define _NAMESPACE_PER_HANDLE");
			o.WriteLine ("#endif  // FEATURE_HANDLES_ARE_SAFE_HANDLES && FEATURE_HANDLES_ARE_INTPTRS");
			o.WriteLine ();
			o.WriteLine ("using System;");
			o.WriteLine ("using System.Linq;");
			o.WriteLine ("using System.Runtime.InteropServices;");
			o.WriteLine ("using System.Threading;");
			o.WriteLine ();
			o.WriteLine ("using Java.Interop;");
			o.WriteLine ();
			o.WriteLine ("using JNIEnvPtr          = System.IntPtr;");
			o.WriteLine ();
			o.WriteLine ("#if FEATURE_HANDLES_ARE_INTPTRS");
			o.WriteLine ("\tusing jinstanceFieldID   = System.IntPtr;");
			o.WriteLine ("\tusing jstaticFieldID     = System.IntPtr;");
			o.WriteLine ("\tusing jinstanceMethodID  = System.IntPtr;");
			o.WriteLine ("\tusing jstaticMethodID    = System.IntPtr;");
			o.WriteLine ("\tusing jobject            = System.IntPtr;");
			o.WriteLine ("#endif  // FEATURE_HANDLES_ARE_INTPTRS");
			o.WriteLine ();
			o.WriteLine ("namespace Java.Interop {");
			GenerateJniNativeInterface (o);
			o.WriteLine ("}");
			o.WriteLine ("#if FEATURE_HANDLES_ARE_SAFE_HANDLES");
			o.WriteLine ("namespace");
			o.WriteLine ("#if _NAMESPACE_PER_HANDLE");
			o.WriteLine ("\tJava.Interop.SafeHandles");
			o.WriteLine ("#else");
			o.WriteLine ("\tJava.Interop");
			o.WriteLine ("#endif");
			o.WriteLine ("{");
			o.WriteLine ();
			GenerateDelegates (o, HandleStyle.SafeHandle);
			o.WriteLine ();
			GenerateTypes (o, HandleStyle.SafeHandle);
			o.WriteLine ();
			GenerateJniNativeInterfaceInvoker (o, HandleStyle.SafeHandle);
			o.WriteLine ("}");
			o.WriteLine ("#endif  // FEATURE_HANDLES_ARE_SAFE_HANDLES");
			o.WriteLine ("#if FEATURE_HANDLES_ARE_INTPTRS");
			o.WriteLine ("namespace");
			o.WriteLine ("#if _NAMESPACE_PER_HANDLE");
			o.WriteLine ("\tJava.Interop.IntPtrs");
			o.WriteLine ("#else");
			o.WriteLine ("\tJava.Interop");
			o.WriteLine ("#endif");
			o.WriteLine ("{");
			o.WriteLine ();
			GenerateDelegates (o, HandleStyle.IntPtr);
			o.WriteLine ();
			GenerateTypes (o, HandleStyle.IntPtr);
			o.WriteLine ();
			GenerateJniNativeInterfaceInvoker (o, HandleStyle.IntPtr);
			o.WriteLine ("}");
			o.WriteLine ("#endif // FEATURE_HANDLES_ARE_INTPTRS");
			// GenerateJniNativeInterfaceInvoker2 (o);
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
					o.WriteLine ("\t\t{0} _{1};", d, e.Name);
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
							entry.Parameters [i].Type.GetMarshalType (style, isReturn: false),
							entry.Parameters [i].Name);
				} 
				
				var ptype   = entry.Parameters [i].Type.GetManagedType (style, isReturn: false);
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
			switch (style) {
			case HandleStyle.SafeHandle:
				return "JNIEnvPtr";
			case HandleStyle.IntPtr:
				return "JNIEnvPtr";
			}
			return null;
		}

		static void GenerateTypes (TextWriter o, HandleStyle style)
		{
			var visibilities = new Dictionary<string, string> {
				{ "Arrays",     "public" },
				{ "Exceptions", "public" },
				{ "References", "public" },
				{ "IO",         "public" },
				{ "Strings",    "public" },
				{ "Types",      "public" },
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
					o.Write ("\t\t\t");
					if (!is_void) 
						o.Write ("var tmp = ");
					o.Write ("JniEnvironment.Current.Invoker.{0} (JniEnvironment.Current.EnvironmentPointer", entry.Name);
					for (int i = 0; i < entry.Parameters.Length; i++) {
						var p = entry.Parameters [i];
						o.Write (", ");
						if (p.Type.GetManagedType (style, isReturn: false).StartsWith ("out ", StringComparison.Ordinal))
							o.Write ("out ");
						o.Write (p.Type.GetManagedToMarshalExpression (style, Escape (entry.Parameters [i].Name)));
					}
					o.WriteLine (");");
					RaiseException (o, entry);
					if (is_void) {
					} else {
						foreach (var line in entry.ReturnType.GetHandleCreationLogStatements (style, entry.Name, "tmp"))
							o.WriteLine ("\t\t\t{0}", line);
						foreach (var line in entry.ReturnType.GetMarshalToManagedStatements (style, "tmp"))
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

		static void RaiseException (TextWriter o, JniFunction entry)
		{
			if (!entry.Throws)
				return;

			o.WriteLine ();
			o.WriteLine ("\t\t\tException __e = JniEnvironment.Current.GetExceptionForLastThrowable ();");
			o.WriteLine ("\t\t\tif (__e != null)");
			o.WriteLine ("\t\t\t\tthrow __e;");
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
				name.Append ("_").Append (rt);
			
			return name.ToString ();
		}
	}

	abstract class TypeInfo
	{
		static readonly Dictionary<string, TypeInfo> types = new Dictionary<string, TypeInfo> () {
			{ "jvalue*",                    new BuiltinTypeInfo ("jvalue*",                 "JValue*") },
			{ "jbyte",                      new BuiltinTypeInfo ("jbyte",                   "sbyte") },
			{ "jchar",                      new BuiltinTypeInfo ("jchar",                   "char") },
			{ "jchar*",                     new BuiltinTypeInfo ("jchar*",                  "IntPtr") },
			{ "jshort",                     new BuiltinTypeInfo ("jshort",                  "short") },
			{ "jsize",                      new BuiltinTypeInfo ("jsize",                   "int") },
			{ "jint",                       new BuiltinTypeInfo ("jint",                    "int") },
			{ "jlong",                      new BuiltinTypeInfo ("jlong",                   "long") },
			{ "jfloat",                     new BuiltinTypeInfo ("jfloat",                  "float") },
			{ "jdouble",                    new BuiltinTypeInfo ("jdouble",                 "double") },
			{ "jboolean",                   new BuiltinTypeInfo ("jboolean",                "bool") },
			{ "",                           new BuiltinTypeInfo ("",                        "void") },
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

		public  abstract    string      GetMarshalType (HandleStyle style, bool isReturn);
		public  abstract    string      GetManagedType (HandleStyle style, bool isReturn);

		public  virtual     string[]    GetHandleCreationLogStatements (HandleStyle style, string method, string variable)
		{
			return new string [0];
		}

		public  virtual     string      GetManagedToMarshalExpression (HandleStyle style, string variable)
		{
			return variable;
		}

		public  virtual     string[]    GetMarshalToManagedStatements (HandleStyle style, string variable)
		{
			return new[] {
				string.Format ("return {0};", variable),
			};
		}

		public virtual string[] VerifyParameter (HandleStyle style, string variable)
		{
			return new string [0];
		}
	}

	class BuiltinTypeInfo : TypeInfo {

		string managed;

		public BuiltinTypeInfo (string jni, string managed)
			: base (jni)
		{
			this.managed    = managed;
		}

		public override string GetMarshalType (HandleStyle style, bool isReturn)
		{
			return managed;
		}

		public override string GetManagedType (HandleStyle style, bool isReturn)
		{
			return managed;
		}

		public override string[] VerifyParameter (HandleStyle style, string variable)
		{
			if (managed != "IntPtr")
				return new string [0];
			return new[] {
				string.Format ("if ({0} == IntPtr.Zero)", variable),
				string.Format ("\tthrow new ArgumentException (\"'{0}' must not be IntPtr.Zero.\", \"{0}\");", variable),
			};
		}
	}

	class StringTypeInfo : TypeInfo {

		public StringTypeInfo (string jni)
			: base (jni)
		{
		}

		public override string GetMarshalType (HandleStyle style, bool isReturn)
		{
			return "string";
		}

		public override string GetManagedType (HandleStyle style, bool isReturn)
		{
			return "string";
		}

		public override string[] GetMarshalToManagedStatements (HandleStyle style, string variable)
		{
			switch (style) {
			case HandleStyle.SafeHandle:
				return new [] {
					string.Format ("JniEnvironment.Current.LogCreateLocalRef ({0});", variable),
					string.Format ("return {0};", variable),
				};
			case HandleStyle.IntPtr:
				return new [] {
					string.Format ("JniEnvironment.Current.LogCreateLocalRef ({0});", variable),
					string.Format ("return new JniObjectReference ({0}, JniObjectReferenceType.Local);", variable),
				};
			}
			return null;
		}

		public override string[] VerifyParameter (HandleStyle style, string variable)
		{
			return new[] {
				string.Format ("if ({0} == null)", variable),
				string.Format ("\tthrow new ArgumentNullException (\"{0}\");", variable),
			};
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

		public override string GetMarshalType (HandleStyle style, bool isReturn)
		{
			return "IntPtr";
		}

		public override string GetManagedType (HandleStyle style, bool isReturn)
		{
			return type;
		}

		public override string GetManagedToMarshalExpression (HandleStyle style, string variable)
		{
			return string.Format ("{0}.ID", variable);
		}

		public override string[] VerifyParameter (HandleStyle style, string variable)
		{
			return new [] {
				string.Format ("if ({0} == null)", variable),
				string.Format ("\tthrow new ArgumentNullException (\"{0}\");", variable),
				string.Format ("if ({0}.ID == IntPtr.Zero)", variable),
				string.Format ("\tthrow new ArgumentException (\"Handle value cannot be null.\", \"{0}\");", variable),
			};
		}

		public override string[] GetMarshalToManagedStatements (HandleStyle style, string variable)
		{
			return new[] {
				string.Format ("if ({0} == IntPtr.Zero)", variable),
				string.Format ("\treturn null;"),
				string.Format ("return new {0} ({1});", type, variable),
			};
		}
	}

	class InstanceFieldTypeInfo : IdTypeInfo {

		public InstanceFieldTypeInfo (string jni)
			: base (jni, "JniInstanceFieldID")
		{
		}
	}

	class InstanceMethodTypeInfo : IdTypeInfo {

		public InstanceMethodTypeInfo (string jni)
			: base (jni, "JniInstanceMethodID")
		{
		}
	}

	class StaticFieldTypeInfo : IdTypeInfo {

		public StaticFieldTypeInfo (string jni)
			: base (jni, "JniStaticFieldID")
		{
		}
	}

	class StaticMethodTypeInfo : IdTypeInfo {

		public StaticMethodTypeInfo (string jni)
			: base (jni, "JniStaticMethodID")
		{
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

		public override string GetMarshalType (HandleStyle style, bool isReturn)
		{
			switch (style) {
			case HandleStyle.SafeHandle:
				return isReturn ? safeType : "JniReferenceSafeHandle";
			case HandleStyle.IntPtr:
				return "jobject";
			}
			return null;
		}

		public override string GetManagedType (HandleStyle style, bool isReturn)
		{
			return "JniObjectReference";
		}

		public override string GetManagedToMarshalExpression (HandleStyle style, string variable)
		{
			switch (style) {
			case HandleStyle.SafeHandle:
				return string.Format ("{0}.SafeHandle", variable);
			case HandleStyle.IntPtr:
				return string.Format ("{0}.Handle", variable);
			}
			return null;
		}

		public override string[] GetMarshalToManagedStatements (HandleStyle style, string variable)
		{
			switch (style) {
			case HandleStyle.SafeHandle:
				return new [] {
					string.Format ("return new JniObjectReference ({0}, {1});", variable, refType),
				};
			case HandleStyle.IntPtr:
				return new [] {
					string.Format ("return new JniObjectReference ({0}, {1});", variable, refType),
				};
			}
			return null;
		}

		public override string[] VerifyParameter (HandleStyle style, string variable)
		{
			switch (style) {
			case HandleStyle.SafeHandle:
				return new [] {
					string.Format ("if ({0}.SafeHandle == null)", variable),
					string.Format ("\tthrow new ArgumentNullException (\"{0}\");", variable),
					string.Format ("if ({0}.SafeHandle.IsInvalid)", variable),
					string.Format ("\tthrow new ArgumentException (\"{0}\");", variable),
				};
			case HandleStyle.IntPtr:
				return new [] {
					string.Format ("if ({0}.Handle == IntPtr.Zero)", variable),
					string.Format ("\tthrow new ArgumentException (\"`{0}` must not be IntPtr.Zero.\", \"{0}\");", variable),
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
				string.Format ("JniEnvironment.Current.LogCreateLocalRef ({0});", variable),
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

		public override string GetMarshalType (HandleStyle style, bool isReturn)
		{
			return "out IntPtr";
		}

		public override string GetManagedType (HandleStyle style, bool isReturn)
		{
			return "out IntPtr";
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
		IntPtr,
	}
}

