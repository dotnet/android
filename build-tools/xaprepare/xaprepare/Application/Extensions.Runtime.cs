using System;
using System.Diagnostics.CodeAnalysis;

namespace Xamarin.Android.Prepare
{
	static class Runtime_Extensions
	{
		[return: MaybeNull]
		public static T As <T> (this Runtime runtime) where T: Runtime
		{
			return runtime as T;
		}
	}
}
