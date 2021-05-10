using System;
using System.Collections.Generic;
using System.Reflection;
using Java.Interop.Tools.Cecil;
using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;
using Mono.Tuner;
using MonoDroid.Tuner;

namespace Microsoft.Android.Sdk.ILLink
{
	class SetupStep : BaseStep
	{
		List<IStep> _steps;

		List<IStep> Steps {
			get {
				if (_steps == null) {
					var pipeline = typeof (LinkContext).GetProperty ("Pipeline").GetGetMethod ().Invoke (Context, null);
					_steps = (List<IStep>) pipeline.GetType ().GetField ("_steps", BindingFlags.Instance | BindingFlags.NonPublic).GetValue (pipeline);
					//foreach (var step in _steps)
					//	Console.WriteLine ($"step: {step.GetType ().Name}");
				}
				return _steps;
			}
		}

		protected override void Process ()
		{
			if (Context.TryGetCustomData ("XATargetFrameworkDirectories", out string tfmPaths))
				Xamarin.Android.Tasks.MonoAndroidHelper.TargetFrameworkDirectories = tfmPaths.Split (new char [] { ';' });

			// The following steps share state and must be injected via reflection until we get
			// a linker with the fix from https://github.com/mono/linker/pull/2019.
			string androidLinkResources;
			if (Context.TryGetCustomData ("AndroidLinkResources", out androidLinkResources) && bool.TryParse (androidLinkResources, out var linkResources) && linkResources) {
				InsertAfter (new RemoveResourceDesignerStep (),  "CleanStep");
				InsertAfter (new GetAssembliesStep (), "CleanStep");
			}
		}

		void InsertAfter (IStep step, string stepName)
		{
			for (int i = 0; i < Steps.Count;) {
				if (Steps [i++].GetType ().Name == stepName) {
					Steps.Insert (i, step);
					return;
				}
			}

			throw new InvalidOperationException ($"Could not insert {step} after {stepName}.");
		}
	}
}
