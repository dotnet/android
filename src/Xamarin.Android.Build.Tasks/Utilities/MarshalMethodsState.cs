#if ENABLE_MARSHAL_METHODS
using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tasks
{
	sealed class MarshalMethodsState
	{
		public IDictionary<string, MarshalMethodEntry> MarshalMethods        { get; }

		public MarshalMethodsState (IDictionary<string, MarshalMethodEntry> marshalMethods)
		{
			MarshalMethods = marshalMethods ?? throw new ArgumentNullException (nameof (marshalMethods));
		}
	}
}
#endif
