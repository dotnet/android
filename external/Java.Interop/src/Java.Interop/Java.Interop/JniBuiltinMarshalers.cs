using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Java.Interop {

	partial class JniRuntime {

		static readonly KeyValuePair<Type, JniTypeSignature>[] JniBuiltinTypeNameMappings = new []{
			new KeyValuePair<Type, JniTypeSignature>(typeof (string),    new JniTypeSignature ("java/lang/String")),

			new KeyValuePair<Type, JniTypeSignature>(typeof (void),      new JniTypeSignature ("V", arrayRank: 0, keyword: true)),
			new KeyValuePair<Type, JniTypeSignature>(typeof (void),      new JniTypeSignature ("java/lang/Void")),

			new KeyValuePair<Type, JniTypeSignature>(typeof (Boolean),     new JniTypeSignature ("Z", 0, keyword: true)),
			new KeyValuePair<Type, JniTypeSignature>(typeof (Boolean),     new JniTypeSignature ("java/lang/Boolean")),
			new KeyValuePair<Type, JniTypeSignature>(typeof (SByte),     new JniTypeSignature ("B", 0, keyword: true)),
			new KeyValuePair<Type, JniTypeSignature>(typeof (SByte),     new JniTypeSignature ("java/lang/Byte")),
			new KeyValuePair<Type, JniTypeSignature>(typeof (Char),     new JniTypeSignature ("C", 0, keyword: true)),
			new KeyValuePair<Type, JniTypeSignature>(typeof (Char),     new JniTypeSignature ("java/lang/Character")),
			new KeyValuePair<Type, JniTypeSignature>(typeof (Int16),     new JniTypeSignature ("S", 0, keyword: true)),
			new KeyValuePair<Type, JniTypeSignature>(typeof (Int16),     new JniTypeSignature ("java/lang/Short")),
			new KeyValuePair<Type, JniTypeSignature>(typeof (Int32),     new JniTypeSignature ("I", 0, keyword: true)),
			new KeyValuePair<Type, JniTypeSignature>(typeof (Int32),     new JniTypeSignature ("java/lang/Integer")),
			new KeyValuePair<Type, JniTypeSignature>(typeof (Int64),     new JniTypeSignature ("J", 0, keyword: true)),
			new KeyValuePair<Type, JniTypeSignature>(typeof (Int64),     new JniTypeSignature ("java/lang/Long")),
			new KeyValuePair<Type, JniTypeSignature>(typeof (Single),     new JniTypeSignature ("F", 0, keyword: true)),
			new KeyValuePair<Type, JniTypeSignature>(typeof (Single),     new JniTypeSignature ("java/lang/Float")),
			new KeyValuePair<Type, JniTypeSignature>(typeof (Double),     new JniTypeSignature ("D", 0, keyword: true)),
			new KeyValuePair<Type, JniTypeSignature>(typeof (Double),     new JniTypeSignature ("java/lang/Double")),
		};

		static readonly KeyValuePair<Type, JniMarshalInfo>[] JniBuiltinMarshalers = new []{
			new KeyValuePair<Type, JniMarshalInfo>(typeof (string), new JniMarshalInfo {
				GetValueFromJni             = JniEnvironment.Strings.ToString,
				CreateLocalRef              = JniEnvironment.Strings.NewString,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Boolean), new JniMarshalInfo {
				CreateJniArgumentValue      = JniBoolean.CreateJniArgumentValue,
				GetValueFromJni             = JniBoolean.GetValueFromJni,
				CreateLocalRef              = JniBoolean.CreateLocalRef,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (SByte), new JniMarshalInfo {
				CreateJniArgumentValue      = JniByte.CreateJniArgumentValue,
				GetValueFromJni             = JniByte.GetValueFromJni,
				CreateLocalRef              = JniByte.CreateLocalRef,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Char), new JniMarshalInfo {
				CreateJniArgumentValue      = JniCharacter.CreateJniArgumentValue,
				GetValueFromJni             = JniCharacter.GetValueFromJni,
				CreateLocalRef              = JniCharacter.CreateLocalRef,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Int16), new JniMarshalInfo {
				CreateJniArgumentValue      = JniShort.CreateJniArgumentValue,
				GetValueFromJni             = JniShort.GetValueFromJni,
				CreateLocalRef              = JniShort.CreateLocalRef,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Int32), new JniMarshalInfo {
				CreateJniArgumentValue      = JniInteger.CreateJniArgumentValue,
				GetValueFromJni             = JniInteger.GetValueFromJni,
				CreateLocalRef              = JniInteger.CreateLocalRef,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Int64), new JniMarshalInfo {
				CreateJniArgumentValue      = JniLong.CreateJniArgumentValue,
				GetValueFromJni             = JniLong.GetValueFromJni,
				CreateLocalRef              = JniLong.CreateLocalRef,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Single), new JniMarshalInfo {
				CreateJniArgumentValue      = JniFloat.CreateJniArgumentValue,
				GetValueFromJni             = JniFloat.GetValueFromJni,
				CreateLocalRef              = JniFloat.CreateLocalRef,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Double), new JniMarshalInfo {
				CreateJniArgumentValue      = JniDouble.CreateJniArgumentValue,
				GetValueFromJni             = JniDouble.GetValueFromJni,
				CreateLocalRef              = JniDouble.CreateLocalRef,
			}),
		};
	}

	static class JniBoolean {
		internal    const   string  JniTypeName = "java/lang/Boolean";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		internal static JniArgumentValue CreateJniArgumentValue (object value)
		{
			return new JniArgumentValue ((Boolean) value);
		}

		static JniInstanceMethodInfo init;
		internal static unsafe JniObjectReference CreateLocalRef (object value)
		{
			Debug.Assert (value is Boolean, "Expected value of type `Boolean`; was: " + (value == null ? "<null>" : value.GetType ().FullName));

			var args    = stackalloc JniArgumentValue [1];
			args [0]    = new JniArgumentValue ((Boolean) value);

			TypeRef.GetCachedConstructor (ref init, "(Z)V");
			return TypeRef.NewObject (init, args);
		}

		static JniInstanceMethodInfo booleanValue;
		internal static object GetValueFromJni (ref JniObjectReference self, JniObjectReferenceOptions transfer, Type targetType)
		{
			Debug.Assert (targetType == null || targetType == typeof (Boolean), "Expected targetType==typeof(Boolean); was: " + targetType);
			TypeRef.GetCachedInstanceMethod (ref booleanValue, "booleanValue", "()Z");
			try {
				return booleanValue.InvokeVirtualBooleanMethod (self);
			} finally {
				JniEnvironment.References.Dispose (ref self, transfer);
			}
		}
	}

	static class JniByte {
		internal    const   string  JniTypeName = "java/lang/Byte";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		internal static JniArgumentValue CreateJniArgumentValue (object value)
		{
			return new JniArgumentValue ((SByte) value);
		}

		static JniInstanceMethodInfo init;
		internal static unsafe JniObjectReference CreateLocalRef (object value)
		{
			Debug.Assert (value is SByte, "Expected value of type `SByte`; was: " + (value == null ? "<null>" : value.GetType ().FullName));

			var args    = stackalloc JniArgumentValue [1];
			args [0]    = new JniArgumentValue ((SByte) value);

			TypeRef.GetCachedConstructor (ref init, "(B)V");
			return TypeRef.NewObject (init, args);
		}

		static JniInstanceMethodInfo byteValue;
		internal static object GetValueFromJni (ref JniObjectReference self, JniObjectReferenceOptions transfer, Type targetType)
		{
			Debug.Assert (targetType == null || targetType == typeof (SByte), "Expected targetType==typeof(SByte); was: " + targetType);
			TypeRef.GetCachedInstanceMethod (ref byteValue, "byteValue", "()B");
			try {
				return byteValue.InvokeVirtualSByteMethod (self);
			} finally {
				JniEnvironment.References.Dispose (ref self, transfer);
			}
		}
	}

	static class JniCharacter {
		internal    const   string  JniTypeName = "java/lang/Character";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		internal static JniArgumentValue CreateJniArgumentValue (object value)
		{
			return new JniArgumentValue ((Char) value);
		}

		static JniInstanceMethodInfo init;
		internal static unsafe JniObjectReference CreateLocalRef (object value)
		{
			Debug.Assert (value is Char, "Expected value of type `Char`; was: " + (value == null ? "<null>" : value.GetType ().FullName));

			var args    = stackalloc JniArgumentValue [1];
			args [0]    = new JniArgumentValue ((Char) value);

			TypeRef.GetCachedConstructor (ref init, "(C)V");
			return TypeRef.NewObject (init, args);
		}

		static JniInstanceMethodInfo charValue;
		internal static object GetValueFromJni (ref JniObjectReference self, JniObjectReferenceOptions transfer, Type targetType)
		{
			Debug.Assert (targetType == null || targetType == typeof (Char), "Expected targetType==typeof(Char); was: " + targetType);
			TypeRef.GetCachedInstanceMethod (ref charValue, "charValue", "()C");
			try {
				return charValue.InvokeVirtualCharMethod (self);
			} finally {
				JniEnvironment.References.Dispose (ref self, transfer);
			}
		}
	}

	static class JniShort {
		internal    const   string  JniTypeName = "java/lang/Short";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		internal static JniArgumentValue CreateJniArgumentValue (object value)
		{
			return new JniArgumentValue ((Int16) value);
		}

		static JniInstanceMethodInfo init;
		internal static unsafe JniObjectReference CreateLocalRef (object value)
		{
			Debug.Assert (value is Int16, "Expected value of type `Int16`; was: " + (value == null ? "<null>" : value.GetType ().FullName));

			var args    = stackalloc JniArgumentValue [1];
			args [0]    = new JniArgumentValue ((Int16) value);

			TypeRef.GetCachedConstructor (ref init, "(S)V");
			return TypeRef.NewObject (init, args);
		}

		static JniInstanceMethodInfo shortValue;
		internal static object GetValueFromJni (ref JniObjectReference self, JniObjectReferenceOptions transfer, Type targetType)
		{
			Debug.Assert (targetType == null || targetType == typeof (Int16), "Expected targetType==typeof(Int16); was: " + targetType);
			TypeRef.GetCachedInstanceMethod (ref shortValue, "shortValue", "()S");
			try {
				return shortValue.InvokeVirtualInt16Method (self);
			} finally {
				JniEnvironment.References.Dispose (ref self, transfer);
			}
		}
	}

	static class JniInteger {
		internal    const   string  JniTypeName = "java/lang/Integer";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		internal static JniArgumentValue CreateJniArgumentValue (object value)
		{
			return new JniArgumentValue ((Int32) value);
		}

		static JniInstanceMethodInfo init;
		internal static unsafe JniObjectReference CreateLocalRef (object value)
		{
			Debug.Assert (value is Int32, "Expected value of type `Int32`; was: " + (value == null ? "<null>" : value.GetType ().FullName));

			var args    = stackalloc JniArgumentValue [1];
			args [0]    = new JniArgumentValue ((Int32) value);

			TypeRef.GetCachedConstructor (ref init, "(I)V");
			return TypeRef.NewObject (init, args);
		}

		static JniInstanceMethodInfo intValue;
		internal static object GetValueFromJni (ref JniObjectReference self, JniObjectReferenceOptions transfer, Type targetType)
		{
			Debug.Assert (targetType == null || targetType == typeof (Int32), "Expected targetType==typeof(Int32); was: " + targetType);
			TypeRef.GetCachedInstanceMethod (ref intValue, "intValue", "()I");
			try {
				return intValue.InvokeVirtualInt32Method (self);
			} finally {
				JniEnvironment.References.Dispose (ref self, transfer);
			}
		}
	}

	static class JniLong {
		internal    const   string  JniTypeName = "java/lang/Long";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		internal static JniArgumentValue CreateJniArgumentValue (object value)
		{
			return new JniArgumentValue ((Int64) value);
		}

		static JniInstanceMethodInfo init;
		internal static unsafe JniObjectReference CreateLocalRef (object value)
		{
			Debug.Assert (value is Int64, "Expected value of type `Int64`; was: " + (value == null ? "<null>" : value.GetType ().FullName));

			var args    = stackalloc JniArgumentValue [1];
			args [0]    = new JniArgumentValue ((Int64) value);

			TypeRef.GetCachedConstructor (ref init, "(J)V");
			return TypeRef.NewObject (init, args);
		}

		static JniInstanceMethodInfo longValue;
		internal static object GetValueFromJni (ref JniObjectReference self, JniObjectReferenceOptions transfer, Type targetType)
		{
			Debug.Assert (targetType == null || targetType == typeof (Int64), "Expected targetType==typeof(Int64); was: " + targetType);
			TypeRef.GetCachedInstanceMethod (ref longValue, "longValue", "()J");
			try {
				return longValue.InvokeVirtualInt64Method (self);
			} finally {
				JniEnvironment.References.Dispose (ref self, transfer);
			}
		}
	}

	static class JniFloat {
		internal    const   string  JniTypeName = "java/lang/Float";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		internal static JniArgumentValue CreateJniArgumentValue (object value)
		{
			return new JniArgumentValue ((Single) value);
		}

		static JniInstanceMethodInfo init;
		internal static unsafe JniObjectReference CreateLocalRef (object value)
		{
			Debug.Assert (value is Single, "Expected value of type `Single`; was: " + (value == null ? "<null>" : value.GetType ().FullName));

			var args    = stackalloc JniArgumentValue [1];
			args [0]    = new JniArgumentValue ((Single) value);

			TypeRef.GetCachedConstructor (ref init, "(F)V");
			return TypeRef.NewObject (init, args);
		}

		static JniInstanceMethodInfo floatValue;
		internal static object GetValueFromJni (ref JniObjectReference self, JniObjectReferenceOptions transfer, Type targetType)
		{
			Debug.Assert (targetType == null || targetType == typeof (Single), "Expected targetType==typeof(Single); was: " + targetType);
			TypeRef.GetCachedInstanceMethod (ref floatValue, "floatValue", "()F");
			try {
				return floatValue.InvokeVirtualSingleMethod (self);
			} finally {
				JniEnvironment.References.Dispose (ref self, transfer);
			}
		}
	}

	static class JniDouble {
		internal    const   string  JniTypeName = "java/lang/Double";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		internal static JniArgumentValue CreateJniArgumentValue (object value)
		{
			return new JniArgumentValue ((Double) value);
		}

		static JniInstanceMethodInfo init;
		internal static unsafe JniObjectReference CreateLocalRef (object value)
		{
			Debug.Assert (value is Double, "Expected value of type `Double`; was: " + (value == null ? "<null>" : value.GetType ().FullName));

			var args    = stackalloc JniArgumentValue [1];
			args [0]    = new JniArgumentValue ((Double) value);

			TypeRef.GetCachedConstructor (ref init, "(D)V");
			return TypeRef.NewObject (init, args);
		}

		static JniInstanceMethodInfo doubleValue;
		internal static object GetValueFromJni (ref JniObjectReference self, JniObjectReferenceOptions transfer, Type targetType)
		{
			Debug.Assert (targetType == null || targetType == typeof (Double), "Expected targetType==typeof(Double); was: " + targetType);
			TypeRef.GetCachedInstanceMethod (ref doubleValue, "doubleValue", "()D");
			try {
				return doubleValue.InvokeVirtualDoubleMethod (self);
			} finally {
				JniEnvironment.References.Dispose (ref self, transfer);
			}
		}
	}
}
