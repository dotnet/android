using System;
using System.Collections.Generic;
using System.Text;
using Mono.Tuner;

namespace Microsoft.Android.Sdk.ILLink
{
	public class PreserveSubStepDispatcher : PreMarkSubStepsDispatcher
	{
		public PreserveSubStepDispatcher ()
			: base (new MyBaseSubStep [] {
				new ApplyPreserveAttribute (),
				new PreserveExportedTypes ()
			})
		{
		}
	}

	internal struct CategorizedSubSteps
	{
		public List<MyBaseSubStep> on_assemblies { get; set; }
		public List<MyBaseSubStep> on_types { get; set; }
		public List<MyBaseSubStep> on_fields { get; set; }
		public List<MyBaseSubStep> on_methods { get; set; }
		public List<MyBaseSubStep> on_properties { get; set; }
		public List<MyBaseSubStep> on_events { get; set; }
	}
}
