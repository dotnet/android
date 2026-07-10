using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.Collections.Generic;


namespace Java.Interop.BootstrapTasks
{
	public class ReplaceFileContents : Task
	{
		public ITaskItem TemplateFile { get; set; }
		public ITaskItem OutputFile { get; set; }

		public ITaskItem [] Replacements { get; set; }
		public override bool Execute ()
		{
			string text = File.ReadAllText (TemplateFile.ItemSpec);
			foreach (var replacement in Replacements)
			{
				text = text.Replace (replacement.ItemSpec, replacement.GetMetadata ("Replacement"));
			}
			File.WriteAllText (OutputFile.ItemSpec, text);
			return !Log.HasLoggedErrors;
		}
	}
}
