using System.Threading;

namespace Mono.Android_Test.Library
{
	public static class TrimmableManifestActivityState
	{
		public static int OnCreateCount;
		public static readonly ManualResetEventSlim OnCreateCompleted = new ();
	}
}
