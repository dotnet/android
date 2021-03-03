using System;
using System.Collections.Generic;
using System.Text;
using Mono.Linker.Steps;

namespace Microsoft.Android.Sdk.ILLink
{
	public class SubStepDispatcher : MarkSubStepsDispatcher
	{
		public SubStepDispatcher (IEnumerable<ISubStep> subSteps) : base (subSteps)
		{
		}
	}
}
