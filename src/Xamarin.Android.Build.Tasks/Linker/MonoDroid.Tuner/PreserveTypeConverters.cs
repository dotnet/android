using System;
using System.Linq;

using Mono.Linker;

using Mono.Cecil;

namespace Mono.Tuner {

	public class PreserveTypeConverters : BaseSubStep {

		public override SubStepTargets Targets {
			get { return SubStepTargets.Type; }
		}

		public override bool IsActiveFor (AssemblyDefinition assembly)
		{
			return Annotations.GetAction (assembly) == AssemblyAction.Link;
		}

		public override void ProcessType (TypeDefinition type)
		{
			if (IsTypeConverter (type))
				PreserveTypeConverter (type);
		}

		void PreserveTypeConverter (TypeDefinition type)
		{
			if (!type.HasMethods)
				return;

			foreach (MethodDefinition ctor in type.Methods.Where (m => m.IsConstructor)) {
				// We only care about ctors with 0 or 1 params.
				if (ctor.HasParameters && ctor.Parameters.Count > 1)
					continue;

				Annotations.AddPreservedMethod (type, ctor);
			}
		}

		static bool IsTypeConverter (TypeDefinition type)
		{
			return type.Inherits ("System.ComponentModel", "TypeConverter");
		}
	}
}
