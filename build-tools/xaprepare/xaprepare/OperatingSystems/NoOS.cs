using System;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	class NoOS : OS
	{
		public override string Type => throw new NotImplementedException ();
		public override List<Program> Dependencies => throw new NotImplementedException ();
		public override string HomeDirectory => throw new NotImplementedException ();
		public override StringComparison DefaultStringComparison => throw new NotImplementedException ();
		public override StringComparer DefaultStringComparer => throw new NotImplementedException ();
		public override bool IsWindows => false;
		public override bool IsUnix => false;
		protected override List<string> ExecutableExtensions => throw new NotImplementedException ();

		public NoOS (Context context)
			: base (context)
		{}

		public override string GetManagedProgramRunner (string programPath)
		{
			throw new NotImplementedException ();
		}

		protected override string AssertIsExecutable (string fullPath)
		{
			throw new NotImplementedException ();
		}

		protected override void InitializeDependencies ()
		{
			throw new NotImplementedException ();
		}
	}
}
