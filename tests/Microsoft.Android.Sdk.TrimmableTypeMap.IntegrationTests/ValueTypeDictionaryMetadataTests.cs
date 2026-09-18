using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Android.Runtime;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.IntegrationTests;

public class ValueTypeDictionaryMetadataTests
{
	static readonly (Type Type, string Name) [] ValueTypes = [
		(typeof (bool), "Boolean"),
		(typeof (byte), "Byte"),
		(typeof (sbyte), "SByte"),
		(typeof (char), "Char"),
		(typeof (short), "Int16"),
		(typeof (int), "Int32"),
		(typeof (long), "Int64"),
		(typeof (float), "Single"),
		(typeof (double), "Double"),
		(typeof (bool?), "NullableBoolean"),
		(typeof (byte?), "NullableByte"),
		(typeof (sbyte?), "NullableSByte"),
		(typeof (char?), "NullableChar"),
		(typeof (short?), "NullableInt16"),
		(typeof (int?), "NullableInt32"),
		(typeof (long?), "NullableInt64"),
		(typeof (float?), "NullableSingle"),
		(typeof (double?), "NullableDouble"),
	];

	[Fact]
	public void ValueTypeDictionaryConvertersCoverFullPrimitiveProduct ()
	{
		var assembly = typeof (Java.Lang.Object).Assembly;
		var valueTypeSet = ValueTypes.Select (value => value.Type).ToHashSet ();
		var entries = assembly.GetCustomAttributesData ()
			.Where (IsJavaDictionaryTypeMapAttribute)
			.Select (CreateEntry)
			.Where (entry => IsValueTypeDictionaryTarget (entry.TrimTarget, valueTypeSet))
			.ToList ();

		Assert.Equal (648, entries.Count);
		Assert.Equal (324, entries.Select (entry => entry.ConverterType).Distinct ().Count ());

		foreach (var key in ValueTypes) {
			foreach (var value in ValueTypes) {
				var pair = $"{key.Name}/{value.Name}";
				var interfaceType = typeof (IDictionary<,>).MakeGenericType (key.Type, value.Type);
				var concreteType = typeof (JavaDictionary<,>).MakeGenericType (key.Type, value.Type);
				var interfaceEntry = GetSingleEntry (entries, interfaceType, pair);
				var concreteEntry = GetSingleEntry (entries, concreteType, pair);

				Assert.True (
					interfaceEntry.Key == interfaceType.ToString (),
					$"{pair}: interface key '{interfaceEntry.Key}' does not match trim target '{interfaceType}'.");
				Assert.True (
					concreteEntry.Key == concreteType.ToString (),
					$"{pair}: concrete key '{concreteEntry.Key}' does not match trim target '{concreteType}'.");
				Assert.True (
					interfaceEntry.ConverterType == concreteEntry.ConverterType,
					$"{pair}: keys '{interfaceEntry.Key}' and '{concreteEntry.Key}' use different converters.");

				var converterType = interfaceEntry.ConverterType;
				var expectedSuffix = $"__{key.Name}{value.Name}JavaDictionaryConverter";
				Assert.True (
					converterType.FullName?.EndsWith (expectedSuffix, StringComparison.Ordinal) == true,
					$"{pair}: converter '{converterType.FullName}' for key '{interfaceEntry.Key}' does not end with '{expectedSuffix}'.");
				Assert.True (
					converterType.GetCustomAttributesData ().Any (attribute => attribute.AttributeType == converterType),
					$"{pair}: converter '{converterType}' for key '{interfaceEntry.Key}' is missing its self-applied attribute.");

				var createInstance = converterType.GetMethod (
					"CreateInstance",
					BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
					binder: null,
					types: [typeof (IntPtr), typeof (JniHandleOwnership)],
					modifiers: null);
				Assert.True (createInstance != null, $"{pair}: converter '{converterType}' does not declare CreateInstance(IntPtr, JniHandleOwnership).");
				if (createInstance == null) {
					continue;
				}

				Assert.True (
					createInstance.ReturnType == typeof (IDictionary),
					$"{pair}: converter '{converterType}' CreateInstance returns '{createInstance.ReturnType}' instead of non-generic IDictionary.");
				Assert.True (createInstance.IsVirtual, $"{pair}: converter '{converterType}' CreateInstance method is not virtual.");
				Assert.True (
					createInstance != createInstance.GetBaseDefinition (),
					$"{pair}: converter '{converterType}' CreateInstance method does not override the converter base method.");
			}
		}
	}

	static bool IsJavaDictionaryTypeMapAttribute (CustomAttributeData attribute)
	{
		var attributeType = attribute.AttributeType;
		return attributeType.IsGenericType &&
			attributeType.GetGenericTypeDefinition ().FullName == "System.Runtime.InteropServices.TypeMapAttribute`1" &&
			attributeType.GenericTypeArguments.Length == 1 &&
			attributeType.GenericTypeArguments [0] == typeof (JavaDictionary);
	}

	static TypeMapEntry CreateEntry (CustomAttributeData attribute)
	{
		Assert.Equal (3, attribute.ConstructorArguments.Count);
		var key = Assert.IsType<string> (attribute.ConstructorArguments [0].Value);
		var converterType = Assert.IsAssignableFrom<Type> (attribute.ConstructorArguments [1].Value);
		var trimTarget = Assert.IsAssignableFrom<Type> (attribute.ConstructorArguments [2].Value);
		return new TypeMapEntry (key, converterType, trimTarget);
	}

	static bool IsValueTypeDictionaryTarget (Type targetType, HashSet<Type> valueTypes)
	{
		if (!targetType.IsGenericType) {
			return false;
		}

		var definition = targetType.GetGenericTypeDefinition ();
		if (definition != typeof (IDictionary<,>) && definition != typeof (JavaDictionary<,>)) {
			return false;
		}

		var arguments = targetType.GetGenericArguments ();
		return arguments.Length == 2 && valueTypes.Contains (arguments [0]) && valueTypes.Contains (arguments [1]);
	}

	static TypeMapEntry GetSingleEntry (List<TypeMapEntry> entries, Type trimTarget, string pair)
	{
		var matches = entries.Where (entry => entry.TrimTarget == trimTarget).ToList ();
		Assert.True (
			matches.Count == 1,
			$"{pair}: expected exactly one TypeMapAttribute entry for '{trimTarget}', found {matches.Count}. Keys: {string.Join (", ", matches.Select (entry => entry.Key))}");
		return matches [0];
	}

	sealed record TypeMapEntry (string Key, Type ConverterType, Type TrimTarget);
}
