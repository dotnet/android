using System;
using System.Collections;
using System.Linq;

using Mono.Linker;
using Mono.Linker.Steps;

using Mono.Tuner;
using Mobile.Tuner;

using Mono.Cecil;

namespace MonoDroid.Tuner {

	public class PreserveApplications :
#if NET5_LINKER
		BaseMarkHandler
#else   // !NET5_LINKER
		BaseSubStep
#endif  // !NET5_LINKER
	{

#if NET5_LINKER
		public override void Initialize (LinkContext context, MarkContext markContext)
		{
			base.Initialize (context, markContext);
			markContext.RegisterMarkAssemblyAction (assembly => ProcessAssembly (assembly));
			markContext.RegisterMarkTypeAction (type => ProcessType (type));
		}
#else   // !NET5_LINKER
		public override SubStepTargets Targets {
			get { return SubStepTargets.Type
				| SubStepTargets.Assembly;
			}
		}
#endif  // !NET5_LINKER

		public
#if !NET5_LINKER
		override
#endif  // !NET5_LINKER
		bool IsActiveFor (AssemblyDefinition assembly)
		{
			return Annotations.GetAction (assembly) == AssemblyAction.Link;
		}

		public 
#if !NET5_LINKER
		override
#endif  // !NET5_LINKER
		void ProcessAssembly (AssemblyDefinition assembly)
		{
#if NET5_LINKER
			if (!IsActiveFor (assembly))
				return;
#endif  // NET5_LINKER
			ProcessAttributeProvider (assembly);
		}

		public
#if !NET5_LINKER
		override
#endif  // !NET5_LINKER
		void ProcessType (TypeDefinition type)
		{
#if NET5_LINKER
			if (!IsActiveFor (type.Module.Assembly))
				return;
#endif  // NET5_LINKER
			if (!type.Inherits ("Android.App.Application"))
				return;

			ProcessAttributeProvider (type);
		}

		void ProcessAttributeProvider (ICustomAttributeProvider provider)
		{
			if (!provider.HasCustomAttributes)
				return;

			const string ApplicationAttribute = "Android.App.ApplicationAttribute";

			foreach (CustomAttribute attribute in provider.CustomAttributes)
				if (attribute.Constructor.DeclaringType.FullName == ApplicationAttribute)
					PreserveApplicationAttribute (attribute);
		}

		void PreserveApplicationAttribute (CustomAttribute attribute)
		{
			PreserveTypeProperty (attribute, "BackupAgent");
			PreserveTypeProperty (attribute, "ManageSpaceActivity");
		}

		void PreserveTypeProperty (CustomAttribute attribute, string property)
		{
			if (!attribute.HasProperties)
				return;

			var type_ref = (TypeReference) attribute.Properties.First (p => p.Name == property).Argument.Value;
			if (type_ref == null)
				return;

			var type = type_ref.Resolve ();
			if (type == null)
				return;

			PreserveDefaultConstructor (type);
		}

		void PreserveDefaultConstructor (TypeDefinition type)
		{
			if (!type.HasMethods)
				return;

			foreach (MethodDefinition ctor in type.Methods.Where (t => t.IsConstructor)) {
				if (!ctor.IsStatic && !ctor.HasParameters) {
					PreserveMethod (type, ctor);
					break;
				}
			}
		}

		void PreserveMethod (TypeDefinition type, MethodDefinition method)
		{
			Annotations.AddPreservedMethod (type, method);
		}
	}
}
