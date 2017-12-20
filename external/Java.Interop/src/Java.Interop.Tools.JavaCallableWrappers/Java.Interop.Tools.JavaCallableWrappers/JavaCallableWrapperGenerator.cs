using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using Mono.Cecil;
using Mono.Cecil.Cil;

using Android.Runtime;

using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.Diagnostics;
using Java.Interop.Tools.TypeNameMappings;

using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace Java.Interop.Tools.JavaCallableWrappers {

	 public class JavaCallableWrapperGenerator {

		class JavaFieldInfo {
			public JavaFieldInfo (MethodDefinition method, string fieldName)
			{
				this.FieldName = fieldName;
				InitializerName = method.Name;
				TypeName = JavaNativeTypeManager.ReturnTypeFromSignature (GetJniSignature (method)).Type;
				IsStatic = method.IsStatic;
				Access = method.Attributes & MethodAttributes.MemberAccessMask;
				Annotations = GetAnnotationsString ("\t", method.CustomAttributes);
			}

			public MethodAttributes Access { get; private set; }
			public bool IsStatic { get; private set; }
			public string TypeName { get; private set; }
			public string FieldName { get; private set; }
			public string InitializerName { get; private set; }
			public string Annotations { get; private set; }

			public string GetJavaAccess ()
			{
				return JavaCallableWrapperGenerator.GetJavaAccess (Access);
			}
		}

		Action<string, object[]> log;
		string name;
		string package;
		TypeDefinition type;
		List<JavaFieldInfo> exported_fields = new List<JavaFieldInfo> ();
		List<Signature> methods = new List<Signature> ();
		List<Signature> ctors   = new List<Signature> ();
		List<JavaCallableWrapperGenerator> children;

		public JavaCallableWrapperGenerator (TypeDefinition type, Action<string, object[]> log)
			: this (type, null, log)
		{
			if (type.HasNestedTypes) {
				children = new List<JavaCallableWrapperGenerator> ();
				AddNestedTypes (type);
			}
		}

		public  string          ApplicationJavaClass            { get; set; }

		public bool UseSharedRuntime;

		public bool GenerateOnCreateOverrides { get; set; }

		public bool HasExport { get; private set; }

		public string Name {
			get { return name; }
		}

		void AddNestedTypes (TypeDefinition type)
		{
			foreach (TypeDefinition nt in type.NestedTypes) {
				if (!nt.IsSubclassOf ("Java.Lang.Object"))
					continue;
				if (!JavaNativeTypeManager.IsNonStaticInnerClass (nt))
					continue;
				children.Add (new JavaCallableWrapperGenerator (nt, JavaNativeTypeManager.ToJniName (type), log));
				if (nt.HasNestedTypes)
					AddNestedTypes (nt);
			}
			HasExport |= children.Any (t => t.HasExport);
		}

		JavaCallableWrapperGenerator (TypeDefinition type, string outerType, Action<string, object[]> log)
		{
			this.type = type;
			this.log = log;

			if (type.IsEnum || type.IsInterface || type.IsValueType)
				Diagnostic.Error (4200, LookupSource (type), "Can only generate Java wrappers for 'class' types, not type '{0}'.", type.FullName);

			string jniName = JavaNativeTypeManager.ToJniName (type);
			if (jniName == null)
				Diagnostic.Error (4201, LookupSource (type), "Unable to determine Java name for type {0}", type.FullName);
			if (!string.IsNullOrEmpty (outerType)) {
				string p;
				jniName = jniName.Substring (outerType.Length + 1);
				ExtractJavaNames (outerType, out p, out outerType);
			}
			ExtractJavaNames (jniName, out package, out name);
			if (string.IsNullOrEmpty (package) &&
					(type.IsSubclassOf ("Android.App.Activity") ||
					 type.IsSubclassOf ("Android.App.Application") ||
					 type.IsSubclassOf ("Android.App.Service") ||
					 type.IsSubclassOf ("Android.Content.BroadcastReceiver") ||
					 type.IsSubclassOf ("Android.Content.ContentProvider")))
				Diagnostic.Error (4203, LookupSource (type), "The Name property must be a fully qualified 'package.TypeName' value, and no package was found for '{0}'.", jniName);

			foreach (MethodDefinition minfo in type.Methods.Where (m => !m.IsConstructor)) {
				var baseMethods = GetBaseMethods (minfo);
				var baseRegiteredMethod = baseMethods.FirstOrDefault (m => GetRegisterAttributes (m).Any ());
				if (baseRegiteredMethod != null)
					AddMethod (baseRegiteredMethod, minfo);
				else if (GetExportFieldAttributes (minfo).Any ()) {
					AddMethod (null, minfo);
					HasExport = true;
				} else if (GetExportAttributes (minfo).Any ()) {
					AddMethod (null, minfo);
					HasExport = true;
				}
			}

			foreach (MethodDefinition imethod in type.Interfaces.Select (ifaceInfo => ifaceInfo.InterfaceType)
					.Select (r => {
						var d = r.Resolve ();
						if (d == null)
							Diagnostic.Error (4204,
									LookupSource (type),
									"Unable to resolve interface type '{0}'. Are you missing an assembly reference?",
									r.FullName);
						return d;
					})
					.Where (d => GetRegisterAttributes (d).Any ())
					.SelectMany (d => d.Methods)) {
				AddMethod (imethod, imethod);
			}

			var ctorTypes = new List<TypeDefinition> () {
				type,
			};
			foreach (var bt in type.GetBaseTypes ()) {
				ctorTypes.Add (bt);
				RegisterAttribute rattr = GetRegisterAttributes (bt).FirstOrDefault ();
				if (rattr != null && rattr.DoNotGenerateAcw)
					break;
			}
			ctorTypes.Reverse ();

			var curCtors = new List<MethodDefinition> ();

			foreach (MethodDefinition minfo in type.Methods.Where (m => m.IsConstructor)) {
				if (GetExportAttributes (minfo).Any ()) {
					if (minfo.IsStatic) {
						// Diagnostic.Warning (log, "ExportAttribute does not work on static constructor");
					}
					else {
						AddConstructor (minfo, ctorTypes [0], outerType, null, curCtors, false, true);
						HasExport = true;
					}
				}
			}

			AddConstructors (ctorTypes [0], outerType, null, curCtors, true);

			for (int i = 1; i < ctorTypes.Count; ++i) {
				var baseCtors = curCtors;
				curCtors      = new List<MethodDefinition> ();
				AddConstructors (ctorTypes [i], outerType, baseCtors, curCtors, false);
			}
		}

		static void ExtractJavaNames (string jniName, out string package, out string type)
		{
			int i = jniName.LastIndexOf ('/');
			if (i < 0) {
				type    = jniName;
				package = string.Empty;
			}
			else {
				type    = jniName.Substring (i+1);
				package = jniName.Substring (0, i).Replace ('/', '.');
			}
		}

		static SequencePoint LookupSource (MethodDefinition method)
		{
			if (!method.HasBody)
				return null;

			foreach (var ins in method.Body.Instructions) {
				var seqPoint = method.DebugInformation.GetSequencePoint (ins);
				if (seqPoint != null)
					return seqPoint;
			}

			return null;
		}

		static SequencePoint LookupSource (TypeDefinition type)
		{
			SequencePoint candidate = null;
			foreach (var method in type.Methods) {
				if (!method.HasBody)
					continue;

				foreach (var ins in method.Body.Instructions) {
					var seq = method.DebugInformation.GetSequencePoint (ins);
					if (seq == null)
						continue;

					if (Regex.IsMatch (seq.Document.Url, ".+\\.(g|designer)\\..+"))
						break;
					if (candidate == null || seq.StartLine < candidate.StartLine)
						candidate = seq;
					break;
				}
			}

			return candidate;
		}

		void AddConstructors (TypeDefinition type, string outerType, List<MethodDefinition> baseCtors, List<MethodDefinition> curCtors, bool onlyRegisteredOrExportedCtors)
		{
			foreach (MethodDefinition ctor in type.Methods.Where (m => m.IsConstructor && !m.IsStatic))
				if (!GetExportAttributes (ctor).Any ())
					AddConstructor (ctor, type, outerType, baseCtors, curCtors, onlyRegisteredOrExportedCtors, false);
		}

		void AddConstructor (MethodDefinition ctor, TypeDefinition type, string outerType, List<MethodDefinition> baseCtors, List<MethodDefinition> curCtors, bool onlyRegisteredOrExportedCtors, bool skipParameterCheck)
		{
				string managedParameters = GetManagedParameters (ctor, outerType);
				if (!skipParameterCheck && (managedParameters == null || ctors.Any (c => c.ManagedParameters == managedParameters))) {
					return;
				}

				ExportAttribute eattr = GetExportAttributes (ctor).FirstOrDefault ();
				if (eattr != null) {
					if (!string.IsNullOrEmpty (eattr.Name)) {
						// Diagnostic.Warning (log, "Use of ExportAttribute.Name property is invalid on constructors");
					}
					ctors.Add (new Signature (ctor, eattr));
					curCtors.Add (ctor);
					return;
				}

				RegisterAttribute rattr = GetRegisterAttributes (ctor).FirstOrDefault ();
				if (rattr != null) {
					if (ctors.Any (c => c.JniSignature == rattr.Signature))
						return;
					ctors.Add (new Signature (ctor, rattr, managedParameters, outerType));
					curCtors.Add (ctor);
					return;
				}

				if (onlyRegisteredOrExportedCtors)
					return;

				string jniSignature = GetJniSignature (ctor);

				if (jniSignature == null)
					return;

				if (ctors.Any (c => c.JniSignature == jniSignature))
					return;

				if (baseCtors.Any (m => m.Parameters.AreParametersCompatibleWith (ctor.Parameters))) {
					ctors.Add (new Signature (".ctor", jniSignature, "", managedParameters, outerType, null));
					curCtors.Add (ctor);
					return;
				}
				if (baseCtors.Any (m => !m.HasParameters)) {
					ctors.Add (new Signature (".ctor", jniSignature, "", managedParameters, outerType, ""));
					curCtors.Add (ctor);
					return;
				}
		}

		static IEnumerable<MethodDefinition> GetBaseMethods (MethodDefinition method)
		{
			MethodDefinition bmethod;
			while ((bmethod = method.GetBaseDefinition ()) != method) {
				method = bmethod;
				yield return method;
			}
		}

		internal static RegisterAttribute ToRegisterAttribute (CustomAttribute attr)
		{
			// attr.Resolve ();
			RegisterAttribute r = null;
			if (attr.ConstructorArguments.Count == 1)
				r = new RegisterAttribute ((string) attr.ConstructorArguments [0].Value);
			else if (attr.ConstructorArguments.Count == 3)
				r = new RegisterAttribute (
						(string) attr.ConstructorArguments [0].Value,
						(string) attr.ConstructorArguments [1].Value,
						(string) attr.ConstructorArguments [2].Value);
			if (r != null) {
				var v = attr.Properties.FirstOrDefault (p => p.Name == "DoNotGenerateAcw");
				r.DoNotGenerateAcw = v.Name == null ? false : (bool) v.Argument.Value;
			}
			return r;
		}

		internal static ExportAttribute ToExportAttribute (CustomAttribute attr, IMemberDefinition declaringMember)
		{
			var name = attr.ConstructorArguments.Count > 0 ? (string) attr.ConstructorArguments [0].Value : declaringMember.Name;
			if (attr.Properties.Count == 0)
				return new ExportAttribute (name);
			var typeArgs = (CustomAttributeArgument []) attr.Properties.FirstOrDefault (p => p.Name == "Throws").Argument.Value;
			var thrown = typeArgs != null && typeArgs.Any () ? (from caa in typeArgs select JavaNativeTypeManager.Parse (GetJniTypeName ((TypeReference)caa.Value)).Type).ToArray () : null;
			var superArgs = (string) attr.Properties.FirstOrDefault (p => p.Name == "SuperArgumentsString").Argument.Value;
			return new ExportAttribute (name) {ThrownNames = thrown, SuperArgumentsString = superArgs};
		}

		internal static ExportFieldAttribute ToExportFieldAttribute (CustomAttribute attr)
		{
			return new ExportFieldAttribute ((string) attr.ConstructorArguments [0].Value);
		}

		static IEnumerable<RegisterAttribute> GetRegisterAttributes (Mono.Cecil.ICustomAttributeProvider p)
		{
			return GetAttributes<RegisterAttribute> (p, a => ToRegisterAttribute (a));
		}

		static IEnumerable<ExportAttribute> GetExportAttributes (Mono.Cecil.IMemberDefinition p)
		{
			return GetAttributes<ExportAttribute> (p, a => ToExportAttribute (a, p));
		}

		static IEnumerable<ExportFieldAttribute> GetExportFieldAttributes (Mono.Cecil.ICustomAttributeProvider p)
		{
			return GetAttributes<ExportFieldAttribute> (p, a => ToExportFieldAttribute (a));
		}

		static IEnumerable<TAttribute> GetAttributes<TAttribute> (Mono.Cecil.ICustomAttributeProvider p, Func<CustomAttribute, TAttribute> selector)
		{
			return p.GetCustomAttributes (typeof (TAttribute))
				.Select (selector);
		}

		void AddMethod (MethodDefinition registeredMethod, MethodDefinition implementedMethod)
		{
			if (registeredMethod != null)
				foreach (RegisterAttribute attr in GetRegisterAttributes (registeredMethod)) {
					var msig = new Signature (implementedMethod, attr);
					if (!registeredMethod.IsConstructor && !methods.Any (m => m.Name == msig.Name && m.Params == msig.Params))
						methods.Add (msig);
				}
			foreach (ExportAttribute attr in GetExportAttributes (implementedMethod)) {
				if (type.HasGenericParameters)
					Diagnostic.Error (4206, LookupSource (implementedMethod), "[Export] cannot be used on a generic type.");

				var msig = new Signature (implementedMethod, attr);
				if (!string.IsNullOrEmpty (attr.SuperArgumentsString)) {
					// Diagnostic.Warning (log, "Use of ExportAttribute.SuperArgumentsString property is invalid on methods");
				}
				if (!implementedMethod.IsConstructor && !methods.Any (m => m.Name == msig.Name && m.Params == msig.Params))
					methods.Add (msig);
			}
			foreach (ExportFieldAttribute attr in GetExportFieldAttributes (implementedMethod)) {
				if (type.HasGenericParameters)
					Diagnostic.Error (4207, LookupSource (implementedMethod), "[ExportField] cannot be used on a generic type.");

				var msig = new Signature (implementedMethod, attr);
				if (!implementedMethod.IsConstructor && !methods.Any (m => m.Name == msig.Name && m.Params == msig.Params)) {
					methods.Add (msig);
					exported_fields.Add (new JavaFieldInfo (implementedMethod, attr.Name));
				}
			}
		}

		string GetManagedParameters (MethodDefinition ctor, string outerType)
		{
			StringBuilder sb = new StringBuilder ();
			foreach (ParameterDefinition pdef in ctor.Parameters) {
				if (sb.Length > 0)
					sb.Append (':');
				if (outerType != null && sb.Length == 0)
					sb.Append (type.DeclaringType.GetPartialAssemblyQualifiedName ());
				else
					sb.Append (pdef.ParameterType.GetPartialAssemblyQualifiedName ());
			}
			return sb.ToString ();
		}

		static string GetJniTypeName (TypeReference typeRef)
		{
			return GetJniTypeName (typeRef, ExportParameterKind.Unspecified);
		}

		static string GetJniTypeName (TypeReference typeRef, ExportParameterKind exportKind)
		{
			return JavaNativeTypeManager.GetJniTypeName (typeRef, exportKind);
		}

		static string GetJniSignature (MethodDefinition ctor)
		{
			return JavaNativeTypeManager.GetJniSignature (ctor);
		}

		// example of java target to generate for a type
		//
		// package mono.droid;
		//
		// import android.app.Activity;
		// import android.os.Bundle;
		//
		// public class MonoActivity extends android.app.Activity
		// {
		// 	  static final String __md_methods;
		// 	  static {
		// 	    __md_methods =
		// 	      "n_OnCreate:(Landroid/os/Bundle;)V:GetOnCreate_Landroid_os_Bundle_Handler\n" +
		// 	      "";
		// 	    mono.android.Runtime.register ("Mono.Droid.MonoActivity, AssemblyName", MonoActivity.class, __md_methods);
		// 	  }
		//
		//    public void onCreate(android.os.Bundle savedInstanceState)
		//    {
		//      n_onCreate (savedInstanceState);
		//    }
		//
		//    private native void n_onCreate (android.os.Bundle bundle);
		// }

		public void Generate (TextWriter writer)
		{
			if (!string.IsNullOrEmpty (package)) {
				writer.WriteLine ("package " + package + ";");
				writer.WriteLine ();
			}

			GenerateHeader (writer);

			writer.WriteLine ("/** @hide */");
			writer.WriteLine ("\tpublic static final String __md_methods;");
			if (children != null) {
				foreach (var i in Enumerable.Range (1, children.Count))
					writer.WriteLine ("\tstatic final String __md_{0}_methods;", i);
			}
			writer.WriteLine ("\tstatic {");
			GenerateRegisterType (writer, this, "__md_methods");
			if (children != null) {
				for (int i = 0; i < children.Count; ++i) {
					string methods = string.Format ("__md_{0}_methods", i + 1);
					GenerateRegisterType (writer, children [i], methods);
				}
			}
			writer.WriteLine ("\t}");

			GenerateBody (writer);

			if (children != null)
				foreach (JavaCallableWrapperGenerator child in children) {
					child.GenerateHeader (writer);
					child.GenerateBody (writer);
					child.GenerateFooter (writer);
				}

			GenerateFooter (writer);
		}

		public void Generate (string outputPath)
		{
			using (StreamWriter sw = OpenStream (outputPath)) {
				Generate (sw);
			}
		}

		static string GetAnnotationsString (string indent, IEnumerable<CustomAttribute> atts)
		{
			var sw = new StringWriter ();
			WriteAnnotations (indent, sw, atts);
			return sw.ToString ();
		}

		static void WriteAnnotations (string indent, TextWriter sw, IEnumerable<CustomAttribute> atts)
		{
			foreach (var ca in atts) {
				var catype = ca.AttributeType.Resolve ();
				var tca = catype.CustomAttributes.FirstOrDefault (a => a.AttributeType.FullName == "Android.Runtime.AnnotationAttribute");
				if (tca != null) {
					sw.Write ("{0}@{1}", indent, tca.ConstructorArguments [0].Value);
					if (ca.Properties.Count > 0) {
						sw.WriteLine ("(");
						bool wrote = false;
						foreach (var p in ca.Properties) {
							if (wrote)
								sw.WriteLine (',');
							var pd = catype.Properties.FirstOrDefault (pp => pp.Name == p.Name);
							var reg = pd != null ? pd.CustomAttributes.FirstOrDefault (pdca => pdca.AttributeType.FullName == "Android.Runtime.RegisterAttribute") : null;
							sw.Write ("{0} = {1}", reg != null ? reg.ConstructorArguments [0].Value : p.Name, ManagedValueToJavaSource (p.Argument.Value));
							wrote = true;
						}
						sw.Write (")");
					}
					sw.WriteLine ();
				}
			}
		}

		// FIXME: this is hacky. Is there any existing code for value to source conversion?
		static string ManagedValueToJavaSource (object value)
		{
			if (value is string)
				return "\"" + value.ToString ().Replace ("\"", "\"\"") + '"';
			else if (value.GetType ().FullName == "Java.Lang.Class")
				return value.ToString () + ".class";
			else if (value is bool)
				return ((bool) value) ? "true" : "false";
			else
				return value.ToString ();
		}

		void GenerateHeader (TextWriter sw)
		{
			sw.WriteLine ();

			// class annotations.
			WriteAnnotations ("", sw, type.CustomAttributes);

			sw.WriteLine ("public " + (type.IsAbstract ? "abstract " : "") + "class " + name);

			string extendsType = GetJavaTypeName (type.BaseType);
			if (extendsType == "android.app.Application" && !string.IsNullOrEmpty (ApplicationJavaClass))
				extendsType = ApplicationJavaClass;
			sw.WriteLine ("\textends " + extendsType);
			sw.WriteLine ("\timplements");
			sw.Write ("\t\tmono.android.IGCUserPeer");
			IEnumerable<TypeDefinition> ifaces = type.Interfaces.Select (ifaceInfo => ifaceInfo.InterfaceType)
				.Select (r => r.Resolve ())
				.Where (d => GetRegisterAttributes (d).Any ());
			if (ifaces.Any ()) {
				foreach (TypeDefinition iface in ifaces) {
					sw.WriteLine (",");
					sw.Write ("\t\t{0}", GetJavaTypeName (iface));
				}
			}
			sw.WriteLine ();
			sw.WriteLine ("{");
		}

		void GenerateBody (TextWriter sw)
		{
			foreach (Signature ctor in ctors) {
				if (string.IsNullOrEmpty (ctor.Params) && JavaNativeTypeManager.IsApplication (type))
					continue;
				GenerateConstructor (ctor, sw);
			}

			GenerateApplicationConstructor (sw);

			foreach (JavaFieldInfo field in exported_fields)
				GenerateExportedField (field, sw);

			foreach (Signature method in methods)
				GenerateMethod (method, sw);

			if (GenerateOnCreateOverrides && JavaNativeTypeManager.IsApplication (type) && !methods.Any (m => m.Name == "onCreate"))
				WriteApplicationOnCreate (sw, w => {
						w.WriteLine ("\t\tmono.android.Runtime.register (\"{0}\", {1}.class, __md_methods);", type.GetPartialAssemblyQualifiedName (), name);
						w.WriteLine ("\t\tsuper.onCreate ();");
				});
			if (GenerateOnCreateOverrides && JavaNativeTypeManager.IsInstrumentation (type) && !methods.Any (m => m.Name == "onCreate"))
				WriteInstrumentationOnCreate (sw, w => {
						w.WriteLine ("\t\tmono.android.Runtime.register (\"{0}\", {1}.class, __md_methods);", type.GetPartialAssemblyQualifiedName (), name);
						w.WriteLine ("\t\tsuper.onCreate (arguments);");
				});

			sw.WriteLine ();
			sw.WriteLine ("\tprivate java.util.ArrayList refList;");
			sw.WriteLine ("\tpublic void monodroidAddReference (java.lang.Object obj)");
			sw.WriteLine ("\t{");
			sw.WriteLine ("\t\tif (refList == null)");
			sw.WriteLine ("\t\t\trefList = new java.util.ArrayList ();");
			sw.WriteLine ("\t\trefList.add (obj);");
			sw.WriteLine ("\t}");
			sw.WriteLine ();
			sw.WriteLine ("\tpublic void monodroidClearReferences ()");
			sw.WriteLine ("\t{");
			sw.WriteLine ("\t\tif (refList != null)");
			sw.WriteLine ("\t\t\trefList.clear ();");
			sw.WriteLine ("\t}");
		}

		static void GenerateRegisterType (TextWriter sw, JavaCallableWrapperGenerator self, string field)
		{
			sw.WriteLine ("\t\t{0} = ", field);
			foreach (Signature method in self.methods)
				sw.WriteLine ("\t\t\t\"{0}\\n\" +", method.Method);
			sw.WriteLine ("\t\t\t\"\";");
			if (!CannotRegisterInStaticConstructor (self.type))
				sw.WriteLine ("\t\tmono.android.Runtime.register (\"{0}\", {1}.class, {2});",
						self.type.GetPartialAssemblyQualifiedName (), self.name, field);
		}

		void GenerateFooter (TextWriter sw)
		{
			sw.WriteLine ("}");
		}

		static string GetJavaAccess (MethodAttributes access)
		{
			switch (access) {
			case MethodAttributes.Public:
				return "public";
			case MethodAttributes.FamORAssem:
				return "protected";
			case MethodAttributes.Family:
				return "protected";
			default:
				return "private";
			}
		}

		static string GetJavaTypeName (TypeReference r)
		{
			TypeDefinition d = r.Resolve ();
			string jniName = JavaNativeTypeManager.ToJniName (d);
			if (jniName == null)
				Diagnostic.Error (4201, "Unable to determine JNI name for type {0}.", r.FullName);
			return jniName.Replace ('/', '.').Replace ('$', '.');
		}

		static bool CannotRegisterInStaticConstructor (TypeDefinition type)
		{
			return JavaNativeTypeManager.IsApplication (type) || JavaNativeTypeManager.IsInstrumentation (type);
		}

		class Signature {

			public Signature (MethodDefinition method, RegisterAttribute register) : this (method, register, null, null) {}

			public Signature (MethodDefinition method, RegisterAttribute register, string managedParameters, string outerType)
				: this (register.Name, register.Signature, register.Connector, managedParameters, outerType, null)
			{
				Annotations = JavaCallableWrapperGenerator.GetAnnotationsString ("\t", method.CustomAttributes);
			}

			public Signature (MethodDefinition method, ExportAttribute export)
				: this (method.Name, GetJniSignature (method), "__export__", null, null, export.SuperArgumentsString)
			{
				IsExport = true;
				IsStatic = method.IsStatic;
				JavaAccess = JavaCallableWrapperGenerator.GetJavaAccess (method.Attributes & MethodAttributes.MemberAccessMask);
				ThrownTypeNames = export.ThrownNames;
				JavaNameOverride = export.Name;
				Annotations = JavaCallableWrapperGenerator.GetAnnotationsString ("\t", method.CustomAttributes);
			}

			public Signature (MethodDefinition method, ExportFieldAttribute exportField)
				: this (method.Name, GetJniSignature (method), "__export__", null, null, null)
			{
				if (method.HasParameters)
					Diagnostic.Error (4205, JavaCallableWrapperGenerator.LookupSource (method), "[ExportField] can only be used on methods with 0 parameters.");
				if (method.ReturnType.MetadataType == MetadataType.Void)
					Diagnostic.Error (4208, JavaCallableWrapperGenerator.LookupSource (method), "[ExportField] cannot be used on a method returning void.");
				IsExport = true;
				IsStatic = method.IsStatic;
				JavaAccess = JavaCallableWrapperGenerator.GetJavaAccess (method.Attributes & MethodAttributes.MemberAccessMask);

				// annotations are processed within JavaFieldInfo, not the initializer method. So we don't generate them here.
			}

			public Signature (string name, string signature, string connector, string managedParameters, string outerType, string superCall)
			{
				ManagedParameters = managedParameters;
				JniSignature      = signature;
				Method    = "n_" + name + ":" + signature + ":" + connector;
				Name      = name;

				var jnisig = signature;
				int closer = jnisig.IndexOf (")");
				string ret = jnisig.Substring (closer + 1);
				retval = JavaNativeTypeManager.Parse (ret).Type;
				string jniparms = jnisig.Substring (1, closer - 1);
				if (string.IsNullOrEmpty (jniparms) && string.IsNullOrEmpty (superCall))
					return;
				var parms = new StringBuilder ();
				var scall = new StringBuilder ();
				var acall = new StringBuilder ();
				bool first = true;
				int i = 0;
				foreach (JniTypeName jti in JavaNativeTypeManager.FromSignature (jniparms)) {
					if (outerType != null) {
						acall.Append (outerType).Append (".this");
						outerType = null;
						continue;
					}
					string parmType = jti.Type;
					if (!first) {
						parms.Append (", ");
						scall.Append (", ");
						acall.Append (", ");
					}
					first = false;
					parms.Append (parmType).Append (" p").Append (i);
					scall.Append ("p").Append (i);
					acall.Append ("p").Append (i);
					++i;
				}
				this.parms = parms.ToString ();
				this.call  = superCall != null ? superCall : scall.ToString ();
				this.ActivateCall = acall.ToString ();
			}

			string call;
			public string SuperCall {
				get { return call; }
			}

			public string ActivateCall {get; private set;}

			public readonly string Name;
			public readonly string JavaNameOverride;
			public string JavaName {
				get { return JavaNameOverride ?? Name; }
			}

			string parms;
			public string Params {
				get { return parms; }
			}

			string retval;
			public string Retval {
				get { return retval; }
			}

			public string ThrowsDeclaration {
				get { return ThrownTypeNames?.Length > 0 ? " throws " + String.Join (", ", ThrownTypeNames) : null; }
			}

			public readonly string JavaAccess;
			public readonly string ManagedParameters;
			public readonly string JniSignature;
			public readonly string Method;
			public readonly bool IsExport;
			public readonly bool IsStatic;
			public readonly string [] ThrownTypeNames;
			public readonly string Annotations;
		}

		void GenerateConstructor (Signature ctor, TextWriter sw)
		{
			// TODO:  we only generate constructors so that Android types w/ no
			//        default constructor can be subclasses by our generated code.
			//
			//        This does NOT currently allow creating managed types from Java.
			sw.WriteLine ();
			if (ctor.Annotations != null)
				sw.WriteLine (ctor.Annotations);
			sw.WriteLine ("\tpublic {0} ({1}){2}", name, ctor.Params, ctor.ThrowsDeclaration);
			sw.WriteLine ("\t{");
			sw.WriteLine ("\t\tsuper ({0});", ctor.SuperCall);
#if MONODROID_TIMING
			sw.WriteLine ("\t\tandroid.util.Log.i(\"MonoDroid-Timing\", \"{0}..ctor({1}): time: \"+java.lang.System.currentTimeMillis());", name, ctor.Params);
#endif
			if (!CannotRegisterInStaticConstructor (type)) {
				sw.WriteLine ("\t\tif (getClass () == {0}.class)", name);
				sw.WriteLine ("\t\t\tmono.android.TypeManager.Activate (\"{0}\", \"{1}\", this, new java.lang.Object[] {{ {2} }});", type.GetPartialAssemblyQualifiedName (), ctor.ManagedParameters, ctor.ActivateCall);
			}
			sw.WriteLine ("\t}");
		}

		void GenerateApplicationConstructor (TextWriter sw)
		{
			if (!JavaNativeTypeManager.IsApplication (type)) {
				return;
			}

			sw.WriteLine ();
			sw.WriteLine ("\tpublic {0} ()", name);
			sw.WriteLine ("\t{");
			sw.WriteLine ("\t\tmono.MonoPackageManager.setContext (this);");
			sw.WriteLine ("\t}");
		}

		void GenerateExportedField (JavaFieldInfo field, TextWriter sw)
		{
			sw.WriteLine ();
			if (field.Annotations != null)
				sw.WriteLine (field.Annotations);
			sw.WriteLine ("\t{0} {1}{2} {3} = {4} ();", field.GetJavaAccess (), field.IsStatic ? "static " : null, field.TypeName, field.FieldName, field.InitializerName);
		}

		void GenerateMethod (Signature method, TextWriter sw)
		{
			sw.WriteLine ();
			if (method.Annotations != null)
				sw.WriteLine (method.Annotations);
			sw.WriteLine ("\t{0} {1}{2} {3} ({4}){5}", method.IsExport ? method.JavaAccess : "public", method.IsStatic ? "static " : null, method.Retval, method.JavaName, method.Params, method.ThrowsDeclaration);
			sw.WriteLine ("\t{");
#if MONODROID_TIMING
			sw.WriteLine ("\t\tandroid.util.Log.i(\"MonoDroid-Timing\", \"{0}.{1}: time: \"+java.lang.System.currentTimeMillis());", name, method.Name);
#endif
			sw.WriteLine ("\t\t{0}n_{1} ({2});", method.Retval == "void" ? String.Empty : "return ", method.Name, method.ActivateCall);

			sw.WriteLine ("\t}");
			sw.WriteLine ();
			sw.WriteLine ("\tprivate {0}native {1} n_{2} ({3});", method.IsStatic ? "static " : null, method.Retval, method.Name, method.Params);
		}

		void WriteApplicationOnCreate (TextWriter sw, Action<TextWriter> extra)
		{
			sw.WriteLine ();
			sw.WriteLine ("\tpublic void onCreate ()");
			sw.WriteLine ("\t{");
			extra (sw);
			sw.WriteLine ("\t}");
		}

		void WriteInstrumentationOnCreate (TextWriter sw, Action<TextWriter> extra)
		{
			sw.WriteLine ();
			sw.WriteLine ("\tpublic void onCreate (android.os.Bundle arguments)");
			sw.WriteLine ("\t{");

#if MONODROID_TIMING
			sw.WriteLine ("\t\tandroid.util.Log.i(\"MonoDroid-Timing\", \"{0}.onCreate(Bundle): time: \"+java.lang.System.currentTimeMillis());", name);
			sw.WriteLine ();
#endif

			sw.WriteLine ("\t\tandroid.content.Context context = getContext ();");
			sw.WriteLine ();

			using (var app = new StreamReader (
						Assembly.GetExecutingAssembly ().GetManifestResourceStream (
							UseSharedRuntime
							? "MonoRuntimeProvider.Shared.java"
							: "MonoRuntimeProvider.Bundled.java"))) {
				bool copy = false;
				string line;
				while ((line = app.ReadLine ()) != null) {
					if (string.CompareOrdinal ("\t\t// Mono Runtime Initialization {{{", line) == 0)
						copy = true;
					if (copy)
						sw.WriteLine (line);
					if (string.CompareOrdinal ("\t\t// }}}", line) == 0)
						copy = false;
				}
			}

			extra (sw);
			sw.WriteLine ("\t}");
		}

		StreamWriter OpenStream (string outputPath)
		{
			string path = outputPath;
			foreach (string dir in package.Split ('.'))
				path = Path.Combine (path, dir);

			if (!Directory.Exists (path))
				Directory.CreateDirectory (path);

			path = Path.Combine (path, name + ".java");
			return new StreamWriter (new FileStream (path, FileMode.Create, FileAccess.Write));
		}
	}
}


