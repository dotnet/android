namespace Mono.Linker
{
	public static class Extensions
	{
		public static void LogMessage (this LinkContext context, string message)
		{
			context.LogMessage (MessageContainer.CreateInfoMessage (message));
		}
	}
}
