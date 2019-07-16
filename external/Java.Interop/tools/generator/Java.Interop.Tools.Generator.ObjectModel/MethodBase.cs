using System;

namespace MonoDroid.Generation
{
	public abstract class MethodBase : ApiVersionsSupport.IApiAvailability {

		ParameterList parms;

		protected MethodBase (GenBase declaringType)
		{
			DeclaringType = declaringType;
			parms = new ParameterList ();
		}

		public virtual string AssemblyName {
			get { return null; }
		}

		public virtual bool IsAcw {
			get { return true; }
		}

		public GenBase DeclaringType { get; private set; }

		protected bool HasParameters {
			get { return parms.Count > 0; }
		}
		
		public abstract string Deprecated {
			get;
		}

		public virtual bool IsGeneric {
			get { return parms.HasGeneric; }
		}

		string id_sig;
		internal string IDSignature {
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
			get;
			internal protected set;
		}
		
		public abstract string Visibility {
			get;
		}

		public int ApiAvailableSince { get; set; }

		public bool IsValid { get; private set; }
		public string Annotation { get; internal set; }

		public virtual bool Matches (MethodBase other)
		{
			if (Name != other.Name)
				return false;

			if (Parameters.Count != other.Parameters.Count)
				return false;

			for (int i = 0; i < Parameters.Count; i++) {
				if (Parameters [i].RawNativeType != other.Parameters [i].RawNativeType)
					return false;
			}

			return true;
		}

		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params, CodeGeneratorContext context)
		{
			context.ContextMethod = this;
			try {
				return IsValid = OnValidate (opt, type_params, context);
			} finally {
				context.ContextMethod = null;
			}
		}

		protected virtual bool OnValidate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params, CodeGeneratorContext context)
		{
			var tpl = GenericParameterDefinitionList.Merge (type_params, GenericArguments);
			if (!parms.Validate (opt, tpl, context))
				return false;
			if (Parameters.Count > 14) {
				Report.Warning (0, Report.WarningMethodBase + 0, "More than 16 parameters were found, which goes beyond the maximum number of parameters. ({0})", context.ContextString);
				return false;
			}
			return true;
		}
	}
}
