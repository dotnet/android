using System;
using System.Linq;
using Mono.Cecil;

namespace MonoDroid.Generation
{
#if HAVE_CECIL
	public class ManagedGenBaseSupport : GenBaseSupport
	{
		TypeDefinition t;
		string pkg_name, java_name, full_name;
		GenericParameterDefinitionList type_parameters;
		bool deprecated, is_acw;
		string deprecatedComment;

		public ManagedGenBaseSupport (TypeDefinition t, CodeGenerationOptions opt)
		{
			this.t = t;
			var regatt = t.CustomAttributes.FirstOrDefault (a => a.AttributeType.FullNameCorrected () == "Android.Runtime.RegisterAttribute");
			is_acw = regatt != null;
			string jn = regatt != null ? ((string) regatt.ConstructorArguments [0].Value).Replace ('/', '.') : t.FullNameCorrected ();
			int idx = jn.LastIndexOf ('.');
			pkg_name = idx < 0 ? String.Empty : jn.Substring (0, idx);
			java_name = SymbolTable.FilterPrimitiveFullName (t.FullNameCorrected ());
			if (java_name == null) {
				java_name = idx < 0 ? jn : jn.Substring (idx + 1);
				full_name = t.FullNameCorrected ();
			} else {
				var sym = opt.SymbolTable.Lookup (java_name);
				full_name = sym != null ? sym.FullName : t.FullNameCorrected ();
			}
			java_name = java_name.Replace ('$', '.');
			type_parameters = GenericParameterDefinitionList.FromMetadata (t.GenericParameters);

			var obsolete    = t.CustomAttributes.FirstOrDefault (ca => ca.AttributeType.FullName == "System.ObsoleteAttribute");
			if (obsolete != null) {
				deprecated        = true;
				deprecatedComment = obsolete.HasConstructorArguments
					? obsolete.ConstructorArguments [0].Value.ToString ()
					: "This class is obsoleted in this android platform";
			}
		}

		public override bool IsAcw {
			get { return is_acw; }
		}
		
		public override bool IsDeprecated {
			get { return deprecated; }
		}

		public override bool IsObfuscated {
			get { return false; } // obfuscated types have no chance to be already bound in managed types.
		}
		
		public override string DeprecatedComment {
			get { return deprecatedComment; }
		}

		public override bool IsGeneratable {
			get { return false; }
		}

		public override string FullName {
			get { return full_name; }
			set { throw new NotImplementedException (); }
		}

		public override bool IsGeneric {
			get { return t.HasGenericParameters; }
		}

		public override string JavaSimpleName {
			get { return java_name; }
		}
		
		/*
		public override string Marshaler {
			get { return null; }
		}
		*/

		public override string Name {
			get { return t.Name; }
			set { throw new NotImplementedException (); }
		}

		public override string Namespace {
			get { return t.Namespace; }
		}

		public override string PackageName {
			get { return pkg_name; }
			set { throw new NotImplementedException (); }
		}

		public override string TypeNamePrefix {
			get { return String.Empty; }
		}

		public override GenericParameterDefinitionList TypeParameters {
			get { return type_parameters; }
		}

		public override string Visibility {
			get { return t.IsPublic || t.IsNestedPublic ? "public" : "protected internal"; }
		}
	}
}
#endif
