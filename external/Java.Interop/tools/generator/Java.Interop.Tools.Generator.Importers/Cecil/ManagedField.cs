using System;
using System.Linq;
using Mono.Cecil;

namespace MonoDroid.Generation
{
#if HAVE_CECIL
	public class ManagedField : Field {
		FieldDefinition f;
		string java_name;
		bool is_acw;

		public ManagedField (FieldDefinition f)
		{
			this.f = f;
			var regatt = f.CustomAttributes.FirstOrDefault (a => a.AttributeType.FullNameCorrected () == "Android.Runtime.RegisterAttribute");
			is_acw = regatt != null;
			java_name = regatt != null ? ((string) regatt.ConstructorArguments [0].Value).Replace ('/', '.') : f.Name;
		}

		public override bool IsAcw {
			get { return is_acw; }
		}

		public override bool IsDeprecated {
			get { return f.CustomAttributes.Any (a => a.AttributeType.FullNameCorrected () == "System.ObsoleteAttribute"); }
		}

		public override string DeprecatedComment {
			get {
				if (!IsDeprecated)
					return null;
				var ca = f.CustomAttributes.First (a => a.AttributeType.FullNameCorrected () == "System.ObsoleteAttribute");
				return ca.ConstructorArguments.Any () ? (string) ca.ConstructorArguments [0].Value : string.Empty;
			}
		}
		
		public override bool IsEnumified {
			get { return f.CustomAttributes.Any (c => c.AttributeType.FullName == "Android.Runtime.GeneratedEnumAttribute"); }
		}

		public override bool IsFinal {
			get { return f.Constant != null; }
		}

		public override bool IsStatic {
			get { return f.IsStatic; }
		}

		public override string JavaName {
			get { return java_name; }
		}

		public override string TypeName {
			get { return f.FieldType.FullNameCorrected (); }
		}

		public override string Name {
			get { return f.Name; }
			set { throw new NotSupportedException (); }
		}

		public override string Value {
			get { return f.Constant == null ? null : f.FieldType.FullName == "System.String" ? '"' + f.Constant.ToString () + '"' : f.Constant.ToString (); }
		}

		public override string Visibility {
			get { return f.IsPublic ? "public" : f.IsFamilyOrAssembly ? "protected internal" : f.IsFamily ? "protected" : f.IsAssembly ? "internal" : "private"; }
		}

		protected override Parameter SetterParameter {
			get {
				var p = Parameter.FromManagedType (f.FieldType.Resolve (), null);
				p.Name = "value";
				return p;
			}
		}
	}
#endif
}
