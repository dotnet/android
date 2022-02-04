using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Tasks
{
	abstract class ArmNativeAssemblyGenerator : NativeAssemblyGenerator
	{
		protected abstract string ArchName { get; }

		protected ArmNativeAssemblyGenerator (StreamWriter output, string fileName)
			: base (output, fileName)
		{}

		public override void WriteFileTop ()
		{
			base.WriteFileTop ();
			WriteDirective (".arch", ArchName);
		}
	}
}
