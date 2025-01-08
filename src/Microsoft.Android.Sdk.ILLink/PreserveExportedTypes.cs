using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Mono.Linker;
using Mono.Linker.Steps;

using Mono.Cecil;
using Microsoft.Android.Sdk.ILLink;

namespace Mono.Tuner {

	public class PreserveExportedTypes : BaseSubStep {

		public override SubStepTargets Targets {
			get {
				return SubStepTargets.Field 
					| SubStepTargets.Method
					| SubStepTargets.Property
					;
			}
		}

		public override bool IsActiveFor (AssemblyDefinition assembly)
		{
			return assembly.MainModule.HasTypeReference ("Java.Interop.ExportAttribute") ||
				assembly.MainModule.HasTypeReference ("Java.Interop.ExportFieldAttribute");
		}

		public override void ProcessField (FieldDefinition field)
		{
			ProcessExports (field);
		}

		public override void ProcessMethod (MethodDefinition method)
		{
			ProcessExports (method);
		}

		public override void ProcessProperty (PropertyDefinition property)
		{
			ProcessExports (property.GetMethod);
			ProcessExports (property.SetMethod);
		}

		public override void ProcessEvent (EventDefinition @event)
		{
		}

		void ProcessExports (ICustomAttributeProvider provider)
		{
			if (provider == null)
				return;
			if (!provider.HasCustomAttributes)
				return;

			var attributes = provider.CustomAttributes;

			for (int i = 0; i < attributes.Count; i++) {
				var attribute = attributes [i];
				switch (attribute.Constructor.DeclaringType.FullName) {
				case "Java.Interop.ExportAttribute":
					Annotations.Mark (provider);
					if (!attribute.HasProperties)
						break;
					var throwsAtt = attribute.Properties.FirstOrDefault (p => p.Name == "Throws");
					var thrownTypesArgs = throwsAtt.Argument.Value != null ? (CustomAttributeArgument []) throwsAtt.Argument.Value : null;
					if (thrownTypesArgs != null)
						foreach (var attArg in thrownTypesArgs)
							Annotations.Mark (((TypeReference) attArg.Value).Resolve ());
					break;
				case "Java.Interop.ExportFieldAttribute":
					Annotations.Mark (provider);
					break;
				default:
					continue;
				}
				if (provider is MemberReference)
					Annotations.Mark (((MemberReference) provider).DeclaringType.Resolve ());
				if (provider is MethodDefinition)
					Annotations.SetAction (((MethodDefinition) provider), MethodAction.ForceParse);
			}
		}
	}
}
