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
#if ILLINK
		BaseMarkHandler
#else   // !ILLINK
		BaseSubStep
#endif  // !ILLINK
	{

#if ILLINK
		public override void Initialize (LinkContext context, MarkContext markContext)
		{
			base.Initialize (context, markContext);
			markContext.RegisterMarkAssemblyAction (assembly => ProcessAssembly (assembly));
			markContext.RegisterMarkTypeAction (type => ProcessType (type));
		}
#else   // !ILLINK
		public override SubStepTargets Targets {
			get { return SubStepTargets.Type
				| SubStepTargets.Assembly;
			}
		}
#endif  // !ILLINK

		public
#if !ILLINK
		override
#endif  // !ILLINK
		bool IsActiveFor (AssemblyDefinition assembly)
		{
			return Annotations.GetAction (assembly) == AssemblyAction.Link;
		}

		public 
#if !ILLINK
		override
#endif  // !ILLINK
		void ProcessAssembly (AssemblyDefinition assembly)
		{
#if ILLINK
			if (!IsActiveFor (assembly))
				return;
#endif  // ILLINK
			ProcessAttributeProvider (assembly);
		}

		public
#if !ILLINK
		override
#endif  // !ILLINK
		void ProcessType (TypeDefinition type)
		{
#if ILLINK
			if (!IsActiveFor (type.Module.Assembly))
				return;
#endif  // ILLINK
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
