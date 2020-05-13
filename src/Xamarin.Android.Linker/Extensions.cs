using Mono.Linker;

namespace Xamarin.Android.Linker
{
	public static class Extensions
	{
		public static void LogMessage (this LinkContext context, string message)
		{
			context.LogMessage (MessageContainer.CreateInfoMessage (message));
		}
	}
}
