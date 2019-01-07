// Author: Jonathan Pobst <jpobst@xamarin.com>
// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Reflection;

using Java.Interop.Tools.JavaCallableWrappers;

namespace Xamarin.Android.Tasks
{
	public class CopyResource : Task
	{
		[Required]
		public string ResourceName { get; set; }

		[Required]
		public string OutputPath { get; set; }

		static readonly Assembly ExecutingAssembly = Assembly.GetExecutingAssembly ();
		static readonly Assembly JcwGenAssembly = typeof (JavaCallableWrapperGenerator).Assembly;

		public override bool Execute ()
		{
			using (var from = GetManifestResourceStream (ResourceName)) {
				if (from == null) {
					Log.LogCodedError ("XA0116", $"Unable to find `EmbeddedResource` of name `{ResourceName}`.");
					return false;
				}
				if (MonoAndroidHelper.CopyIfStreamChanged (from, OutputPath)) {
					Log.LogDebugMessage ($"Wrote resource {OutputPath}.");
				} else {
					Log.LogDebugMessage ($"Resource {OutputPath} is unchanged. Skipping.");
				}
			}

			return true;
		}

		Stream GetManifestResourceStream (string name)
		{
			return ExecutingAssembly.GetManifestResourceStream (name) ?? JcwGenAssembly.GetManifestResourceStream (name);
		}
	}
}

