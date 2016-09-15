using System;
using System.Linq;

using Mono.Linker;

using Mono.Cecil;

namespace Mono.Tuner {
	
	// WARNING: this link substep is placed after marking all required stuff.
	public class PreserveLinqExpressions : BaseSubStep {
		
		public override SubStepTargets Targets {
			get { return SubStepTargets.Type; }
		}

		public override bool IsActiveFor (AssemblyDefinition assembly)
		{
			// scan all user assemblies even if it is not linked.
			return !Xamarin.Android.Tasks.MonoAndroidHelper.IsFrameworkAssembly (assembly.Name.Name);
		}
		
		bool already_marked;

		public override void ProcessType (TypeDefinition type)
		{
			if (already_marked)
				return;
			if (IsLinqProvider (type)) {
				already_marked = true;
				AssemblyDefinition core;
				if (!context.TryGetLinkedAssembly ("System.Core", out core))
					return;
				foreach (var t in core.MainModule.Types.Where (t => t.Namespace == "System.Linq" || t.Namespace == "System.Linq.Expressions"))
					Annotations.SetPreserve (t, TypePreserve.All);
			}
		}

		bool IsLinqProvider (TypeDefinition type)
		{
			// skip unmarked types.
			if (!Annotations.IsMarked (type))
				return false;
			if (type.Namespace == "System.Linq")
				return false; // we are not looking for default system types.
			if (!type.HasInterfaces)
				return false;
			return type.Interfaces.Any (i => i.InterfaceType.FullName == "System.Linq.IQueryProvider")
					|| type.Interfaces.Select (t => t.InterfaceType.Resolve ()).Any (t => t != null && IsLinqProvider (t));
		}
	}
}
