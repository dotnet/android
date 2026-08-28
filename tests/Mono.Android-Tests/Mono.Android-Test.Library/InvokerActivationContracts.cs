using System;

using Android.Runtime;

using Java.Interop;

namespace Mono.Android_Test.Library
{
	[Register ("java/util/RandomAccess", "", "Java.InteropTests.ExternalRandomAccessInvoker, Mono.Android.NET-Tests")]
	public interface IExternalRandomAccess : IJavaPeerable, IDisposable
	{
	}
}
