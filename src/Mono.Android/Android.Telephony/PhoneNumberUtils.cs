namespace Android.Telephony {

	partial class PhoneNumberUtils {

		public static string StringFromStringAndTOA (string s, int TOA)
		{
			return StringFromStringAndTOA (s, (PhoneNumberToa) TOA);
		}
	}
}