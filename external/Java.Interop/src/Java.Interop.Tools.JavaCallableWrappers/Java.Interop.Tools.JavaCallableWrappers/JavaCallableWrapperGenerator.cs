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
using static Java.Interop.Tools.TypeNameMappings.JavaNativeTypeManager;

namespace Java.Interop.Tools.JavaCallableWrappers {

	public enum JavaPeerStyle {
		XAJavaInterop1,
		JavaInterop1,
	}

	public abstract class JavaCallableMethodClassifier
	{
		public abstract bool ShouldBeDynamicallyRegistered (TypeDefinition topType, MethodDefinition registeredMethod, MethodDefinition implementedMethod, CustomAttribute? registerAttribute);
	}

	 public class JavaCallableWrapperGenerator {

		class JavaFieldInfo {
			public JavaFieldInfo (MethodDefinition method, string fieldName, IMetadataResolver resolver)
			{
				this.FieldName = fieldName;
				InitializerName = method.Name;
				TypeName = JavaNativeTypeManager.ReturnTypeFromSignature (GetJniSignature (method, resolver))?.Type
					?? throw new ArgumentException ($"Could not get JNI signature for method `{method.Name}`", nameof (method));
				IsStatic = method.IsStatic;
				Access = method.Attributes & MethodAttributes.MemberAccessMask;
				Annotations = GetAnnotationsString ("\t", method.CustomAttributes, resolver);
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
		List<JavaCallableWrapperGenerator>? children;

		readonly IMetadataResolver cache;
		readonly JavaCallableMethodClassifier? methodClassifier;

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.", error: true)]
		public JavaCallableWrapperGenerator (TypeDefinition type, Action<string, object []> log) => throw new NotSupportedException ();

		public JavaCallableWrapperGenerator (TypeDefinition type, Action<string, object[]> log, TypeDefinitionCache cache)
			: this (type, log, (IMetadataResolver) cache, methodClassifier: null)
		{ }

		public JavaCallableWrapperGenerator (TypeDefinition type, Action<string, object[]> log, TypeDefinitionCache cache, JavaCallableMethodClassifier? methodClassifier)
			: this (type, log, (IMetadataResolver) cache, methodClassifier)
		{
		}

		public JavaCallableWrapperGenerator (TypeDefinition type, Action<string, object[]> log, IMetadataResolver resolver)
			: this (type, log, resolver, methodClassifier: null)
		{ }

		public JavaCallableWrapperGenerator (TypeDefinition type, Action<string, object[]> log, IMetadataResolver resolver, JavaCallableMethodClassifier? methodClassifier)
			: this (type, null, log, resolver, methodClassifier)
		{
			AddNestedTypes (type);
		}

		public  string?         ApplicationJavaClass            { get; set; }
		public  JavaPeerStyle   CodeGenerationTarget            { get; set; }

		public bool GenerateOnCreateOverrides { get; set; }

		public bool HasExport { get; private set; }

		// If there are no methods, we need to generate "empty" registration because of backward compatibility
		public bool HasDynamicallyRegisteredMethods => methods.Count == 0 || methods.Any ((Signature sig) => sig.IsDynamicallyRegistered);

		/// <summary>
		/// The Java source code to be included in Instrumentation.onCreate
		///
		/// Originally came from MonoRuntimeProvider.java delimited by:
		/// // Mono Runtime Initialization {{{
		/// // }}}
		/// </summary>
		public string? MonoRuntimeInitialization { get; set; }

		public string Name {
			get { return name; }
		}

		void AddNestedTypes (TypeDefinition type)
		{
			if (!type.HasNestedTypes) {
				return;
			}
			children = children ?? new List<JavaCallableWrapperGenerator> ();
			foreach (TypeDefinition nt in type.NestedTypes) {
				if (!nt.HasJavaPeer (cache))
					continue;
				if (!JavaNativeTypeManager.IsNonStaticInnerClass (nt, cache))
					continue;
				children.Add (new JavaCallableWrapperGenerator (nt, JavaNativeTypeManager.ToJniName (type, cache), log, cache));
				AddNestedTypes (nt);
			}
			HasExport |= children.Any (t => t.HasExport);
		}

		JavaCallableWrapperGenerator (TypeDefinition type, string? outerType, Action<string, object[]> log, IMetadataResolver resolver, JavaCallableMethodClassifier? methodClassifier = null)
		{
			this.methodClassifier = methodClassifier;
			this.type = type;
			this.log = log;
			this.cache = resolver ?? new TypeDefinitionCache ();

			if (type.IsEnum || type.IsInterface || type.IsValueType)
				Diagnostic.Error (4200, LookupSource (type), Localization.Resources.JavaCallableWrappers_XA4200, type.FullName);

			string jniName = JavaNativeTypeManager.ToJniName (type, this.cache);
			if (jniName == null) {
				Diagnostic.Error (4201, LookupSource (type), Localization.Resources.JavaCallableWrappers_XA4201, type.FullName);
				throw new InvalidOperationException ("--nrt:jniName-- Should not be reached");
			}
			if (outerType != null && !string.IsNullOrEmpty (outerType)) {
				string p;
				jniName = jniName.Substring (outerType.Length + 1);
				ExtractJavaNames (outerType, out p, out outerType);
			}
			ExtractJavaNames (jniName, out package, out name);
			if (string.IsNullOrEmpty (package) &&
					(type.IsSubclassOf ("Android.App.Activity", cache) ||
					 type.IsSubclassOf ("Android.App.Application", cache) ||
					 type.IsSubclassOf ("Android.App.Service", cache) ||
					 type.IsSubclassOf ("Android.Content.BroadcastReceiver", cache) ||
					 type.IsSubclassOf ("Android.Content.ContentProvider", cache)))
				Diagnostic.Error (4203, LookupSource (type), Localization.Resources.JavaCallableWrappers_XA4203, jniName);

			foreach (MethodDefinition minfo in type.Methods.Where (m => !m.IsConstructor)) {
				var baseRegisteredMethod = GetBaseRegisteredMethod (minfo);
				if (baseRegisteredMethod != null)
					AddMethod (baseRegisteredMethod, minfo);
				else if (minfo.AnyCustomAttributes ("Java.Interop.JavaCallableAttribute")) {
					AddMethod (null, minfo);
					HasExport = true;
				} else if (minfo.AnyCustomAttributes ("Java.Interop.JavaCallableConstructorAttribute")) {
					AddMethod (null, minfo);
					HasExport = true;
				} else if (minfo.AnyCustomAttributes (typeof(ExportFieldAttribute))) {
					AddMethod (null, minfo);
					HasExport = true;
				} else if (minfo.AnyCustomAttributes (typeof (ExportAttribute))) {
					AddMethod (null, minfo);
					HasExport = true;
				}
			}

			foreach (InterfaceImplementation ifaceInfo in type.Interfaces) {
				var typeReference = ifaceInfo.InterfaceType;
				var typeDefinition = cache.Resolve (typeReference);
				if (typeDefinition == null) {
					Diagnostic.Error (4204,
						LookupSource (type),
						Localization.Resources.JavaCallableWrappers_XA4204,
						typeReference.FullName);
					continue;
				}
				if (!GetTypeRegistrationAttributes (typeDefinition).Any ())
					continue;
				foreach (MethodDefinition imethod in typeDefinition.Methods) {
					if (imethod.IsStatic)
						continue;
					AddMethod (imethod, imethod);
				}
			}

			var ctorTypes = new List<TypeDefinition> () {
				type,
			};
			foreach (var bt in type.GetBaseTypes (cache)) {
				ctorTypes.Add (bt);
				RegisterAttribute rattr = GetMethodRegistrationAttributes (bt).FirstOrDefault ();
				if (rattr != null && rattr.DoNotGenerateAcw)
					break;
			}
			ctorTypes.Reverse ();

			var curCtors = new List<MethodDefinition> ();

			foreach (MethodDefinition minfo in type.Methods) {
				if (minfo.IsConstructor && minfo.AnyCustomAttributes (typeof (ExportAttribute))) {
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

		static SequencePoint? LookupSource (MethodDefinition method)
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

		static SequencePoint? LookupSource (TypeDefinition type)
		{
			SequencePoint? candidate = null;
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

		void AddConstructors (TypeDefinition type, string? outerType, List<MethodDefinition>? baseCtors, List<MethodDefinition> curCtors, bool onlyRegisteredOrExportedCtors)
		{
			foreach (MethodDefinition ctor in type.Methods)
				if (ctor.IsConstructor && !ctor.IsStatic && !ctor.AnyCustomAttributes (typeof (ExportAttribute)))
					AddConstructor (ctor, type, outerType, baseCtors, curCtors, onlyRegisteredOrExportedCtors, false);
		}

		void AddConstructor (MethodDefinition ctor, TypeDefinition type, string? outerType, List<MethodDefinition>? baseCtors, List<MethodDefinition> curCtors, bool onlyRegisteredOrExportedCtors, bool skipParameterCheck)
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
					ctors.Add (new Signature (ctor, eattr, managedParameters, cache));
					curCtors.Add (ctor);
					return;
				}

				RegisterAttribute rattr = GetMethodRegistrationAttributes (ctor).FirstOrDefault ();
				if (rattr != null) {
					if (ctors.Any (c => c.JniSignature == rattr.Signature))
						return;
					ctors.Add (new Signature (ctor, rattr, managedParameters, outerType, cache));
					curCtors.Add (ctor);
					return;
				}

				if (onlyRegisteredOrExportedCtors)
					return;

				string? jniSignature = GetJniSignature (ctor, cache);

				if (jniSignature == null)
					return;

				if (ctors.Any (c => c.JniSignature == jniSignature))
					return;

				if (baseCtors == null) {
					throw new InvalidOperationException ("`baseCtors` should not be null!");
				}

				if (baseCtors.Any (m => m.Parameters.AreParametersCompatibleWith (ctor.Parameters, cache))) {
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

		MethodDefinition? GetBaseRegisteredMethod (MethodDefinition method)
		{
			MethodDefinition bmethod;
			while ((bmethod = method.GetBaseDefinition (cache)) != method) {
				method = bmethod;

				if (method.AnyCustomAttributes (typeof (RegisterAttribute))) {
					return method;
				}
			}
			return null;
		}

		internal static RegisterAttribute? ToRegisterAttribute (CustomAttribute attr)
		{
			// attr.Resolve ();
			RegisterAttribute? r = null;
			if (attr.ConstructorArguments.Count == 1)
				r = new RegisterAttribute ((string) attr.ConstructorArguments [0].Value, attr);
			else if (attr.ConstructorArguments.Count == 3)
				r = new RegisterAttribute (
						(string) attr.ConstructorArguments [0].Value,
						(string) attr.ConstructorArguments [1].Value,
						(string) attr.ConstructorArguments [2].Value,
						attr);
			if (r != null) {
				var v = attr.Properties.FirstOrDefault (p => p.Name == "DoNotGenerateAcw");
				r.DoNotGenerateAcw = v.Name == null ? false : (bool) v.Argument.Value;
			}
			return r;
		}

		internal static RegisterAttribute? RegisterFromJniTypeSignatureAttribute (CustomAttribute attr)
		{
			// attr.Resolve ();
			RegisterAttribute? r = null;
			if (attr.ConstructorArguments.Count == 1)
				r = new RegisterAttribute ((string) attr.ConstructorArguments [0].Value, attr);
			if (r != null) {
				var v = attr.Properties.FirstOrDefault (p => p.Name == "GenerateJavaPeer");
				if (v.Name == null) {
					r.DoNotGenerateAcw = false;
				} else if (v.Name == "GenerateJavaPeer") {
					r.DoNotGenerateAcw = ! (bool) v.Argument.Value;
				}
				var isKeyProp   = attr.Properties.FirstOrDefault (p => p.Name == "IsKeyword");
				var isKeyword	= isKeyProp.Name != null && ((bool) isKeyProp.Argument.Value) == true;
				var arrRankProp = attr.Properties.FirstOrDefault (p => p.Name == "ArrayRank");
				if (arrRankProp.Name != null && arrRankProp.Argument.Value is int rank) {
					r.Name = new string ('[', rank) + (isKeyword ? r.Name : "L" + r.Name + ";");
				}
			}
			return r;
		}

		internal static RegisterAttribute? RegisterFromJniConstructorSignatureAttribute (CustomAttribute attr)
		{
			// attr.Resolve ();
			RegisterAttribute? r = null;
			if (attr.ConstructorArguments.Count == 1)
				r = new RegisterAttribute (
					name:             ".ctor",
					signature:        (string) attr.ConstructorArguments [0].Value,
					connector:        "",
					originAttribute:  attr);
			return r;
		}

		internal static RegisterAttribute? RegisterFromJniMethodSignatureAttribute (CustomAttribute attr)
		{
			// attr.Resolve ();
			RegisterAttribute? r = null;
			if (attr.ConstructorArguments.Count == 2)
				r = new RegisterAttribute ((string) attr.ConstructorArguments [0].Value,
					(string) attr.ConstructorArguments [1].Value,
				        "",
				        attr);
			return r;
		}

		ExportAttribute ToExportAttribute (CustomAttribute attr, IMemberDefinition declaringMember)
		{
			var name = attr.ConstructorArguments.Count > 0 ? (string) attr.ConstructorArguments [0].Value : declaringMember.Name;
			if (attr.Properties.Count == 0)
				return new ExportAttribute (name);
			var typeArgs = (CustomAttributeArgument []) attr.Properties.FirstOrDefault (p => p.Name == "Throws").Argument.Value;
			var thrown = typeArgs != null && typeArgs.Any ()
				? (from caa in typeArgs select JavaNativeTypeManager.Parse (GetJniTypeName ((TypeReference)caa.Value, cache))?.Type)
					.Where (v => v != null)
					.ToArray ()
				: null;
			var superArgs = (string) attr.Properties.FirstOrDefault (p => p.Name == "SuperArgumentsString").Argument.Value;
			return new ExportAttribute (name) {ThrownNames = thrown, SuperArgumentsString = superArgs};
		}

		ExportAttribute ToExportAttributeFromJavaCallableAttribute (CustomAttribute attr, IMemberDefinition declaringMember)
		{
			var name = attr.ConstructorArguments.Count > 0
				? (string) attr.ConstructorArguments [0].Value
				: declaringMember.Name;
			return new ExportAttribute (name);
		}

		ExportAttribute ToExportAttributeFromJavaCallableConstructorAttribute (CustomAttribute attr, IMemberDefinition declaringMember)
		{
			var superArgs = (string) attr.Properties
				.FirstOrDefault (p => p.Name == "SuperConstructorExpression")
				.Argument
				.Value;
			return new ExportAttribute (".ctor") {
				SuperArgumentsString = superArgs,
			};
		}

		internal static ExportFieldAttribute ToExportFieldAttribute (CustomAttribute attr)
		{
			return new ExportFieldAttribute ((string) attr.ConstructorArguments [0].Value);
		}

		internal static IEnumerable<RegisterAttribute> GetTypeRegistrationAttributes (Mono.Cecil.ICustomAttributeProvider p)
		{
			foreach (var a in GetAttributes<RegisterAttribute> (p, a => ToRegisterAttribute (a))) {
				yield return a;
			}
			foreach (var c in p.GetCustomAttributes ("Java.Interop.JniTypeSignatureAttribute")) {
				var r = RegisterFromJniTypeSignatureAttribute (c);
				if (r == null) {
					continue;
				}
				yield return r;
			}
		}

		static IEnumerable<RegisterAttribute> GetMethodRegistrationAttributes (Mono.Cecil.ICustomAttributeProvider p)
		{
			foreach (var a in GetAttributes<RegisterAttribute> (p, a => ToRegisterAttribute (a))) {
				yield return a;
			}
			foreach (var c in p.GetCustomAttributes ("Java.Interop.JniConstructorSignatureAttribute")) {
				var r = RegisterFromJniConstructorSignatureAttribute (c);
				if (r == null) {
					continue;
				}
				yield return r;
			}
			foreach (var c in p.GetCustomAttributes ("Java.Interop.JniMethodSignatureAttribute")) {
				var r = RegisterFromJniMethodSignatureAttribute (c);
				if (r == null) {
					continue;
				}
				yield return r;
			}
		}

		IEnumerable<ExportAttribute> GetExportAttributes (IMemberDefinition p)
		{
			return GetAttributes<ExportAttribute> (p, a => ToExportAttribute (a, p))
				.Concat (GetAttributes<ExportAttribute> (p, "Java.Interop.JavaCallableAttribute",
					a => ToExportAttributeFromJavaCallableAttribute (a, p)))
				.Concat (GetAttributes<ExportAttribute> (p, "Java.Interop.JavaCallableConstructorAttribute",
					a => ToExportAttributeFromJavaCallableConstructorAttribute (a, p)));
		}

		static IEnumerable<ExportFieldAttribute> GetExportFieldAttributes (Mono.Cecil.ICustomAttributeProvider p)
		{
			return GetAttributes<ExportFieldAttribute> (p, a => ToExportFieldAttribute (a));
		}

		static IEnumerable<TAttribute> GetAttributes<TAttribute> (Mono.Cecil.ICustomAttributeProvider p, Func<CustomAttribute, TAttribute?> selector)
			where TAttribute : class
		{
			return GetAttributes (p, typeof (TAttribute).FullName, selector);
		}

		static IEnumerable<TAttribute> GetAttributes<TAttribute> (Mono.Cecil.ICustomAttributeProvider p, string attributeName, Func<CustomAttribute, TAttribute?> selector)
			where TAttribute : class
		{
			return p.GetCustomAttributes (attributeName)
				.Select (selector)
				.Where (v => v != null)
				.Select (v => v!);
		}

		void AddMethod (MethodDefinition? registeredMethod, MethodDefinition implementedMethod)
		{
			if (registeredMethod != null)
				foreach (RegisterAttribute attr in GetMethodRegistrationAttributes (registeredMethod)) {
					// Check for Kotlin-mangled methods that cannot be overridden
					if (attr.Name.Contains ("-impl") || (attr.Name.Length > 7 && attr.Name[attr.Name.Length - 8] == '-'))
						Diagnostic.Error (4217, LookupSource (implementedMethod), Localization.Resources.JavaCallableWrappers_XA4217, attr.Name);

					bool shouldBeDynamicallyRegistered = methodClassifier?.ShouldBeDynamicallyRegistered (type, registeredMethod, implementedMethod, attr.OriginAttribute) ?? true;
					var msig = new Signature (implementedMethod, attr, cache, shouldBeDynamicallyRegistered);
					if (!registeredMethod.IsConstructor && !methods.Any (m => m.Name == msig.Name && m.Params == msig.Params))
						methods.Add (msig);
				}
			foreach (ExportAttribute attr in GetExportAttributes (implementedMethod)) {
				if (type.HasGenericParameters)
					Diagnostic.Error (4206, LookupSource (implementedMethod), Localization.Resources.JavaCallableWrappers_XA4206);

				var msig = new Signature (implementedMethod, attr, managedParameters: null, cache: cache);
				if (!string.IsNullOrEmpty (attr.SuperArgumentsString)) {
					// Diagnostic.Warning (log, "Use of ExportAttribute.SuperArgumentsString property is invalid on methods");
				}
				if (!implementedMethod.IsConstructor && !methods.Any (m => m.Name == msig.Name && m.Params == msig.Params))
					methods.Add (msig);
			}
			foreach (ExportFieldAttribute attr in GetExportFieldAttributes (implementedMethod)) {
				if (type.HasGenericParameters)
					Diagnostic.Error (4207, LookupSource (implementedMethod), Localization.Resources.JavaCallableWrappers_XA4207);

				var msig = new Signature (implementedMethod, attr, cache);
				if (!implementedMethod.IsConstructor && !methods.Any (m => m.Name == msig.Name && m.Params == msig.Params)) {
					methods.Add (msig);
					exported_fields.Add (new JavaFieldInfo (implementedMethod, attr.Name, cache));
				}
			}
		}

		string GetManagedParameters (MethodDefinition ctor, string? outerType)
		{
			StringBuilder sb = new StringBuilder ();
			foreach (ParameterDefinition pdef in ctor.Parameters) {
				if (sb.Length > 0)
					sb.Append (':');
				if (outerType != null && sb.Length == 0)
					sb.Append (type.DeclaringType.GetPartialAssemblyQualifiedName (cache));
				else
					sb.Append (pdef.ParameterType.GetPartialAssemblyQualifiedName (cache));
			}
			return sb.ToString ();
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

			bool needCtor = false;
			if (HasDynamicallyRegisteredMethods) {
				needCtor = true;
				writer.WriteLine ("/** @hide */");
				writer.WriteLine ("\tpublic static final String __md_methods;");
			}

			if (children != null) {
				for (int i = 0; i < children.Count; i++) {
					if (!children[i].HasDynamicallyRegisteredMethods) {
						continue;
					}
					needCtor = true;
					writer.Write ("\tstatic final String __md_");
					writer.Write (i + 1);
					writer.WriteLine ("_methods;");
				}
			}

			if (needCtor) {
				writer.WriteLine ("\tstatic {");

				if (HasDynamicallyRegisteredMethods) {
					GenerateRegisterType (writer, this, "__md_methods");
				}

				if (children != null) {
					for (int i = 0; i < children.Count; ++i) {
						GenerateRegisterType (writer, children [i], $"__md_{i + 1}_methods");
					}
				}
				writer.WriteLine ("\t}");
			}

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

		static string GetAnnotationsString (string indent, IEnumerable<CustomAttribute> atts, IMetadataResolver resolver)
		{
			var sw = new StringWriter ();
			WriteAnnotations (indent, sw, atts, resolver);
			return sw.ToString ();
		}

		static void WriteAnnotations (string indent, TextWriter sw, IEnumerable<CustomAttribute> atts, IMetadataResolver resolver)
		{
			foreach (var ca in atts) {
				var catype = resolver.Resolve (ca.AttributeType);
				var tca = catype.CustomAttributes.FirstOrDefault (a => a.AttributeType.FullName == "Android.Runtime.AnnotationAttribute");
				if (tca != null) {
					sw.Write (indent);
					sw.Write ('@');
					sw.Write (tca.ConstructorArguments [0].Value);
					if (ca.Properties.Count > 0) {
						sw.WriteLine ("(");
						bool wrote = false;
						foreach (var p in ca.Properties) {
							if (wrote)
								sw.WriteLine (',');
							var pd = catype.Properties.FirstOrDefault (pp => pp.Name == p.Name);
							var reg = pd != null ? pd.CustomAttributes.FirstOrDefault (pdca => pdca.AttributeType.FullName == "Android.Runtime.RegisterAttribute") : null;
							sw.Write (reg != null ? reg.ConstructorArguments [0].Value : p.Name);
							sw.Write (" = ");
							sw.Write (ManagedValueToJavaSource (p.Argument.Value));
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
			WriteAnnotations ("", sw, type.CustomAttributes, cache);

			sw.WriteLine ("public " + (type.IsAbstract ? "abstract " : "") + "class " + name);

			string extendsType = GetJavaTypeName (type.BaseType, cache);
			if (extendsType == "android.app.Application" && ApplicationJavaClass != null && !string.IsNullOrEmpty (ApplicationJavaClass))
				extendsType = ApplicationJavaClass;
			sw.WriteLine ("\textends " + extendsType);
			sw.WriteLine ("\timplements");
			sw.Write ("\t\t");
			switch (CodeGenerationTarget) {
				case JavaPeerStyle.JavaInterop1:
					sw.Write ("net.dot.jni.GCUserPeerable");
					break;
				default:
					sw.Write ("mono.android.IGCUserPeer");
					break;
			}
			foreach (var ifaceInfo in type.Interfaces) {
				var iface = cache.Resolve(ifaceInfo.InterfaceType);
				if (!GetTypeRegistrationAttributes (iface).Any ())
					continue;
				sw.WriteLine (",");
				sw.Write ("\t\t");
				sw.Write (GetJavaTypeName (iface, cache));
			}
			sw.WriteLine ();
			sw.WriteLine ("{");
		}

		void GenerateBody (TextWriter sw)
		{
			foreach (Signature ctor in ctors) {
				if (string.IsNullOrEmpty (ctor.Params) && JavaNativeTypeManager.IsApplication (type, cache))
					continue;
				GenerateConstructor (ctor, sw);
			}

			GenerateApplicationConstructor (sw);

			foreach (JavaFieldInfo field in exported_fields)
				GenerateExportedField (field, sw);

			foreach (Signature method in methods)
				GenerateMethod (method, sw);

			if (GenerateOnCreateOverrides && JavaNativeTypeManager.IsApplication (type, cache) && !methods.Any (m => m.Name == "onCreate"))
				WriteApplicationOnCreate (sw, w => {
						w.Write ("\t\tmono.android.Runtime.register (\"");
						w.Write (type.GetPartialAssemblyQualifiedName (cache));
						w.Write ("\", ");
						w.Write (name);
						w.WriteLine (".class, __md_methods);");
						w.WriteLine ("\t\tsuper.onCreate ();");
				});
			if (GenerateOnCreateOverrides && JavaNativeTypeManager.IsInstrumentation (type, cache) && !methods.Any (m => m.Name == "onCreate"))
				WriteInstrumentationOnCreate (sw, w => {
						w.Write ("\t\tmono.android.Runtime.register (\"");
						w.Write (type.GetPartialAssemblyQualifiedName (cache));
						w.Write ("\", ");
						w.Write (name);
						w.WriteLine (".class, __md_methods);");
						w.WriteLine ("\t\tsuper.onCreate (arguments);");
				});

			string addRef       = "monodroidAddReference";
			string clearRefs    = "monodroidClearReferences";
			if (CodeGenerationTarget == JavaPeerStyle.JavaInterop1) {
				addRef      = "jiAddManagedReference";
				clearRefs   = "jiClearManagedReferences";
			}

			sw.WriteLine ();
			sw.WriteLine ("\tprivate java.util.ArrayList refList;");
			sw.WriteLine ($"\tpublic void {addRef} (java.lang.Object obj)");
			sw.WriteLine ("\t{");
			sw.WriteLine ("\t\tif (refList == null)");
			sw.WriteLine ("\t\t\trefList = new java.util.ArrayList ();");
			sw.WriteLine ("\t\trefList.add (obj);");
			sw.WriteLine ("\t}");
			sw.WriteLine ();
			sw.WriteLine ($"\tpublic void {clearRefs} ()");
			sw.WriteLine ("\t{");
			sw.WriteLine ("\t\tif (refList != null)");
			sw.WriteLine ("\t\t\trefList.clear ();");
			sw.WriteLine ("\t}");
		}

		void GenerateRegisterType (TextWriter sw, JavaCallableWrapperGenerator self, string field)
		{
			if (!self.HasDynamicallyRegisteredMethods) {
				return;
			}

			sw.Write ("\t\t");
			sw.Write (field);
			sw.WriteLine (" = ");
			string managedTypeName = self.type.GetPartialAssemblyQualifiedName (cache);
			string javaTypeName = $"{package}.{name}";

			foreach (Signature method in self.methods) {
				if (method.IsDynamicallyRegistered) {
					sw.Write ("\t\t\t\"", method.Method);
					sw.Write (method.Method);
					sw.WriteLine ("\\n\" +");
				}
			}
			sw.WriteLine ("\t\t\t\"\";");
			if (CannotRegisterInStaticConstructor (self.type))
				return;
			sw.Write ("\t\t");
			switch (CodeGenerationTarget) {
				case JavaPeerStyle.JavaInterop1:
					sw.Write ("net.dot.jni.ManagedPeer.registerNativeMembers (");
					sw.Write (self.name);
					sw.Write (".class, ");
					sw.Write (field);
					sw.WriteLine (");");
					break;
				default:
					sw.Write ("mono.android.Runtime.register (\"");
					sw.Write (managedTypeName);
					sw.Write ("\", ");
					sw.Write (self.name);
					sw.Write (".class, ");
					sw.Write (field);
					sw.WriteLine (");");
					break;
			}
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

		static string GetJavaTypeName (TypeReference r, IMetadataResolver cache)
		{
			TypeDefinition d = cache.Resolve (r);
			string? jniName = JavaNativeTypeManager.ToJniName (d, cache);
			if (jniName == null) {
				Diagnostic.Error (4201, Localization.Resources.JavaCallableWrappers_XA4201, r.FullName);
				throw new InvalidOperationException ("--nrt:jniName-- Should not be reached");
			}
			return jniName.Replace ('/', '.').Replace ('$', '.');
		}

		bool CannotRegisterInStaticConstructor (TypeDefinition type)
		{
			return JavaNativeTypeManager.IsApplication (type, cache) || JavaNativeTypeManager.IsInstrumentation (type, cache);
		}

		class Signature {

			public Signature (MethodDefinition method, RegisterAttribute register, IMetadataResolver cache, bool shouldBeDynamicallyRegistered = true) : this (method, register, null, null, cache, shouldBeDynamicallyRegistered) {}

			public Signature (MethodDefinition method, RegisterAttribute register, string? managedParameters, string? outerType, IMetadataResolver cache, bool shouldBeDynamicallyRegistered = true)
				: this (register.Name, register.Signature, register.Connector, managedParameters, outerType, null)
			{
				Annotations = JavaCallableWrapperGenerator.GetAnnotationsString ("\t", method.CustomAttributes, cache);
				IsDynamicallyRegistered = shouldBeDynamicallyRegistered;
			}

			public Signature (MethodDefinition method, ExportAttribute export, string? managedParameters, IMetadataResolver cache)
				: this (method.Name, GetJniSignature (method, cache), "__export__", null, null, export.SuperArgumentsString)
			{
				IsExport = true;
				IsStatic = method.IsStatic;
				JavaAccess = JavaCallableWrapperGenerator.GetJavaAccess (method.Attributes & MethodAttributes.MemberAccessMask);
				ThrownTypeNames = export.ThrownNames;
				JavaNameOverride = export.Name;
				ManagedParameters = managedParameters;
				Annotations = JavaCallableWrapperGenerator.GetAnnotationsString ("\t", method.CustomAttributes, cache);
			}

			public Signature (MethodDefinition method, ExportFieldAttribute exportField, IMetadataResolver cache)
				: this (method.Name, GetJniSignature (method, cache), "__export__", null, null, null)
			{
				if (method.HasParameters)
					Diagnostic.Error (4205, JavaCallableWrapperGenerator.LookupSource (method), Localization.Resources.JavaCallableWrappers_XA4205);
				if (method.ReturnType.MetadataType == MetadataType.Void)
					Diagnostic.Error (4208, JavaCallableWrapperGenerator.LookupSource (method), Localization.Resources.JavaCallableWrappers_XA4208);
				IsExport = true;
				IsStatic = method.IsStatic;
				JavaAccess = JavaCallableWrapperGenerator.GetJavaAccess (method.Attributes & MethodAttributes.MemberAccessMask);

				// annotations are processed within JavaFieldInfo, not the initializer method. So we don't generate them here.
			}

			public Signature (string name, string? signature, string? connector, string? managedParameters, string? outerType, string? superCall)
			{
				ManagedParameters = managedParameters;
				JniSignature      = signature ?? throw new ArgumentNullException ("`connector` cannot be null.", nameof (connector));
				Method    = "n_" + name + ":" + JniSignature + ":" + connector;
				Name      = name;

				var jnisig = JniSignature;
				int closer = jnisig.IndexOf (')');
				string ret = jnisig.Substring (closer + 1);
				retval = JavaNativeTypeManager.Parse (ret)?.Type;
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
					string? parmType = jti.Type;
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

			string? call;
			public string? SuperCall {
				get { return call; }
			}

			public string? ActivateCall {get; private set;}

			public readonly string Name;
			public readonly string? JavaNameOverride;
			public string JavaName {
				get { return JavaNameOverride ?? Name; }
			}

			string? parms;
			public string? Params {
				get { return parms; }
			}

			string? retval;
			public string? Retval {
				get { return retval; }
			}

			public string? ThrowsDeclaration {
				get { return ThrownTypeNames?.Length > 0 ? " throws " + String.Join (", ", ThrownTypeNames) : null; }
			}

			public readonly string? JavaAccess;
			public readonly string? ManagedParameters;
			public readonly string JniSignature;
			public readonly string Method;
			public readonly bool IsExport;
			public readonly bool IsStatic;
			public readonly bool IsDynamicallyRegistered = true;
			public readonly string []? ThrownTypeNames;
			public readonly string? Annotations;
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
			sw.Write ("\tpublic ");
			sw.Write (name);
			sw.Write (" (");
			sw.Write (ctor.Params);
			sw.Write (')');
			sw.WriteLine (ctor.ThrowsDeclaration);
			sw.WriteLine ("\t{");
			sw.Write ("\t\tsuper (");
			sw.Write (ctor.SuperCall);
			sw.WriteLine (");");
#if MONODROID_TIMING
			sw.WriteLine ("\t\tandroid.util.Log.i(\"MonoDroid-Timing\", \"{0}..ctor({1}): time: \"+java.lang.System.currentTimeMillis());", name, ctor.Params);
#endif
			if (!CannotRegisterInStaticConstructor (type)) {
				sw.Write ("\t\tif (getClass () == ");
				sw.Write (name);
				sw.WriteLine (".class) {");
				sw.Write ("\t\t\t");
				switch (CodeGenerationTarget) {
					case JavaPeerStyle.JavaInterop1:
						sw.Write ("net.dot.jni.ManagedPeer.construct (this, \"");
						sw.Write (ctor.JniSignature);
						sw.Write ("\", new java.lang.Object[] { ");
						sw.Write (ctor.ActivateCall);
						sw.WriteLine (" });");
						break;
					default:
						sw.Write ("mono.android.TypeManager.Activate (\"");
						sw.Write (type.GetPartialAssemblyQualifiedName (cache));
						sw.Write ("\", \"");
						sw.Write (ctor.ManagedParameters);
						sw.Write ("\", this, new java.lang.Object[] { ");
						sw.Write (ctor.ActivateCall);
						sw.WriteLine (" });");
						break;
				}
				sw.WriteLine ("\t\t}");
			}
			sw.WriteLine ("\t}");
		}

		void GenerateApplicationConstructor (TextWriter sw)
		{
			if (!JavaNativeTypeManager.IsApplication (type, cache)) {
				return;
			}

			sw.WriteLine ();
			sw.Write ("\tpublic ");
			sw.Write (name);
			sw.WriteLine (" ()");
			sw.WriteLine ("\t{");
			sw.WriteLine ("\t\tmono.MonoPackageManager.setContext (this);");
			sw.WriteLine ("\t}");
		}

		void GenerateExportedField (JavaFieldInfo field, TextWriter sw)
		{
			sw.WriteLine ();
			if (field.Annotations != null)
				sw.WriteLine (field.Annotations);
			sw.Write ("\t");
			sw.Write (field.GetJavaAccess ());
			sw.Write (' ');
			if (field.IsStatic)
				sw.Write ("static ");
			sw.Write (field.TypeName);
			sw.Write (' ');
			sw.Write (field.FieldName);
			sw.Write (" = ");
			sw.Write (field.InitializerName);
			sw.WriteLine (" ();");
		}

		void GenerateMethod (Signature method, TextWriter sw)
		{
			sw.WriteLine ();
			if (method.Annotations != null)
				sw.WriteLine (method.Annotations);
			sw.Write ("\t");
			sw.Write (method.IsExport ? method.JavaAccess : "public");
			sw.Write (' ');
			if (method.IsStatic)
				sw.Write ("static ");
			sw.Write (method.Retval);
			sw.Write (' ');
			sw.Write (method.JavaName);
			sw.Write (" (");
			sw.Write (method.Params);
			sw.Write (')');
			sw.WriteLine (method.ThrowsDeclaration);
			sw.WriteLine ("\t{");
#if MONODROID_TIMING
			sw.WriteLine ("\t\tandroid.util.Log.i(\"MonoDroid-Timing\", \"{0}.{1}: time: \"+java.lang.System.currentTimeMillis());", name, method.Name);
#endif
			sw.Write ("\t\t");
			sw.Write (method.Retval == "void" ? String.Empty : "return ");
			sw.Write ("n_");
			sw.Write (method.Name);
			sw.Write (" (");
			sw.Write (method.ActivateCall);
			sw.WriteLine (");");

			sw.WriteLine ("\t}");
			sw.WriteLine ();
			sw.Write ("\tprivate ");
			if (method.IsStatic)
				sw.Write ("static ");
			sw.Write ("native ");
			sw.Write (method.Retval);
			sw.Write (" n_");
			sw.Write (method.Name);
			sw.Write (" (");
			sw.Write (method.Params);
			sw.WriteLine (");");
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

			if (!string.IsNullOrEmpty (MonoRuntimeInitialization)) {
				sw.WriteLine (MonoRuntimeInitialization);
				sw.WriteLine ();
			}

			extra (sw);
			sw.WriteLine ("\t}");
		}

		StreamWriter OpenStream (string outputPath)
		{
			string destination = GetDestinationPath (outputPath);
			Directory.CreateDirectory (Path.GetDirectoryName (destination));
			return new StreamWriter (new FileStream (destination, FileMode.Create, FileAccess.Write));
		}

		/// <summary>
		/// Returns a destination file path based on the package name of this Java type
		/// </summary>
		public string GetDestinationPath (string outputPath)
		{
			var dir = package.Replace ('.', Path.DirectorySeparatorChar);
			return Path.Combine (outputPath, dir, name + ".java");
		}
	}
}
