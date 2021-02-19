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
			string tfmPaths;
			if (Context.TryGetCustomData ("XATargetFrameworkDirectories", out tfmPaths))
				Xamarin.Android.Tasks.MonoAndroidHelper.TargetFrameworkDirectories = tfmPaths.Split (new char [] { ';' });

			var subSteps1 = new SubStepDispatcher ();
			subSteps1.Add (new ApplyPreserveAttribute ());

			var cache = new TypeDefinitionCache ();
			var subSteps2 = new SubStepDispatcher ();
			subSteps2.Add (new PreserveExportedTypes ());
			subSteps2.Add (new MarkJavaObjects ());
			subSteps2.Add (new PreserveJavaExceptions ());
			subSteps2.Add (new PreserveApplications ());
			subSteps2.Add (new PreserveRegistrations (cache));
			subSteps2.Add (new PreserveJavaInterfaces ());

			ProcessDispatcher (subSteps1);
			ProcessDispatcher (subSteps1);
			ProcessStep (new FixAbstractMethodsStep (cache));

			string proguardPath;
			if (Context.TryGetCustomData ("ProguardConfiguration", out proguardPath))
				InsertAfter (new GenerateProguardConfiguration (proguardPath),  "CleanStep");

			string addKeepAlivesStep;
			if (Context.TryGetCustomData ("AddKeepAlivesStep", out addKeepAlivesStep) && bool.TryParse (addKeepAlivesStep, out var bv) && bv)
				InsertAfter (new AddKeepAlivesStep (cache), "CleanStep");

			InsertAfter (new StripEmbeddedLibraries (),  "CleanStep");
		}

		static Type dispatcherType = typeof (SubStepsDispatcher);
		static MethodInfo initMethod = dispatcherType.GetMethod ("InitializeSubSteps", BindingFlags.NonPublic | BindingFlags.Instance);
		static MethodInfo browseMethod = dispatcherType.GetMethod ("BrowseAssemblies", BindingFlags.NonPublic | BindingFlags.Instance);
		static MethodInfo getReferencedAssembliesMethod = typeof (LinkContext).GetMethod ("GetReferencedAssemblies", BindingFlags.Public | BindingFlags.Instance);
		static MethodInfo processAssemblyMethod = typeof (BaseStep).GetMethod ("ProcessAssembly", BindingFlags.NonPublic | BindingFlags.Instance);

		void ProcessStep (BaseStep step)
		{
			typeof (BaseStep).GetField ("_context", BindingFlags.Instance | BindingFlags.NonPublic).SetValue (step, Context);

			foreach (var assembly in (IEnumerable<AssemblyDefinition>) getReferencedAssembliesMethod.Invoke (Context, null))
				processAssemblyMethod.Invoke (step, new object [] { assembly });
		}

		void ProcessDispatcher (SubStepDispatcher dispatcher)
		{
			initMethod.Invoke (dispatcher, new object [] { Context });
			browseMethod.Invoke (dispatcher, new object [] { getReferencedAssembliesMethod.Invoke (Context, null) });
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
