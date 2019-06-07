using System;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	partial class LinuxUbuntuCommon : LinuxDebianCommon
	{
		const string BinfmtWarningUbuntu = @"
Since you are running Ubuntu then you can disable just the CLI (Mono) and Wine interpreters
by issuing the following commands as root (or by prepending the commands below with `sudo`):

   update-binfmts --disable cli
   update-binfmts --disable wine

";

		public override void ShowFinalNotices ()
		{
			base.ShowFinalNotices ();
			if (!WarnBinFmt)
				return;

			Log.WarningLine (BinfmtWarningUbuntu, ConsoleColor.White, showSeverity: false);
		}
	}
}
