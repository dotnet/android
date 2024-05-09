using System;
using System.Collections;
using System.Linq;

using Mono.Linker;
using Mono.Linker.Steps;

using Mono.Tuner;
using Mobile.Tuner;

using Mono.Cecil;

namespace MonoDroid.Tuner {

	public class PreserveApplications : BaseMarkHandler
	{
		public override void Initialize (LinkContext context, MarkContext markContext)
		{
			base.Initialize (context, markContext);
			markContext.RegisterMarkAssemblyAction (assembly => ProcessAssembly (assembly));
			markContext.RegisterMarkTypeAction (type => ProcessType (type));
		}

		public bool IsActiveFor (AssemblyDefinition assembly)
		{
			return Annotations.GetAction (assembly) == AssemblyAction.Link;
		}

		public void ProcessAssembly (AssemblyDefinition assembly)
		{
			if (!IsActiveFor (assembly))
				return;
			ProcessAttributeProvider (assembly);
		}

		public void ProcessType (TypeDefinition type)
		{
			if (!IsActiveFor (type.Module.Assembly))
				return;
			if (!type.Inherits ("Android.App.Application", cache))
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

			// NOTE: CustomAttributeNamedArgument is a struct
			var named_arg = attribute.Properties.FirstOrDefault (p => p.Name == property);
			if (named_arg.Name == null)
				return;

			var type_ref = named_arg.Argument.Value as TypeReference;
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
