namespace Xamarin.Android.Prepare
{
	static class Runtime_Extensions
	{
		public static T As <T> (this Runtime runtime) where T: Runtime
		{
			return runtime as T;
		}
	}
}
