using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;
using Xamarin.Android.Tasks;

namespace Microsoft.Android.Sdk.ILLink
{

	public class ApplyPreserveAttribute : BaseSubStep {

		public override SubStepTargets Targets {
			get {
				return SubStepTargets.Type
					| SubStepTargets.Field
					| SubStepTargets.Method
					| SubStepTargets.Property
					| SubStepTargets.Event;
			}
		}

		public override bool IsActiveFor (AssemblyDefinition assembly)
		{
			return !MonoAndroidHelper.IsFrameworkAssembly (assembly) && Annotations.GetAction (assembly) == AssemblyAction.Link;
		}

		public override void ProcessType (TypeDefinition type)
		{
			TryApplyPreserveAttribute (type);
		}

		public override void ProcessField (FieldDefinition field)
		{
			foreach (var attribute in GetPreserveAttributes (field))
				Mark (field, attribute);
		}

		public override void ProcessMethod (MethodDefinition method)
		{
			MarkMethodIfPreserved (method);
		}

		public override void ProcessProperty (PropertyDefinition property)
		{
			foreach (var attribute in GetPreserveAttributes (property)) {
				MarkMethod (property.GetMethod, attribute);
				MarkMethod (property.SetMethod, attribute);
			}
		}

		public override void ProcessEvent (EventDefinition @event)
		{
			foreach (var attribute in GetPreserveAttributes (@event)) {
				MarkMethod (@event.AddMethod, attribute);
				MarkMethod (@event.InvokeMethod, attribute);
				MarkMethod (@event.RemoveMethod, attribute);
			}
		}

		void MarkMethodIfPreserved (MethodDefinition method)
		{
			foreach (var attribute in GetPreserveAttributes (method))
				MarkMethod (method, attribute);
		}

		void MarkMethod (MethodDefinition method, CustomAttribute preserve_attribute)
		{
			if (method == null)
				return;

			Mark (method, preserve_attribute);
			Annotations.SetAction (method, MethodAction.Parse);
		}

		void Mark (IMetadataTokenProvider provider, CustomAttribute preserve_attribute)
		{
			if (IsConditionalAttribute (preserve_attribute)) {
				PreserveConditional (provider);
				return;
			}

			PreserveUnconditional (provider);
		}

		void PreserveConditional (IMetadataTokenProvider provider)
		{
			var method = provider as MethodDefinition;
			if (method == null) {
				// workaround to support (uncommon but valid) conditional fields form [Preserve]
				PreserveUnconditional (provider);
				return;
			}

			Annotations.AddPreservedMethod (method.DeclaringType, method);
		}

		static bool IsConditionalAttribute (CustomAttribute attribute)
		{
			if (attribute == null)
				return false;

			foreach (var named_argument in attribute.Fields)
				if (named_argument.Name == "Conditional")
					return (bool) named_argument.Argument.Value;

			return false;
		}

		void PreserveUnconditional (IMetadataTokenProvider provider)
		{
			Annotations.Mark (provider);

			var member = provider as IMemberDefinition;
			if (member == null || member.DeclaringType == null)
				return;

			Mark (member.DeclaringType, null);
		}

		void TryApplyPreserveAttribute (TypeDefinition type)
		{
			if (!type.HasCustomAttributes)
				return;

			foreach (var attribute in GetPreserveAttributes (type)) {
				Annotations.Mark (type);

				if (!attribute.HasFields)
					continue;

				foreach (var named_argument in attribute.Fields)
					if (named_argument.Name == "AllMembers" && (bool)named_argument.Argument.Value)
						Annotations.SetPreserve (type, TypePreserve.All);
			}
		}

		static IEnumerable<CustomAttribute> GetPreserveAttributes (ICustomAttributeProvider provider)
		{
			return provider.CustomAttributes.Where (a => a.Constructor.DeclaringType.Name == "PreserveAttribute");
		}
	}
}
