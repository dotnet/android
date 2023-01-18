namespace Xamarin.Android.Utilities;

interface IProcessOutputLogger
{
	void WriteStdout (string text);
	void WriteStderr (string text);
}
