namespace Xamarin.Android.Tasks
{
	class ApplicationConfigTaskState
	{
		public const string RegisterTaskObjectKey = "Xamarin.Android.Tasks.ApplicationConfigTaskState";

		public bool JniAddNativeMethodRegistrationAttributePresent { get; set; } = false;
	}
}
