using System;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Java.Interop.BootstrapTasks
{
	public class SetEnvironmentVariable : Task
	{
		[Required]
		public string Name { get; set; }

		[Required]
		public string Value { get; set; }

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (SetEnvironmentVariable)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Name)}: {Name}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Value)}: {Value}");

			Environment.SetEnvironmentVariable (Name, Value);

			return !Log.HasLoggedErrors;
		}
	}
}

