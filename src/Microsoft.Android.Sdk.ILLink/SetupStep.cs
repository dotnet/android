using System;
using System.Collections.Generic;
using System.Reflection;
using Java.Interop.Tools.Cecil;
using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;
using MonoDroid.Tuner;

namespace Microsoft.Android.Sdk.ILLink
{
	class SetupStep : BaseStep
	{
		protected override void Process ()
		{
			if (Context.TryGetCustomData ("XATargetFrameworkDirectories", out string tfmPaths))
				Xamarin.Android.Tasks.MonoAndroidHelper.TargetFrameworkDirectories = tfmPaths.Split (new char [] { ';' });
		}
	}
}
