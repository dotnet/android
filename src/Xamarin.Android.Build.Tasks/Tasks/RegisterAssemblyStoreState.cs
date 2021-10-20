using System;
using System.IO;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class RegisterAssemblyStoreState : AndroidTask
	{
		public const string RegisteredObjectKey = ".:RegisterAssemblyStoreState_Key:.";

		public override string TaskPrefix => "RASS";

		[Required]
		public bool UseAssemblyStore { get; set; }

		public override bool RunTask ()
		{
			BuildEngine4.RegisterTaskObjectAssemblyLocal (RegisteredObjectKey, UseAssemblyStore, RegisteredTaskObjectLifetime.Build);
			return !Log.HasLoggedErrors;
		}
	}
}
