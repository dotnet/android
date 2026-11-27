#nullable enable

using System;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using MonoDroid.Tuner;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// This task is called for builds that did not use ILLink. It runs "linker steps" that
	/// should be run on assemblies even in non-linked builds.
	/// </summary>
	[MSBuildMultiThreadableTask]
	public class LinkAssembliesNoShrink : AssemblyModifierPipeline
	{
		readonly KeepAliveFixupReporter keepAliveFixupReporter;

		public override string TaskPrefix => "LNS";

		public LinkAssembliesNoShrink ()
		{
			keepAliveFixupReporter = new KeepAliveFixupReporter (
				(message, args) => Log.LogCodedWarning ("XA0150", message, args));
		}

		public bool AddKeepAlives { get; set; }

		// MSBuild supplies the typemap-specific default. Keep direct task callers backward compatible.
		public bool EnableLegacyCompatibilityAssemblyFixups { get; set; } = true;

		public bool UseDesignerAssembly { get; set; }

		public override bool RunTask ()
		{
			keepAliveFixupReporter.Reset ();
			return base.RunTask ();
		}

		protected override void BuildPipeline (AssemblyPipeline pipeline, MSBuildLinkContext context)
		{
			if (EnableLegacyCompatibilityAssemblyFixups) {
				// FixAbstractMethodsStep
				var fixAbstractMethodsStep = new FixAbstractMethodsStep ();
				fixAbstractMethodsStep.Initialize (context);
				pipeline.Steps.Add (fixAbstractMethodsStep);

				// FixLegacyResourceDesignerStep
				if (UseDesignerAssembly) {
					var fixLegacyResourceDesignerStep = new FixLegacyResourceDesignerStep ();
					fixLegacyResourceDesignerStep.Initialize (context);
					pipeline.Steps.Add (fixLegacyResourceDesignerStep);
				}

				// AddKeepAlivesStep
				if (AddKeepAlives) {
					var addKeepAliveStep = new AddKeepAlivesStep (keepAliveFixupReporter);
					addKeepAliveStep.Initialize (context);
					pipeline.Steps.Add (addKeepAliveStep);
				}
			} else {
				Log.LogDebugMessage ("Skipping legacy compatibility assembly fixups. Set `AndroidEnableLegacyCompatibilityAssemblyFixups` to `true` to enable them.");
			}

			// Ensure the <AssemblyModifierPipeline> task's steps are added
			base.BuildPipeline (pipeline, context);
		}
	}
}
