using System;
using Mono.Linker.Steps;
using MonoDroid.Tuner;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// This task is called for builds that did not use ILLink. It runs "linker steps" that
	/// should be run on assemblies even in non-linked builds.
	/// </summary>
	public class LinkAssembliesNoShrink : AssemblyModifierPipeline
	{
		public override string TaskPrefix => "LNS";

		public bool AddKeepAlives { get; set; }

		public bool UseDesignerAssembly { get; set; }

		protected override void BuildPipeline (AssemblyPipeline pipeline, MSBuildLinkContext context)
		{
			// FixAbstractMethodsStep
			var fixAbstractMethodsStep = new FixAbstractMethodsStep ();
			fixAbstractMethodsStep.Initialize (context, new EmptyMarkContext ());
			pipeline.Steps.Add (fixAbstractMethodsStep);

			// FixLegacyResourceDesignerStep
			if (UseDesignerAssembly) {
				var fixLegacyResourceDesignerStep = new FixLegacyResourceDesignerStep ();
				fixLegacyResourceDesignerStep.Initialize (context);
				pipeline.Steps.Add (fixLegacyResourceDesignerStep);
			}

			// AddKeepAlivesStep
			if (AddKeepAlives) {
				var addKeepAliveStep = new AddKeepAlivesStep ();
				addKeepAliveStep.Initialize (context);
				pipeline.Steps.Add (addKeepAliveStep);
			}

			// Ensure the <AssemblyModifierPipeline> task's steps are added
			base.BuildPipeline (pipeline, context);
		}
	}
}
