using System;
using System.Collections;
using System.Linq;

using Mono.Linker;
using Mono.Linker.Steps;

using Mono.Tuner;
using Mobile.Tuner;

using Mono.Cecil;

namespace MonoDroid.Tuner {

	public class PreserveJavaTypeRegistrations : BaseSubStep {

		public override SubStepTargets Targets {
			get { return SubStepTargets.Type; }
		}

		public override void ProcessType (TypeDefinition type)
		{
			if (type.FullName == "Java.Interop.__TypeRegistrations")
				PreserveJavaTypeRegistration (type);
		}

		void PreserveJavaTypeRegistration (TypeDefinition type)
		{
			Annotations.Mark (type);
			Annotations.SetPreserve (type, TypePreserve.All);
		}
	}
}
