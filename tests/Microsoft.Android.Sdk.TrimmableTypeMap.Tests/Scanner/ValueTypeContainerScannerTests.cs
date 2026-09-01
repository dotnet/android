using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;

using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class ValueTypeContainerScannerTests : FixtureTestBase
{
	[Fact]
	public void Scan_CollectsOnlyClosedValueTypeContainerShapes ()
	{
		using var peReader = new PEReader (File.OpenRead (TestFixtureAssemblyPath));
		var fixtureDirectory = Path.GetDirectoryName (TestFixtureAssemblyPath);
		Assert.NotNull (fixtureDirectory);
		var attributeFixturePath = Path.Combine (fixtureDirectory, "TestAttributeFixtures.dll");
		using var attributePeReader = new PEReader (File.OpenRead (attributeFixturePath));
		var roots = ValueTypeContainerScanner.Scan (
			[
				new AssemblyInput ("TestFixtures", TestFixtureAssemblyPath, peReader),
				new AssemblyInput ("TestAttributeFixtures", attributeFixturePath, attributePeReader),
			],
			new HashSet<string> (StringComparer.Ordinal));
		var fixtureRoots = roots
			.Where (root => root.TypeArguments.Any (ContainsFixtureValueType))
			.Select (root => root.DisplayName)
			.ToList ();

		Assert.Equal (new [] {
			"Collection<ValueTypeContainerFixtures.UserState>",
			"Dictionary<System.String,ValueTypeContainerFixtures.UserState>",
			"Dictionary<ValueTypeContainerFixtures.GenericArrayValue[],System.Int32>",
			"Dictionary<ValueTypeContainerFixtures.UserState,ValueTypeContainerFixtures.UserValue>",
			"Dictionary<ValueTypeContainerFixtures.UserValue,System.String>",
			"Dictionary<ValueTypeContainerFixtures.UserValue,ValueTypeContainerFixtures.UserState>",
			"Dictionary<ValueTypeContainerFixtures.UserValue[,],System.Int32>",
			"List<System.Nullable`1<ValueTypeContainerFixtures.UserState>>",
			"List<ValueTypeContainerFixtures.ChainedMethodValue>",
			"List<ValueTypeContainerFixtures.ExternalBodyValue>",
			"List<ValueTypeContainerFixtures.GenericBodyValue>",
			"List<ValueTypeContainerFixtures.GenericMethodValue>",
			"List<ValueTypeContainerFixtures.GenericTypeBodyValue>",
			"List<ValueTypeContainerFixtures.GenericTypeValue>",
			"List<ValueTypeContainerFixtures.LocalOnlyValue>",
			"List<ValueTypeContainerFixtures.StaticBodyValue>",
			"List<ValueTypeContainerFixtures.UserValue>",
		}, fixtureRoots);
		Assert.Contains (roots, root => root.DisplayName == "List<System.Nullable`1<System.Int32>>");
		Assert.Contains (roots, root => root.DisplayName == "List<System.UIntPtr>");
		Assert.DoesNotContain (roots, root => root.DisplayName == "List<System.String>");
		Assert.DoesNotContain (roots, root => root.DisplayName == "List<ValueTypeContainerFixtures.UserValue[,]>");
		Assert.DoesNotContain (roots, root => root.TypeArguments.Any (ContainsOpenType));
	}

	static bool ContainsFixtureValueType (TypeRefData type)
	{
		if (type.AssemblyName == "TestFixtures" &&
				type.ManagedTypeName.StartsWith ("ValueTypeContainerFixtures.", StringComparison.Ordinal)) {
			return true;
		}
		return type.GenericArguments.Any (ContainsFixtureValueType);
	}

	static bool ContainsOpenType (TypeRefData type)
	{
		if (type.ManagedTypeName.StartsWith ("!", StringComparison.Ordinal)) {
			return true;
		}
		return type.GenericArguments.Any (ContainsOpenType);
	}
}
