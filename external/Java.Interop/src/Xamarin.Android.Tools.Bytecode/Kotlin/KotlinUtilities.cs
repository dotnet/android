using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.Android.Tools.Bytecode
{
	public static class KotlinUtilities
	{
		[return: NotNullIfNotNull (nameof (value))]
		public static string? Capitalize (this string? value)
		{
			if (string.IsNullOrWhiteSpace (value))
				return value;

			if (value.Length < 1)
				return value;

			return char.ToUpperInvariant (value [0]) + value.Substring (1);
		}

		public static string ConvertKotlinTypeSignature (KotlinType? type, KotlinFile? metadata = null, bool convertUnsignedToPrimitive = true)
		{
			if (type is null)
				return string.Empty;

			var class_name = type.ClassName;

			if (string.IsNullOrWhiteSpace (class_name)) {
				if (metadata is KotlinClass klass) {

					var tp = klass.TypeParameters?.FirstOrDefault (t => t.Id == type.TypeParameter);

					if (tp?.UpperBounds?.FirstOrDefault ()?.ClassName != null)
						return ConvertKotlinClassToJava (tp.UpperBounds.FirstOrDefault ()?.ClassName);
				}
				
				return "Ljava/lang/Object;";
			}

			var result = ConvertKotlinClassToJava (class_name, convertUnsignedToPrimitive);

			if (result == "[")
				result += ConvertKotlinTypeSignature (type.Arguments?.FirstOrDefault ()?.Type, null, convertUnsignedToPrimitive);

			return result;
		}

		public static string ConvertKotlinClassToJava (string? className, bool convertUnsignedToPrimitive = true)
		{
			if (className == null || string.IsNullOrWhiteSpace (className))
				return string.Empty;

			className = className.Replace ('.', '$');

			if (type_map.TryGetValue (className.TrimEnd (';'), out var result))
				return result;

			if (convertUnsignedToPrimitive && unsigned_type_map.TryGetValue (className.TrimEnd (';'), out var result2))
				return result2;

			return "L" + className;
		}

		public static string GetSignature (this List<KotlinValueParameter> parameters)
		{
			return string.Join (string.Empty, parameters.Select (p => ConvertKotlinTypeSignature (p.Type)));
		}

		public static ParameterInfo[] GetFilteredParameters (this MethodInfo method)
		{
			// Kotlin adds this to some constructors but I cannot tell which ones,
			// so we'll just ignore them if we see them on the Java side
			return method.GetParameters ().Where (p => p.Type.BinaryName != "Lkotlin/jvm/internal/DefaultConstructorMarker;" && !p.Name.StartsWith ("$", StringComparison.Ordinal)).ToArray ();
		}

		public static string GetMethodNameWithoutSuffix (this MethodInfo method)
		{
			// Kotlin will rename some of its constructs to hide them from the Java runtime
			// These take the form of thing like:
			// - add-impl
			// - add-H3FcsT8
			// We strip them for trying to match up the metadata to the MethodInfo
			var index = method.Name.IndexOfAny (new [] { '-', '$' });

			return index >= 0 ? method.Name.Substring (0, index) : method.Name;
		}

		public static bool IsDefaultConstructorMarker (this MethodInfo method)
		{
			// A default constructor is synthetic and always has an int and a
			// DefaultConstructorMarker as its final 2 parameters.
			if (method.Name != "<init>")
				return false;

			if (!method.AccessFlags.HasFlag (MethodAccessFlags.Synthetic))
				return false;

			var parameters = method.GetParameters ();

			if (parameters.Length < 2)
				return false;

			// Parameter list ends with `int, DefaultConstructorMarker`.
			return parameters [parameters.Length - 2].Type.TypeSignature == "I" &&
				parameters [parameters.Length - 1].Type.TypeSignature == "Lkotlin/jvm/internal/DefaultConstructorMarker;";
		}

		// Sometimes the Kotlin provided JvmSignature is null (or unhelpful), so we need to construct one ourselves
		public static string ConstructJvmSignature (this KotlinFunction function)
		{
			// The receiver type (if specified) is a "hidden" parameter passed at the beginning
			// of the Java parameter list, so we include it so the Signature/Descriptors match.
			return $"({function.ReceiverType?.GetSignature (false)}{string.Concat (function.ValueParameters?.Select (p => p.Type?.GetSignature (false)) ?? Enumerable.Empty<string> ())}){function.ReturnType?.GetSignature (false)}";
		}

		internal static List<TResult>? ToList<TSource, TResult> (this IEnumerable<TSource>? self, JvmNameResolver resolver, Func<TSource, JvmNameResolver, TResult?> creator)
			where TResult: class
		{
			if (self == null)
				return null;
			return self.Select (v => creator (v, resolver)!)
				.Where (v => v != null)
				.ToList ();
		}

		public static bool IsPubliclyVisible (this ClassAccessFlags flags) => flags.HasFlag (ClassAccessFlags.Public) || flags.HasFlag (ClassAccessFlags.Protected);

		public static bool IsPubliclyVisible (this KotlinClassVisibility flags) => flags == KotlinClassVisibility.Public || flags == KotlinClassVisibility.Protected;

		public static bool IsPubliclyVisible (this KotlinFunctionFlags flags) => flags.HasFlag (KotlinFunctionFlags.Public) || flags.HasFlag (KotlinFunctionFlags.Protected);

		public static bool IsPubliclyVisible (this KotlinConstructorFlags flags) => flags.HasFlag (KotlinConstructorFlags.Public) || flags.HasFlag (KotlinConstructorFlags.Protected);

		public static bool IsPubliclyVisible (this KotlinPropertyFlags flags) => flags.HasFlag (KotlinPropertyFlags.Public) || flags.HasFlag (KotlinPropertyFlags.Protected);

		public static bool IsUnnamedParameter (this ParameterInfo parameter) => parameter.Name.Length > 1 && parameter.Name.StartsWith ("p", StringComparison.Ordinal) && int.TryParse (parameter.Name.Substring (1), out var _);

		public static bool IsCompilerNamed (this ParameterInfo parameter) => parameter.Name.Length > 0 && parameter.Name.StartsWith ("$", StringComparison.Ordinal);

		public static bool IsUnnamedParameter (this KotlinValueParameter parameter) => parameter.Name?.Length > 1 &&
			parameter.Name.StartsWith ("p", StringComparison.Ordinal) &&
			int.TryParse (parameter.Name.Substring (1), out var _);

		static Dictionary<string, string> unsigned_type_map = new Dictionary<string, string> {
			{ "kotlin/UInt", "I" },
			{ "kotlin/ULong", "J" },
			{ "kotlin/UShort", "S" },
			{ "kotlin/UByte", "B" },
			{ "kotlin/UIntArray", "[I" },
			{ "kotlin/ULongArray", "[J" },
			{ "kotlin/UShortArray", "[S" },
			{ "kotlin/UByteArray", "[B" },
		};

		static Dictionary<string, string> type_map = new Dictionary<string, string> {
			{ "kotlin/Int", "I" },
			{ "kotlin/Double", "D" },
			{ "kotlin/Char", "C" },
			{ "kotlin/Long", "J" },
			{ "kotlin/Float", "F" },
			{ "kotlin/Short", "S" },
			{ "kotlin/Byte", "B" },
			{ "kotlin/Boolean", "Z" },
			{ "kotlin/Unit", "V" },

			{ "kotlin/Array", "[" },
			{ "kotlin/IntArray", "[I" },
			{ "kotlin/DoubleArray", "[D" },
			{ "kotlin/CharArray", "[C" },
			{ "kotlin/LongArray", "[J" },
			{ "kotlin/FloatArray", "[F" },
			{ "kotlin/ShortArray", "[S" },
			{ "kotlin/ByteArray", "[B" },
			{ "kotlin/BooleanArray", "[Z" },

			{ "kotlin/Any", "Ljava/lang/Object;" },
			{ "kotlin/Nothing", "Ljava/lang/Void;" },
			{ "kotlin/Annotation", "Ljava/lang/annotation/Annotation;" },
			{ "kotlin/String", "Ljava/lang/String;" },
			{ "kotlin/CharSequence", "Ljava/lang/CharSequence;" },
			{ "kotlin/Throwable", "Ljava/lang/Throwable;" },
			{ "kotlin/Cloneable", "Ljava/lang/Cloneable;" },
			{ "kotlin/Number", "Ljava/lang/Number;" },
			{ "kotlin/Comparable", "Ljava/lang/Comparable;" },
			{ "kotlin/Enum", "Ljava/lang/Enum;" },

			{ "kotlin/collections/Iterator", "Ljava/util/Iterator;" },
			{ "kotlin/collections/MutableIterator", "Ljava/util/Iterator;" },
			{ "kotlin/collections/Collection", "Ljava/util/Collection;" },
			{ "kotlin/collections/MutableCollection", "Ljava/util/Collection;" },
			{ "kotlin/collections/List", "Ljava/util/List;" },
			{ "kotlin/collections/MutableList", "Ljava/util/List;" },
			{ "kotlin/collections/Set", "Ljava/util/Set;" },
			{ "kotlin/collections/MutableSet", "Ljava/util/Set;" },
			{ "kotlin/collections/Map", "Ljava/util/Map;" },
			{ "kotlin/collections/MutableMap", "Ljava/util/Map;" },
			{ "kotlin/collections/ListIterator", "Ljava/util/ListIterator;" },
			{ "kotlin/collections/MutableListIterator", "Ljava/util/ListIterator;" },

			{ "kotlin/collections/Iterable", "Ljava/lang/Iterable;" },
			{ "kotlin/collections/MutableIterable", "Ljava/lang/Iterable;" },
			{ "kotlin/collections/Map$Entry", "Ljava/util/Map$Entry;" },
			{ "kotlin/collections/MutableMap$MutableEntry", "Ljava/util/Map$Entry;" },

			{ "kotlin/Function0", "Lkotlin/jvm/functions/Function0;" },
			{ "kotlin/Function1", "Lkotlin/jvm/functions/Function1;" },
			{ "kotlin/Function2", "Lkotlin/jvm/functions/Function2;" },
			{ "kotlin/Function3", "Lkotlin/jvm/functions/Function3;" },
			{ "kotlin/Function4", "Lkotlin/jvm/functions/Function4;" },
			{ "kotlin/Function5", "Lkotlin/jvm/functions/Function5;" },
			{ "kotlin/Function6", "Lkotlin/jvm/functions/Function6;" },
			{ "kotlin/Function7", "Lkotlin/jvm/functions/Function7;" },
			{ "kotlin/Function8", "Lkotlin/jvm/functions/Function8;" },
			{ "kotlin/Function9", "Lkotlin/jvm/functions/Function9;" },
			{ "kotlin/Function10", "Lkotlin/jvm/functions/Function10;" },
			{ "kotlin/Function11", "Lkotlin/jvm/functions/Function11;" },
			{ "kotlin/Function12", "Lkotlin/jvm/functions/Function12;" },
			{ "kotlin/Function13", "Lkotlin/jvm/functions/Function13;" },
			{ "kotlin/Function14", "Lkotlin/jvm/functions/Function14;" },
			{ "kotlin/Function15", "Lkotlin/jvm/functions/Function15;" },
			{ "kotlin/Function16", "Lkotlin/jvm/functions/Function16;" },
			{ "kotlin/Function17", "Lkotlin/jvm/functions/Function17;" },
			{ "kotlin/Function18", "Lkotlin/jvm/functions/Function18;" },
			{ "kotlin/Function19", "Lkotlin/jvm/functions/Function19;" },
			{ "kotlin/Function20", "Lkotlin/jvm/functions/Function20;" },
			{ "kotlin/Function21", "Lkotlin/jvm/functions/Function21;" },
			{ "kotlin/Function22", "Lkotlin/jvm/functions/Function22;" },
		};
	}
}
