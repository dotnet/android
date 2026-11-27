#nullable enable
using System;
using System.Linq;
using NUnit.Framework;
using Xamarin.Android.Build.Tests;

namespace Microsoft.Android.TrimmingTests;

[TestFixture]
public class TrimmingExpectationsTests : BaseTest
{
	[Test]
	public void MethodSignaturesDistinguishGenericArity ()
	{
		var assembly = AssemblyMetadata.Read (typeof (GenericMethodOverloads).Assembly.Location);
		var typeName = typeof (GenericMethodOverloads).FullName ?? throw new InvalidOperationException ("The fixture type has no full name.");
		var type = assembly.Types [typeName];
		var methods = type.Members.OfType<MethodMetadata> ().Where (method => method.Name == nameof (GenericMethodOverloads.Method)).ToArray ();

		Assert.That (methods.Select (method => method.Signature), Is.EquivalentTo (new [] { "Method()", "Method``1()" }));
		Assert.That (methods.Select (method => method.CanonicalSignature), Is.EquivalentTo (new [] { "Method()", "Method``1()" }));
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
