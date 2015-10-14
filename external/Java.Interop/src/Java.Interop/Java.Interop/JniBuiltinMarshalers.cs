using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Java.Interop {

	partial class JavaVM {

		static readonly KeyValuePair<Type, JniTypeInfo>[] JniBuiltinTypeNameMappings = new []{
			new KeyValuePair<Type, JniTypeInfo>(typeof (string),    new JniTypeInfo ("java/lang/String")),

			new KeyValuePair<Type, JniTypeInfo>(typeof (void),      new JniTypeInfo ("V",   typeIsKeyword: true)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (void),      new JniTypeInfo ("java/lang/Void")),

			new KeyValuePair<Type, JniTypeInfo>(typeof (Boolean),  new JniTypeInfo ("Z", typeIsKeyword: true)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (Boolean),  new JniTypeInfo ("java/lang/Boolean")),
			new KeyValuePair<Type, JniTypeInfo>(typeof (SByte),  new JniTypeInfo ("B", typeIsKeyword: true)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (SByte),  new JniTypeInfo ("java/lang/Byte")),
			new KeyValuePair<Type, JniTypeInfo>(typeof (Char),  new JniTypeInfo ("C", typeIsKeyword: true)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (Char),  new JniTypeInfo ("java/lang/Character")),
			new KeyValuePair<Type, JniTypeInfo>(typeof (Int16),  new JniTypeInfo ("S", typeIsKeyword: true)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (Int16),  new JniTypeInfo ("java/lang/Short")),
			new KeyValuePair<Type, JniTypeInfo>(typeof (Int32),  new JniTypeInfo ("I", typeIsKeyword: true)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (Int32),  new JniTypeInfo ("java/lang/Integer")),
			new KeyValuePair<Type, JniTypeInfo>(typeof (Int64),  new JniTypeInfo ("J", typeIsKeyword: true)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (Int64),  new JniTypeInfo ("java/lang/Long")),
			new KeyValuePair<Type, JniTypeInfo>(typeof (Single),  new JniTypeInfo ("F", typeIsKeyword: true)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (Single),  new JniTypeInfo ("java/lang/Float")),
			new KeyValuePair<Type, JniTypeInfo>(typeof (Double),  new JniTypeInfo ("D", typeIsKeyword: true)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (Double),  new JniTypeInfo ("java/lang/Double")),
		};

		static readonly KeyValuePair<Type, JniMarshalInfo>[] JniBuiltinMarshalers = new []{
			new KeyValuePair<Type, JniMarshalInfo>(typeof (string), new JniMarshalInfo {
				GetValueFromJni             = JniEnvironment.Strings.ToString,
				CreateLocalRef              = JniEnvironment.Strings.NewString,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Boolean), new JniMarshalInfo {
				CreateJValue                = JniBoolean.CreateJValue,
				GetValueFromJni             = JniBoolean.GetValueFromJni,
				CreateLocalRef              = JniBoolean.CreateLocalRef,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (SByte), new JniMarshalInfo {
				CreateJValue                = JniByte.CreateJValue,
				GetValueFromJni             = JniByte.GetValueFromJni,
				CreateLocalRef              = JniByte.CreateLocalRef,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Char), new JniMarshalInfo {
				CreateJValue                = JniCharacter.CreateJValue,
				GetValueFromJni             = JniCharacter.GetValueFromJni,
				CreateLocalRef              = JniCharacter.CreateLocalRef,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Int16), new JniMarshalInfo {
				CreateJValue                = JniShort.CreateJValue,
				GetValueFromJni             = JniShort.GetValueFromJni,
				CreateLocalRef              = JniShort.CreateLocalRef,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Int32), new JniMarshalInfo {
				CreateJValue                = JniInteger.CreateJValue,
				GetValueFromJni             = JniInteger.GetValueFromJni,
				CreateLocalRef              = JniInteger.CreateLocalRef,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Int64), new JniMarshalInfo {
				CreateJValue                = JniLong.CreateJValue,
				GetValueFromJni             = JniLong.GetValueFromJni,
				CreateLocalRef              = JniLong.CreateLocalRef,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Single), new JniMarshalInfo {
				CreateJValue                = JniFloat.CreateJValue,
				GetValueFromJni             = JniFloat.GetValueFromJni,
				CreateLocalRef              = JniFloat.CreateLocalRef,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Double), new JniMarshalInfo {
				CreateJValue                = JniDouble.CreateJValue,
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

		internal static JValue CreateJValue (object value)
		{
			return new JValue ((Boolean) value);
		}

		static JniInstanceMethodID init;
		internal static unsafe JniObjectReference CreateLocalRef (object value)
		{
			Debug.Assert (value is Boolean, "Expected value of type `Boolean`; was: " + (value == null ? "<null>" : value.GetType ().FullName));

			var args    = stackalloc JValue [1];
			args [0]    = new JValue ((Boolean) value);

			TypeRef.GetCachedConstructor (ref init, "(Z)V");
			return TypeRef.NewObject (init, args);
		}

		static JniInstanceMethodID booleanValue;
		internal static object GetValueFromJni (ref JniObjectReference self, JniHandleOwnership transfer, Type targetType)
		{
			Debug.Assert (targetType == null || targetType == typeof (Boolean), "Expected targetType==typeof(Boolean); was: " + targetType);
			TypeRef.GetCachedInstanceMethod (ref booleanValue, "booleanValue", "()Z");
			try {
				return booleanValue.CallVirtualBooleanMethod (self);
			} finally {
				JniEnvironment.Handles.Dispose (ref self, transfer);
			}
		}
	}

	static class JniByte {
		internal    const   string  JniTypeName = "java/lang/Byte";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		internal static JValue CreateJValue (object value)
		{
			return new JValue ((SByte) value);
		}

		static JniInstanceMethodID init;
		internal static unsafe JniObjectReference CreateLocalRef (object value)
		{
			Debug.Assert (value is SByte, "Expected value of type `SByte`; was: " + (value == null ? "<null>" : value.GetType ().FullName));

			var args    = stackalloc JValue [1];
			args [0]    = new JValue ((SByte) value);

			TypeRef.GetCachedConstructor (ref init, "(B)V");
			return TypeRef.NewObject (init, args);
		}

		static JniInstanceMethodID byteValue;
		internal static object GetValueFromJni (ref JniObjectReference self, JniHandleOwnership transfer, Type targetType)
		{
			Debug.Assert (targetType == null || targetType == typeof (SByte), "Expected targetType==typeof(SByte); was: " + targetType);
			TypeRef.GetCachedInstanceMethod (ref byteValue, "byteValue", "()B");
			try {
				return byteValue.CallVirtualSByteMethod (self);
			} finally {
				JniEnvironment.Handles.Dispose (ref self, transfer);
			}
		}
	}

	static class JniCharacter {
		internal    const   string  JniTypeName = "java/lang/Character";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		internal static JValue CreateJValue (object value)
		{
			return new JValue ((Char) value);
		}

		static JniInstanceMethodID init;
		internal static unsafe JniObjectReference CreateLocalRef (object value)
		{
			Debug.Assert (value is Char, "Expected value of type `Char`; was: " + (value == null ? "<null>" : value.GetType ().FullName));

			var args    = stackalloc JValue [1];
			args [0]    = new JValue ((Char) value);

			TypeRef.GetCachedConstructor (ref init, "(C)V");
			return TypeRef.NewObject (init, args);
		}

		static JniInstanceMethodID charValue;
		internal static object GetValueFromJni (ref JniObjectReference self, JniHandleOwnership transfer, Type targetType)
		{
			Debug.Assert (targetType == null || targetType == typeof (Char), "Expected targetType==typeof(Char); was: " + targetType);
			TypeRef.GetCachedInstanceMethod (ref charValue, "charValue", "()C");
			try {
				return charValue.CallVirtualCharMethod (self);
			} finally {
				JniEnvironment.Handles.Dispose (ref self, transfer);
			}
		}
	}

	static class JniShort {
		internal    const   string  JniTypeName = "java/lang/Short";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		internal static JValue CreateJValue (object value)
		{
			return new JValue ((Int16) value);
		}

		static JniInstanceMethodID init;
		internal static unsafe JniObjectReference CreateLocalRef (object value)
		{
			Debug.Assert (value is Int16, "Expected value of type `Int16`; was: " + (value == null ? "<null>" : value.GetType ().FullName));

			var args    = stackalloc JValue [1];
			args [0]    = new JValue ((Int16) value);

			TypeRef.GetCachedConstructor (ref init, "(S)V");
			return TypeRef.NewObject (init, args);
		}

		static JniInstanceMethodID shortValue;
		internal static object GetValueFromJni (ref JniObjectReference self, JniHandleOwnership transfer, Type targetType)
		{
			Debug.Assert (targetType == null || targetType == typeof (Int16), "Expected targetType==typeof(Int16); was: " + targetType);
			TypeRef.GetCachedInstanceMethod (ref shortValue, "shortValue", "()S");
			try {
				return shortValue.CallVirtualInt16Method (self);
			} finally {
				JniEnvironment.Handles.Dispose (ref self, transfer);
			}
		}
	}

	static class JniInteger {
		internal    const   string  JniTypeName = "java/lang/Integer";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		internal static JValue CreateJValue (object value)
		{
			return new JValue ((Int32) value);
		}

		static JniInstanceMethodID init;
		internal static unsafe JniObjectReference CreateLocalRef (object value)
		{
			Debug.Assert (value is Int32, "Expected value of type `Int32`; was: " + (value == null ? "<null>" : value.GetType ().FullName));

			var args    = stackalloc JValue [1];
			args [0]    = new JValue ((Int32) value);

			TypeRef.GetCachedConstructor (ref init, "(I)V");
			return TypeRef.NewObject (init, args);
		}

		static JniInstanceMethodID intValue;
		internal static object GetValueFromJni (ref JniObjectReference self, JniHandleOwnership transfer, Type targetType)
		{
			Debug.Assert (targetType == null || targetType == typeof (Int32), "Expected targetType==typeof(Int32); was: " + targetType);
			TypeRef.GetCachedInstanceMethod (ref intValue, "intValue", "()I");
			try {
				return intValue.CallVirtualInt32Method (self);
			} finally {
				JniEnvironment.Handles.Dispose (ref self, transfer);
			}
		}
	}

	static class JniLong {
		internal    const   string  JniTypeName = "java/lang/Long";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		internal static JValue CreateJValue (object value)
		{
			return new JValue ((Int64) value);
		}

		static JniInstanceMethodID init;
		internal static unsafe JniObjectReference CreateLocalRef (object value)
		{
			Debug.Assert (value is Int64, "Expected value of type `Int64`; was: " + (value == null ? "<null>" : value.GetType ().FullName));

			var args    = stackalloc JValue [1];
			args [0]    = new JValue ((Int64) value);

			TypeRef.GetCachedConstructor (ref init, "(J)V");
			return TypeRef.NewObject (init, args);
		}

		static JniInstanceMethodID longValue;
		internal static object GetValueFromJni (ref JniObjectReference self, JniHandleOwnership transfer, Type targetType)
		{
			Debug.Assert (targetType == null || targetType == typeof (Int64), "Expected targetType==typeof(Int64); was: " + targetType);
			TypeRef.GetCachedInstanceMethod (ref longValue, "longValue", "()J");
			try {
				return longValue.CallVirtualInt64Method (self);
			} finally {
				JniEnvironment.Handles.Dispose (ref self, transfer);
			}
		}
	}

	static class JniFloat {
		internal    const   string  JniTypeName = "java/lang/Float";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		internal static JValue CreateJValue (object value)
		{
			return new JValue ((Single) value);
		}

		static JniInstanceMethodID init;
		internal static unsafe JniObjectReference CreateLocalRef (object value)
		{
			Debug.Assert (value is Single, "Expected value of type `Single`; was: " + (value == null ? "<null>" : value.GetType ().FullName));

			var args    = stackalloc JValue [1];
			args [0]    = new JValue ((Single) value);

			TypeRef.GetCachedConstructor (ref init, "(F)V");
			return TypeRef.NewObject (init, args);
		}

		static JniInstanceMethodID floatValue;
		internal static object GetValueFromJni (ref JniObjectReference self, JniHandleOwnership transfer, Type targetType)
		{
			Debug.Assert (targetType == null || targetType == typeof (Single), "Expected targetType==typeof(Single); was: " + targetType);
			TypeRef.GetCachedInstanceMethod (ref floatValue, "floatValue", "()F");
			try {
				return floatValue.CallVirtualSingleMethod (self);
			} finally {
				JniEnvironment.Handles.Dispose (ref self, transfer);
			}
		}
	}

	static class JniDouble {
		internal    const   string  JniTypeName = "java/lang/Double";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		internal static JValue CreateJValue (object value)
		{
			return new JValue ((Double) value);
		}

		static JniInstanceMethodID init;
		internal static unsafe JniObjectReference CreateLocalRef (object value)
		{
			Debug.Assert (value is Double, "Expected value of type `Double`; was: " + (value == null ? "<null>" : value.GetType ().FullName));

			var args    = stackalloc JValue [1];
			args [0]    = new JValue ((Double) value);

			TypeRef.GetCachedConstructor (ref init, "(D)V");
			return TypeRef.NewObject (init, args);
		}

		static JniInstanceMethodID doubleValue;
		internal static object GetValueFromJni (ref JniObjectReference self, JniHandleOwnership transfer, Type targetType)
		{
			Debug.Assert (targetType == null || targetType == typeof (Double), "Expected targetType==typeof(Double); was: " + targetType);
			TypeRef.GetCachedInstanceMethod (ref doubleValue, "doubleValue", "()D");
			try {
				return doubleValue.CallVirtualDoubleMethod (self);
			} finally {
				JniEnvironment.Handles.Dispose (ref self, transfer);
			}
		}
	}
}
