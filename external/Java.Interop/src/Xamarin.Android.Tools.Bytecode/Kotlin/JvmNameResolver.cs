using System;
using System.Collections.Generic;
using org.jetbrains.kotlin.metadata.jvm;

namespace Xamarin.Android.Tools.Bytecode
{
	// https://github.com/JetBrains/kotlin/blob/master/core/metadata.jvm/src/org/jetbrains/kotlin/metadata/jvm/deserialization/JvmNameResolver.kt
	class JvmNameResolver
	{
		readonly List<StringTableTypes.Record> records = new List<StringTableTypes.Record> ();
		readonly List<string> strings;

		public JvmNameResolver (StringTableTypes table, List<string> strings)
		{
			foreach (var t in table.Records)
				for (var i = 0; i < t.Range; i++)
					records.Add (t);

			this.strings = strings;
		}

		public string GetString (int index)
		{
			var record = records [index];

			string val;

			// Get string from:
			// - Embedded in string record table
			// - Predefined string
			// - Constant pool
			if (!string.IsNullOrEmpty (record.String))
				val = record.String;
			else if (record.PredefinedIndex > 0)
				val = predefined_strings [record.PredefinedIndex];
			else
				val = strings [index];

			if (record.SubstringIndexs?.Length >= 2) {
				var begin = record.SubstringIndexs [0];
				var end = record.SubstringIndexs [1];

				if (begin > 0 && begin <= end && end <= val.Length)
					val = val.Substring (begin, end);
			}

			if (record.ReplaceChars?.Length >= 2) {
				var from = (char) record.ReplaceChars [0];
				var to = (char) record.ReplaceChars [1];

				val = val.Replace (from, to);
			}

			if (record.operation == StringTableTypes.Record.Operation.InternalToClassId)
				val = val.Replace ('$', '.');
			else if (record.operation == StringTableTypes.Record.Operation.DescToClassId) {
				if (val.Length >= 2)
					val = val.Substring (1, val.Length - 1);

				val = val.Replace ('$', '.');
			}

			return val;
		}

		static readonly string [] predefined_strings = new string [] {
			"kotlin/Any",
			"kotlin/Nothing",
			"kotlin/Unit",
			"kotlin/Throwable",
			"kotlin/Number",

			"kotlin/Byte", "kotlin/Double", "kotlin/Float", "kotlin/Int",
			"kotlin/Long", "kotlin/Short", "kotlin/Boolean", "kotlin/Char",

			"kotlin/CharSequence",
			"kotlin/String",
			"kotlin/Comparable",
			"kotlin/Enum",

			"kotlin/Array",
			"kotlin/ByteArray", "kotlin/DoubleArray", "kotlin/FloatArray", "kotlin/IntArray",
			"kotlin/LongArray", "kotlin/ShortArray", "kotlin/BooleanArray", "kotlin/CharArray",

			"kotlin/Cloneable",
			"kotlin/Annotation",

			"kotlin/collections/Iterable", "kotlin/collections/MutableIterable",
			"kotlin/collections/Collection", "kotlin/collections/MutableCollection",
			"kotlin/collections/List", "kotlin/collections/MutableList",
			"kotlin/collections/Set", "kotlin/collections/MutableSet",
			"kotlin/collections/Map", "kotlin/collections/MutableMap",
			"kotlin/collections/Map.Entry", "kotlin/collections/MutableMap.MutableEntry",

			"kotlin/collections/Iterator", "kotlin/collections/MutableIterator",
			"kotlin/collections/ListIterator", "kotlin/collections/MutableListIterator"
		};
	}
}
