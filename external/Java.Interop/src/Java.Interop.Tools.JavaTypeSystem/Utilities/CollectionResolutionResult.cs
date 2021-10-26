using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Java.Interop.Tools.JavaTypeSystem.Models;

namespace Java.Interop.Tools.JavaTypeSystem
{
	// This class represents the "cycles" it took to resolve the collection.
	// Example:
	// - Cycle 1 removed 'com.example.MyType' because 'android.util.List' was missing
	// - Cycle 2 removed 'com.example.MyDerivedType' because 'com.example.MyType' is now missing
	// This distinction can be interesting, because Cycle 1 removals are often due to missing
	// dependencies, whereas the remaining cycles are just the internal fallout from Cycle 1.
	public class CollectionResolutionResults : Collection<CollectionResolutionResult>
	{
	}

	public class CollectionResolutionResult
	{
		public Collection<JavaUnresolvableModel> Unresolvables { get; }

		public CollectionResolutionResult (Collection<JavaUnresolvableModel> unresolvables) =>
			Unresolvables = unresolvables;
	}
}
