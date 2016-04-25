using System;
using System.Collections;
using System.Linq;

using Mono.Linker;
using Mono.Linker.Steps;

using Mono.Tuner;
using Mobile.Tuner;

using Mono.Cecil;

namespace MonoDroid.Tuner {

	public class PreserveJavaExceptions : BaseSubStep {

		public override SubStepTargets Targets {
			get { return SubStepTargets.Type; }
		}

		public override void ProcessType (TypeDefinition type)
		{
			if (type.IsJavaException ())
				PreserveJavaException (type);
		}

		void PreserveJavaException (TypeDefinition type)
		{
			PreserveStringConstructor (type);
		}

		void PreserveStringConstructor (TypeDefinition type)
		{
			var constructor = GetStringConstructor (type);
			if (constructor == null)
				return;

			PreserveMethod (type, constructor);
		}

		MethodDefinition GetStringConstructor (TypeDefinition type)
		{
			if (!type.HasMethods)
				return null;

			foreach (MethodDefinition constructor in type.Methods.Where (m => m.IsConstructor)) {
				if (!constructor.HasParameters)
					continue;

				if (constructor.Parameters.Count != 1 || constructor.Parameters [0].ParameterType.FullName != "System.String")
					continue;

				return constructor;
			}

			return null;
		}

		void PreserveMethod (TypeDefinition type, MethodDefinition method)
		{
			Annotations.AddPreservedMethod (type, method);
		}
	}
}
