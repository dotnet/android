using System;
using System.Linq;
using System.Text;
using MonoDroid.Generation.Utilities;

namespace MonoDroid.Generation
{
	public abstract class MethodBase : ApiVersionsSupport.IApiAvailability
	{
		protected MethodBase (GenBase declaringType)
		{
			DeclaringType = declaringType;
		}

		public string Annotation { get; internal set; }
		public int ApiAvailableSince { get; set; }
		public string AssemblyName { get; set; }
		public GenBase DeclaringType { get; }
		public string Deprecated { get; set; }
		public GenericParameterDefinitionList GenericArguments { get; set; }
		public bool IsAcw { get; set; }
		public bool IsValid { get; private set; }
		public string Name { get; set; }
		public ParameterList Parameters { get; } = new ParameterList ();
		public string Visibility { get; set; }

		public string [] AutoDetectEnumifiedOverrideParameters (AncestorDescendantCache cache)
		{
			if (Parameters.All (p => p.Type != "int"))
				return null;

			var classes = cache.GetAncestorsAndDescendants (DeclaringType);
			classes = classes.Concat (classes.SelectMany (x => x.GetAllImplementedInterfaces ()));

			foreach (var t in classes) {
				foreach (var candidate in t.GetAllMethods ().Where (m => m.Name == Name
					&& m.Parameters.Count == Parameters.Count
					&& m.Parameters.Any (p => p.IsEnumified))) {
					var ret = new string [Parameters.Count];
					bool mismatch = false;
					for (int i = 0; i < Parameters.Count; i++) {
						if (Parameters [i].Type == "int" && candidate.Parameters [i].IsEnumified)
							ret [i] = candidate.Parameters [i].Type;
						else if (Parameters [i].Type != candidate.Parameters [i].Type) {
							mismatch = true;
							break;
						}
					}
					if (mismatch)
						continue;
					for (int i = 0; i < ret.Length; i++)
						if (ret [i] != null)
							Parameters [i].SetGeneratedEnumType (ret [i]);
					return ret;
				}
			}
			return null;
		}

		public string GetSignature (CodeGenerationOptions opt)
		{
			var sb = new StringBuilder ();

			foreach (var p in Parameters) {
				if (sb.Length > 0)
					sb.Append (", ");
				if (p.IsEnumified)
					sb.Append ("[global::Android.Runtime.GeneratedEnum] ");
				if (p.Annotation != null)
					sb.Append (p.Annotation);
				sb.Append (opt.GetTypeReferenceName (p));
				sb.Append (" ");
				sb.Append (opt.GetSafeIdentifier (p.Name));
			}
			return sb.ToString ();
		}

		internal string IDSignature => Parameters.Count > 0 ? "_" + Parameters.JniSignature.Replace ("/", "_").Replace ("`", "_").Replace (";", "_").Replace ("$", "_").Replace ("[", "array") : string.Empty;

		public virtual bool IsGeneric => Parameters.HasGeneric;

		public bool IsKotlinNameMangled {
			get {
				// Kotlin generates methods that cannot be referenced in Java,
				// like `add-impl` and `add-V5j3Lk8`. We will need to fix those later.
				if (this is Method method) {
					if (method.JavaName.IndexOf ("-impl") >= 0)
						return true;

					return method.JavaName.Length >= 8 && method.JavaName [method.JavaName.Length - 8] == '-';
				}

				return false;
			}
		}

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

		protected virtual bool OnValidate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params, CodeGeneratorContext context)
		{
			var tpl = GenericParameterDefinitionList.Merge (type_params, GenericArguments);
			if (!Parameters.Validate (opt, tpl, context))
				return false;
			if (Parameters.Count > 14) {
				Report.Warning (0, Report.WarningMethodBase + 0, "More than 16 parameters were found, which goes beyond the maximum number of parameters. ({0})", context.ContextString);
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
	}
}
