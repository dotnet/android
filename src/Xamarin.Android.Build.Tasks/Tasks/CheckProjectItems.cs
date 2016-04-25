using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;

namespace Xamarin.Android.Tasks
{
	public class CheckProjectItems : Task
	{
		public bool IsApplication { get; set; }
		public ITaskItem [] EmbeddedNativeLibraries { get; set; }
		public ITaskItem [] NativeLibraries { get; set; }
		public ITaskItem [] JavaLibraries { get; set; }
		public ITaskItem [] JavaSourceFiles { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("CheckProjectItems Task");
			Log.LogDebugTaskItems ("  NativeLibraries:", NativeLibraries);
			Log.LogDebugTaskItems ("  JavaLibraries:", JavaLibraries);
			Log.LogDebugTaskItems ("  JavaSourceFiles:", JavaSourceFiles);
			if (IsApplication && EmbeddedNativeLibraries != null && EmbeddedNativeLibraries.Length > 0) {
				foreach (ITaskItem lib in EmbeddedNativeLibraries) {
					Log.LogError (
							subcategory:      string.Empty,
							errorCode:        "XA0100",
							helpKeyword:      string.Empty,
							file:             lib.ItemSpec,
							lineNumber:       0,
							columnNumber:     0,
							endLineNumber:    0,
							endColumnNumber:  0,
							message:          "EmbeddedNativeLibrary '{0}' is invalid in Android Application project. Please use AndroidNativeLibrary instead.",
							messageArgs:      new [] {lib.ItemSpec}
					);
				}
				return false;
			}
			return true;
		}
	}
}

