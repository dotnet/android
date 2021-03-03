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
		List<IMarkHandler> _markHandlers;

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

		static MethodInfo getReferencedAssembliesMethod = typeof (LinkContext).GetMethod ("GetReferencedAssemblies", BindingFlags.Public | BindingFlags.Instance);

		List<IMarkHandler> MarkHandlers {
			get {
				if (_markHandlers == null) {
					var pipeline = typeof (LinkContext).GetProperty ("Pipeline").GetGetMethod ().Invoke (Context, null);
					_markHandlers = (List<IMarkHandler>) pipeline.GetType ().GetProperty ("MarkHandlers").GetValue (pipeline);
				}
				return _markHandlers;
			}
		}

		protected override void Process ()
		{
			string tfmPaths;
			if (Context.TryGetCustomData ("XATargetFrameworkDirectories", out tfmPaths))
				Xamarin.Android.Tasks.MonoAndroidHelper.TargetFrameworkDirectories = tfmPaths.Split (new char [] { ';' });

			MarkHandlers.Add (new SubStepDispatcher (new List<ISubStep> () {
				new ApplyPreserveAttribute (),
				new PreserveExportedTypes ()
			}));

			MarkHandlers.Add (new MarkJavaObjects ());
			MarkHandlers.Add (new PreserveJavaExceptions ());
			MarkHandlers.Add (new PreserveApplications ());
			var cache = new TypeDefinitionCache ();
			MarkHandlers.Add (new PreserveRegistrations (cache));
			MarkHandlers.Add (new PreserveJavaInterfaces ());

			MarkHandlers.Add (new FixAbstractMethodsHandler (cache));

			// temporary workaround: this call forces illink to process all the assemblies
			if (getReferencedAssembliesMethod == null)
				throw new InvalidOperationException ($"Temporary linker workaround failed, {nameof (getReferencedAssembliesMethod)} is null.");

			foreach (var assembly in (IEnumerable<AssemblyDefinition>)getReferencedAssembliesMethod.Invoke (Context, null))
				Context.LogMessage ($"Reference assembly to process: {assembly}");

			string proguardPath;
			if (Context.TryGetCustomData ("ProguardConfiguration", out proguardPath))
				InsertAfter (new GenerateProguardConfiguration (proguardPath),  "CleanStep");

			string addKeepAlivesStep;
			if (Context.TryGetCustomData ("AddKeepAlivesStep", out addKeepAlivesStep) && bool.TryParse (addKeepAlivesStep, out var bv) && bv)
				InsertAfter (new AddKeepAlivesStep (cache), "CleanStep");

			string androidLinkResources;
			if (Context.TryGetCustomData ("AndroidLinkResources", out androidLinkResources) && bool.TryParse (androidLinkResources, out var linkResources) && linkResources) {
				InsertAfter (new RemoveResourceDesignerStep (),  "CleanStep");
				InsertAfter (new GetAssembliesStep (), "CleanStep");
			}
			InsertAfter (new StripEmbeddedLibraries (),  "CleanStep");
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
