using System;
using System.Collections.Generic;
using System.Reflection;
using Java.Interop.Tools.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;
using Mono.Tuner;
using MonoDroid.Tuner;

namespace Xamarin.Android.Linker
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
			string tfmPaths;
			if (Context.TryGetCustomData ("XATargetFrameworkDirectories", out tfmPaths))
				Tasks.MonoAndroidHelper.TargetFrameworkDirectories = tfmPaths.Split (new char [] { ';' });

			var subSteps = new SubStepDispatcher ();
			subSteps.Add (new PreserveExportedTypes ());
			subSteps.Add (new MarkJavaObjects ());
			subSteps.Add (new PreserveJavaExceptions ());
			subSteps.Add (new PreserveJavaTypeRegistrations ());
			subSteps.Add (new PreserveApplications ());

			var cache = new TypeDefinitionCache ();
			InsertAfter (new FixAbstractMethodsStep (cache), "RemoveUnreachableBlocksStep");
			InsertAfter (subSteps, "RemoveUnreachableBlocksStep");

			string proguardPath;
			if (Context.TryGetCustomData ("ProguardConfiguration", out proguardPath))
				InsertAfter (new GenerateProguardConfiguration (proguardPath),  "CleanStep");
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
