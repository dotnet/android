#nullable enable
using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xamarin.Android.Build.Tests;

namespace Microsoft.Android.TrimmingTests;

[TestClass]
public class TrimmingExpectationsTests : BaseTest
{
	[TestMethod]
	public void MethodSignaturesDistinguishGenericArity ()
	{
		var assembly = AssemblyMetadata.Read (typeof (GenericMethodOverloads).Assembly.Location);
		var typeName = typeof (GenericMethodOverloads).FullName ?? throw new InvalidOperationException ("The fixture type has no full name.");
		var type = assembly.Types [typeName];
		var methods = type.Members.OfType<MethodMetadata> ().Where (method => method.Name == nameof (GenericMethodOverloads.Method)).ToArray ();

		CollectionAssert.AreEquivalent (new [] { "Method()", "Method``1()" }, methods.Select (method => method.Signature).ToArray ());
		CollectionAssert.AreEquivalent (new [] { "Method()", "Method``1()" }, methods.Select (method => method.CanonicalSignature).ToArray ());
	}
}

static class GenericMethodOverloads
{
	public static void Method ()
	{
	}

	public static void Method<T> ()
	{
	}
}
