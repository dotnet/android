using System;

namespace Xamarin.Android.Tasks
{
	class InvalidActivityNameException : Exception
	{
		public InvalidActivityNameException (string message) : base (message)
		{
		}
	}
}
