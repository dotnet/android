namespace Xamarin.Debug.Session.Prep;

class AndroidNdk
{
	// We want the shell/batch scripts first, since they set up Python environment for the debugger
	static readonly string[] lldbNames = {
		"lldb.sh",
		"lldb",
		"lldb.cmd",
		"lldb.exe",
	};
}
