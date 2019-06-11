using System.Linq;
using Mono.Cecil;

namespace MonoDroid.Generation
{
#if HAVE_CECIL
	public class ManagedCtor : Ctor {
		MethodDefinition m;
		string name;
		bool is_acw;

		public ManagedCtor (GenBase declaringType, MethodDefinition m)
			: base (declaringType)
		{
			this.m = m;
			GenericArguments = m.GenericArguments ();
			name = m.Name;
			// If 'elem' is a constructor for a non-static nested type, then
			// the type of the containing class must be inserted as the first
			// argument
			if (IsNonStaticNestedType)
				Parameters.AddFirst (Parameter.FromManagedType (m.DeclaringType.DeclaringType, DeclaringType.JavaName));
			var regatt = m.CustomAttributes.FirstOrDefault (a => a.AttributeType.FullName == "Android.Runtime.RegisterAttribute");
			is_acw = regatt != null;
			foreach (var p in m.GetParameters (regatt))
				Parameters.Add (p);
		}

		public override bool IsAcw {
			get { return is_acw; }
		}
		
		public override bool IsNonStaticNestedType {
			// not a beautiful way to check static type, yes :|
			get { return m.DeclaringType.IsNested && !(m.DeclaringType.IsAbstract && m.DeclaringType.IsSealed); }
		}

		public override string Name {
			get { return name; }
			set { name = value; }
		}

		public override string CustomAttributes {
			get { return null; }
		}

		public override string AssemblyName => m.DeclaringType.Module.Assembly.FullName;

		public override string Visibility => m.Visibility ();

		public override string Deprecated => m.Deprecated ();
	}
}
#endif
