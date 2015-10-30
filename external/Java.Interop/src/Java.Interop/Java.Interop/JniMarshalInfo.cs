using System;

namespace Java.Interop {

	using CreateJniArgumentValueHandler     = Func<object /* value */, JniArgumentValue>;
	using CreateLocalRefHandler             = Func<object /* value */, JniObjectReference>;
	using CreateMarshalCollectionHandler    = Func<object /* value */, IJavaPeerable>;
	using CleanupMarshalCollectionHandler   = Action<IJavaPeerable /* sourceValue */, object /* destValue */>;

	public  delegate    object  CreateValueFromJni (ref JniObjectReference reference, JniObjectReferenceOptions transfer, Type targetType);

	public struct JniMarshalInfo {

		public  CreateValueFromJni                  GetValueFromJni;
		public  CreateJniArgumentValueHandler       CreateJniArgumentValue;
		public  CreateLocalRefHandler               CreateLocalRef;
		public  CreateMarshalCollectionHandler      CreateMarshalCollection;
		public  CleanupMarshalCollectionHandler     CleanupMarshalCollection;
	}
}

