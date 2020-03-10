using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using IOFile = System.IO.File;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public sealed class GitDiff : Git
	{
		protected   override    bool        LogTaskMessages     {
			get { return false; }
		}

		protected   override    bool        PreserveOutput     {
			get { return false; }
		}

		protected override string GenerateCommandLineCommands ()
		{
			return "diff " + Arguments;
		}
	}
}
