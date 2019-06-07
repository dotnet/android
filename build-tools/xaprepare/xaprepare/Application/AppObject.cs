namespace Xamarin.Android.Prepare
{
	class AppObject
	{
		Log log;

		public Log Log {
			get => log ?? Log.Instance;
			protected set => log = value;
		}

		protected AppObject (Log log = null)
		{
			this.log = log;
		}
	}
}
