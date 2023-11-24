using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tasks
{
	sealed class MarshalMethodsState
	{
		public IDictionary<string, IList<MarshalMethodEntry>> MarshalMethods { get; }

		public MarshalMethodsState (IDictionary<string, IList<MarshalMethodEntry>> marshalMethods)
		{
			MonoAndroidHelper.DumpMarshalMethodsToConsole ("Classified ethods in MarshalMethodsState ctor", marshalMethods);

			MarshalMethods = marshalMethods ?? throw new ArgumentNullException (nameof (marshalMethods));
		}
	}
}
