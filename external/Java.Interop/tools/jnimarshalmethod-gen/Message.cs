using Java.Interop.Localization;

namespace Xamarin.Android.Tools.JniMarshalMethodGenerator
{
	class Message
	{
		public string Localized { get; private set; }
		public int Code { get; private set; }

		private Message (int code, string message) {
			Localized = message;
			Code = code;
		}

		public static Message ErrorUnableToPreloadReference = new Message (0x4001, Resources.JniMarshalMethodGen_JM4001);
		public static Message ErrorAtLeastOneAssembly = new Message (0x4002, Resources.JniMarshalMethodGen_JM4002);
		public static Message ErrorUnableToCreateJavaVM = new Message (0x4003, Resources.JniMarshalMethodGen_JM4003);
		public static Message ErrorUnableToReadProfile = new Message (0x4004, Resources.JniMarshalMethodGen_JM4004);
		public static Message ErrorPathDoesNotExist = new Message (0x4005, Resources.JniMarshalMethodGen_JM4005);
		public static Message ErrorUnableToProcessAssembly = new Message (0x4006, Resources.JniMarshalMethodGen_JM4006);

		public static Message WarningCouldntFindInterface = new Message (0x8001, Resources.JniMarshalMethodGen_JM8001);
		public static Message WarningTypeLoadException = new Message (0x8003, Resources.JniMarshalMethodGen_JM8003);
		public static Message WarningUnableToFindTypeDefinition = new Message (0x8004, Resources.JniMarshalMethodGen_JM8004);
		public static Message WarningMarshalMethodsTypeAlreadyExists = new Message (0x8005, Resources.JniMarshalMethodGen_JM8005);
		public static Message WarningUnableToFindMethodDefinition = new Message (0x8006, Resources.JniMarshalMethodGen_JM8006);
		public static Message WarningUnableToFindSCWriteLine = new Message (0x8007, Resources.JniMarshalMethodGen_JM8007);
	}
}
