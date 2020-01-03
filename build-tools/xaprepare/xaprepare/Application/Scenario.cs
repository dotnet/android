using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	abstract partial class Scenario : AppObject
	{
		public string Name        { get; }
		public string Description { get; }
		public string LogFilePath { get; protected set; }
		public List<Step> Steps   { get; } = new List<Step> ();

		protected Scenario (string name, string description, Context context)
		{
			if (String.IsNullOrEmpty (name))
				throw new ArgumentException ("must not be null or empty", nameof (name));
			if (String.IsNullOrEmpty (description))
			    throw new ArgumentException ("must not be null or empty", nameof (description));
			Name = name;
			Description = description;
		}

		public async Task Run (Context context, Log log = null)
		{
			Log = log;
			foreach (Step step in Steps) {
				context.Banner (step.Description ?? step.GetType ().FullName);

				bool success;
				Exception stepEx = null;
				try {
					success = await step.Run (context);
				} catch (Exception ex) {
					stepEx = ex;
					success = false;
				}

				if (success)
					continue;

				string message = $"Step {step.FailedStep ?? step} failed";
				if (stepEx != null)
					throw new InvalidOperationException ($"{message}: {stepEx.Message}", stepEx);
				else
					throw new InvalidOperationException (message);
			}
		}

		public void Init (Context context)
		{
			AddStartSteps (context);
			AddSteps (context);
			AddEndSteps (context);
		}

		protected virtual void AddStartSteps (Context context)
		{
			// These are steps that have to be executed by all the scenarios
			Steps.Add (new Step_DownloadNuGet ());
			Steps.Add (new Step_InstallPackagesLocally ());
		}

		protected virtual void AddEndSteps (Context context)
		{}

		protected virtual void AddSteps (Context context)
		{}

		protected void AddSimpleStep (string description, Func<Context, bool> runner)
		{
			Steps.Add (new SimpleActionStep (description, runner));
		}
	}
}
