#nullable enable
using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Represents the state of marshal methods for a specific compilation unit.
	/// This class encapsulates the collection of marshal methods that have been
	/// classified and are ready for native code generation.
	/// </summary>
	sealed class MarshalMethodsState
	{
		/// <summary>
		/// Gets the dictionary of marshal methods organized by their unique keys.
		/// Each key typically represents a type-method combination, and the value
		/// is a list of marshal method entries for that key (allowing for method overloads).
		/// </summary>
		public IDictionary<string, IList<MarshalMethodEntry>> MarshalMethods { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="MarshalMethodsState"/> class.
		/// </summary>
		/// <param name="marshalMethods">
		/// The dictionary of marshal methods organized by their unique keys.
		/// Cannot be null.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="marshalMethods"/> is null.
		/// </exception>
		/// <remarks>
		/// This constructor also dumps the marshal methods to the console for debugging purposes
		/// when diagnostic logging is enabled.
		/// </remarks>
		public MarshalMethodsState (IDictionary<string, IList<MarshalMethodEntry>> marshalMethods)
		{
			// Debug output for troubleshooting marshal method classification
			MonoAndroidHelper.DumpMarshalMethodsToConsole ("Classified ethods in MarshalMethodsState ctor", marshalMethods);

			MarshalMethods = marshalMethods ?? throw new ArgumentNullException (nameof (marshalMethods));
		}
	}
}
