using System;
using System.Collections.Generic;
using System.Text;
using Mono.Linker.Steps;
using Mono.Tuner;

namespace Microsoft.Android.Sdk.ILLink
{
	public class PreserveSubStepDispatcher : MarkSubStepsDispatcher
	{
		public PreserveSubStepDispatcher ()
			: base (new ISubStep[] {
				new ApplyPreserveAttribute (),
				new PreserveExportedTypes ()
			})
		{
		}
	}
}
