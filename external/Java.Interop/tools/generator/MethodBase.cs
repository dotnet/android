using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Mono.Cecil;
using MonoDroid.Utils;

using Java.Interop.Tools.TypeNameMappings;

using Xamarin.Android.Tools;
using System.Xml.Linq;

namespace MonoDroid.Generation {

	public interface IMethodBaseSupport {
		string AssemblyName { get; }
		string Deprecated { get; }
		GenericParameterDefinitionList GenericArguments { get; }
		string Visibility { get; }
	}

#if USE_CECIL
	public class ManagedMethodBaseSupport : IMethodBaseSupport {
		MethodDefinition m;
		public ManagedMethodBaseSupport (MethodDefinition m)
		{
			this.m = m;
			generic_arguments = m.HasGenericParameters ? GenericParameterDefinitionList.FromMetadata (m.GenericParameters) : null;
		}
		
		public string AssemblyName {
			get { return m.DeclaringType.Module.Assembly.FullName; }
		}
		
		public string Deprecated {
			get {
				var v = m.CustomAttributes.FirstOrDefault (a => a.AttributeType.FullName == "System.ObsoleteAttribute");
				return v != null ? (string) v.ConstructorArguments [0].Value ?? "deprecated" : null;
			}
		}
		
		GenericParameterDefinitionList generic_arguments;
		public GenericParameterDefinitionList GenericArguments {
			get { return generic_arguments; }
		}

		public string Visibility {
			get { return m.IsPublic ? "public" : m.IsFamilyOrAssembly ? "protected internal" : m.IsFamily ? "protected" : m.IsAssembly ? "internal" : "private"; }
		}
		
		internal JniType GetJniReturnType (CustomAttribute regatt)
		{
			var jnisig = (string) (regatt.ConstructorArguments.Count > 1 ? regatt.ConstructorArguments [1].Value : regatt.Properties.First (p => p.Name == "JniSignature").Argument.Value);
			return JniType.ReturnTypeFromSignature (jnisig);
		}
		
		public IEnumerable<Parameter> GetParameters (CustomAttribute regatt)
		{
			var jnisig = (string) (regatt.ConstructorArguments.Count > 1 ? regatt.ConstructorArguments [1].Value : regatt.Properties.First (p => p.Name == "JniSignature").Argument.Value);
			var types = jnisig == null ? null : JniType.FromSignature (jnisig);
			var e = types != null ? types.GetEnumerator () : null;

			foreach (var p in m.Parameters) {
				if (e != null && !e.MoveNext ())
					e = null;
				// Here we do some tricky thing:
				// Both java.io.InputStream and java.io.OutputStream could be mapped to
				// System.IO.Stream. And when there is Stream in parameters, we have to
				// determine which direction of the Stream it was - in or out.
				// To do that, we inspect JNI Signature to handle that.
				//
				// We could *always* use this JNI information, *IF* there were no
				// int->enum conversion. Sadly this is not true, we still have to expect
				// custom enum types and cannot simply use JNI signature here.
				var rawtype = e != null ? e.Current.Type : null;
				var type = p.ParameterType.FullName == "System.IO.Stream" && e != null ? e.Current.Type : null;
				yield return Parameter.FromManagedParameter (p, type, rawtype);
			}
		}
	}
#endif
	
	public class XmlMethodBaseSupport : IMethodBaseSupport {

		XElement elem;
		GenericParameterDefinitionList generic_arguments;
		
		public XmlMethodBaseSupport (XElement elem)
		{
			this.elem = elem;
			var tps = elem.Element ("typeParameters");
			generic_arguments = tps != null ? GenericParameterDefinitionList.FromXml (tps) : null;
		}
		
		public XElement Element {
			get { return elem; }
		}
		
		public string AssemblyName {
			get { return null; }
		}

		public string Deprecated {
			get { return elem.XGetAttribute ("deprecated") != "not deprecated" ? elem.XGetAttribute ("deprecated") : null; }
		}
		
		public GenericParameterDefinitionList GenericArguments {
			get { return generic_arguments; }
		}

		public string Visibility {
			get { return elem.XGetAttribute ("visibility"); }
		}
	}

	public abstract class MethodBase : ApiVersionsSupport.IApiAvailability {

		ParameterList parms;
		IMethodBaseSupport support;

		protected MethodBase (GenBase declaringType, IMethodBaseSupport support)
		{
			DeclaringType = declaringType;
			this.support = support;
			parms = new ParameterList ();
		}

		public virtual bool IsAcw {
			get { return true; }
		}

		public GenBase DeclaringType { get; private set; }
		
		public string AssemblyName {
			get { return support.AssemblyName; }
		}

		protected bool HasParameters {
			get { return parms.Count > 0; }
		}
		
		public string Deprecated {
			get { return support.Deprecated; }
		}

		public virtual bool IsGeneric {
			get { return parms.HasGeneric; }
		}

		string id_sig;
		protected string IDSignature {
			get {
				if (id_sig == null)
					id_sig = HasParameters ? "_" + Parameters.JniSignature.Replace ("/", "_").Replace ("`", "_").Replace (";", "_").Replace ("$", "_").Replace ("[", "array") : String.Empty;
				return id_sig;
			}
		}

		public abstract string Name { get; set; }

		public ParameterList Parameters {
			get { return parms; }
		}
		
		public GenericParameterDefinitionList GenericArguments {
			get { return support.GenericArguments; }
		}
		
		public string Visibility {
			get { return support.Visibility; }
		}

		public int ApiAvailableSince { get; set; }

		public bool IsValid { get; private set; }
		public string Annotation { get; internal set; }

		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params)
		{
			opt.ContextMethod = this;
			try {
				return IsValid = OnValidate (opt, type_params);
			} finally {
				opt.ContextMethod = null;
			}
		}

		protected virtual bool OnValidate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params)
		{
			var tpl = GenericParameterDefinitionList.Merge (type_params, GenericArguments);
			if (!parms.Validate (opt, tpl))
				return false;
			if (Parameters.Count > 14) {
				Report.Warning (0, Report.WarningMethodBase + 0, "More than 16 parameters were found, which goes beyond the maximum number of parameters. ({0})", opt.ContextString);
				return false;
			}
			return true;
		}
	}
}
