using System.Collections.Generic;
using System.Diagnostics;
using Mono.Cecil;
using Mono.Collections.Generic;
using Mono.Linker;
using Mono.Linker.Steps;

namespace Microsoft.Android.Sdk.ILLink
{
	public class PreMarkSubStepsDispatcher : BaseStep
	{
		readonly List<MyBaseSubStep> substeps;

		CategorizedSubSteps? categorized;
		CategorizedSubSteps Categorized {
			get {
				Debug.Assert (categorized.HasValue);
				return categorized.Value;
			}
		}
		protected override void Process()
		{
			InitializeSubSteps (Context);
		}
		protected override void ProcessAssembly(AssemblyDefinition assembly)
		{
			BrowseAssembly (assembly);
		}

		public PreMarkSubStepsDispatcher (IEnumerable<MyBaseSubStep> subSteps)
		{
			substeps = [.. subSteps];
		}

		static bool HasSubSteps (List<MyBaseSubStep> substeps) => substeps?.Count > 0;

		void BrowseAssembly (AssemblyDefinition assembly)
		{
			CategorizeSubSteps (assembly);

			if (!ShouldDispatchTypes ())
				return;

			BrowseTypes (assembly.MainModule.Types);
		}

		bool ShouldDispatchTypes ()
		{
			return HasSubSteps (Categorized.on_types)
			    || HasSubSteps (Categorized.on_fields)
			    || HasSubSteps (Categorized.on_methods)
			    || HasSubSteps (Categorized.on_properties)
			    || HasSubSteps (Categorized.on_events);
		}

		void BrowseTypes (Collection<TypeDefinition> types)
		{
			foreach (TypeDefinition type in types) {
				DispatchType (type);

				if (type.HasFields && HasSubSteps (Categorized.on_fields)) {
					foreach (FieldDefinition field in type.Fields)
						DispatchField (field);
				}

				if (type.HasMethods && HasSubSteps (Categorized.on_methods)) {
					foreach (MethodDefinition method in type.Methods)
						DispatchMethod (method);
				}

				if (type.HasProperties && HasSubSteps (Categorized.on_properties)) {
					foreach (PropertyDefinition property in type.Properties)
						DispatchProperty (property);
				}

				if (type.HasEvents && HasSubSteps (Categorized.on_events)) {
					foreach (EventDefinition @event in type.Events)
						DispatchEvent (@event);
				}

				if (type.HasNestedTypes)
					BrowseTypes (type.NestedTypes);
			}
		}

		void DispatchType (TypeDefinition type)
		{
			foreach (var substep in Categorized.on_types) {
				substep.ProcessType (type);
			}
		}

		void DispatchField (FieldDefinition field)
		{
			foreach (var substep in Categorized.on_fields) {
				substep.ProcessField (field);
			}
		}

		void DispatchMethod (MethodDefinition method)
		{
			foreach (var substep in Categorized.on_methods) {
				substep.ProcessMethod (method);
			}
		}

		void DispatchProperty (PropertyDefinition property)
		{
			foreach (var substep in Categorized.on_properties) {
				substep.ProcessProperty (property);
			}
		}

		void DispatchEvent (EventDefinition @event)
		{
			foreach (var substep in Categorized.on_events) {
				substep.ProcessEvent (@event);
			}
		}

		void InitializeSubSteps (LinkContext context)
		{
			foreach (var substep in substeps)
				substep.Initialize (context);
		}

		void CategorizeSubSteps (AssemblyDefinition assembly)
		{
			categorized = new CategorizedSubSteps {
				on_assemblies = new List<MyBaseSubStep> (),
				on_types = new List<MyBaseSubStep> (),
				on_fields = new List<MyBaseSubStep> (),
				on_methods = new List<MyBaseSubStep> (),
				on_properties = new List<MyBaseSubStep> (),
				on_events = new List<MyBaseSubStep> ()
			};

			foreach (var substep in substeps)
				CategorizeSubStep (substep, assembly);
		}

		void CategorizeSubStep (MyBaseSubStep substep, AssemblyDefinition assembly)
		{
			if (!substep.IsActiveFor (assembly))
				return;

			CategorizeTarget (substep, SubStepTargets.Assembly, Categorized.on_assemblies);
			CategorizeTarget (substep, SubStepTargets.Type, Categorized.on_types);
			CategorizeTarget (substep, SubStepTargets.Field, Categorized.on_fields);
			CategorizeTarget (substep, SubStepTargets.Method, Categorized.on_methods);
			CategorizeTarget (substep, SubStepTargets.Property, Categorized.on_properties);
			CategorizeTarget (substep, SubStepTargets.Event, Categorized.on_events);
		}

		static void CategorizeTarget (MyBaseSubStep substep, SubStepTargets target, List<MyBaseSubStep> list)
		{
			if (!Targets (substep, target))
				return;

			list.Add (substep);
		}

		static bool Targets (MyBaseSubStep substep, SubStepTargets target) => (substep.Targets & target) == target;
	}
}
