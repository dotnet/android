using System;

namespace Xamarin.Android.Prepare
{
	class AppObject
	{
		public Log Log { get; } = Log.Instance;
		public Context Context => Context.Instance;

		public AppObject (Log? log = null)
		{
			if (log != null) {
				Log = log;
			}
		}

		protected string EnsurePropertyValue (string propertyName, string propertyValue)
		{
			if (propertyValue.Length > 0) {
				return propertyValue;
			}

			throw new InvalidOperationException ($"{propertyName} property must have a value");
		}

		protected string EnsureParameterValue (string parameterName, string parameterValue, bool trimWhitespace = true)
		{
			if (trimWhitespace) {
				parameterValue = parameterValue.Trim ();
			}

			if (parameterValue.Length > 0) {
				return parameterValue;
			}

			throw new ArgumentException (parameterName, "must not be an empty string");
		}

		protected int EnsurePositivePropertyValue (string propertyName, int propertyValue)
		{
			if (propertyValue > 0) {
				return propertyValue;
			}

			throw new ArgumentException (propertyName, "must be a positive integer");
		}
	}
}
