using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	abstract class Step : AppObject
	{
		List<Step> failureSteps;

		bool HasFailureSteps => failureSteps != null && failureSteps.Any (s => s != null);

		public string Description { get; }
		public Step FailedStep { get; private set; }
		public bool ExecutedFailureSteps { get; private set; }

		protected Step (string description)
		{
			if (String.IsNullOrEmpty (description))
				throw new ArgumentException ("must not be null or empty", nameof (description));
			Description = description;
		}

		public void AddFailureStep (Step step)
		{
			if (step == null)
				throw new ArgumentNullException (nameof (step));

			if (failureSteps == null)
				failureSteps = new List <Step> ();

			failureSteps.Add (step);
		}

		protected abstract Task<bool> Execute (Context context);

		public async Task<bool> Run (Context context)
		{
			FailedStep = null;
			bool success = await Execute (context);
			if (success)
				return true;

			if (!HasFailureSteps)
				return false;

			foreach (Step step in failureSteps) {
				ExecutedFailureSteps = true;
				context.Banner (step.Description);
				try {
					if (!await step.Run (context)) {
						FailedStep = step;
						return false;
					}
				} catch {
					FailedStep = step;
					throw;
				}
			}

			return true;
		}

		protected void TouchStampFile (string filePath)
		{
			File.WriteAllText (filePath, DateTime.UtcNow.ToString ());
		}
	}
}
