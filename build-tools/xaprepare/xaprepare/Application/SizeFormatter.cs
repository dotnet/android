namespace Xamarin.Android.Prepare
{
	class SizeFormatter
	{
		const ulong UL_KILOBYTE = 1024UL;
		const decimal FL_KILOBYTE = 1024.0M;
		const ulong UL_MEGABYTE = 1024UL * 1024UL;
		const decimal FL_MEGABYTE = 1024.0M * 1024.0M;
		const ulong UL_GIGABYTE = 1024UL * 1024UL * 1024UL;
		const decimal FL_GIGABYTE = 1024.0M * 1024.0M * 1024.0M;
		const ulong UL_TERABYTE = 1024UL * 1024UL * 1024UL * 1024UL;
		const decimal FL_TERABYTE = 1024.0M * 1024.0M * 1024.0M * 1024.0M;
		const ulong UL_PETABYTE = 1024UL * 1024UL * 1024UL * 1024UL * 1024UL;
		const decimal FL_PETABYTE = 1024.0M * 1024.0M * 1024.0M * 1024.0M * 1024.0M;
		const ulong UL_EXABYTE = 1024UL * 1024UL * 1024UL * 1024UL * 1024UL * 1024UL;
		const decimal FL_EXABYTE = 1024.0M * 1024.0M * 1024.0M * 1024.0M * 1024.0M * 1024.0M;

		public static void FormatBytes (ulong bytes, out decimal value, out string unit)
		{
			if (bytes < UL_KILOBYTE) {
				unit = "B";
				value = (decimal)bytes;
			} else if (bytes >= UL_KILOBYTE && bytes < UL_MEGABYTE) {
				unit = "KB";
				value = (decimal)bytes / FL_KILOBYTE;
			} else if (bytes >= UL_MEGABYTE && bytes < UL_GIGABYTE) {
				unit = "MB";
				value = (decimal)bytes / FL_MEGABYTE;
			} else if (bytes >= UL_GIGABYTE && bytes < UL_TERABYTE) {
				unit = "GB";
				value = (decimal)bytes / FL_GIGABYTE;
			} else if (bytes >= UL_TERABYTE && bytes < UL_PETABYTE) {
				unit = "TB";
				value = (decimal)bytes / FL_TERABYTE;
			} else if (bytes >= UL_PETABYTE && bytes < UL_EXABYTE) {
				unit = "PB";
				value = (decimal)bytes / FL_PETABYTE;
			} else {
				unit = "EB";
				value = (decimal)bytes / FL_EXABYTE;
			}
		}
	}
}
