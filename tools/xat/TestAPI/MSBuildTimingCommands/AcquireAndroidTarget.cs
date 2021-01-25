using System.Threading.Tasks;

namespace Xamarin.Android.Tests.MSBuildTiming
{
	/// <summary>
	///   Makes sure that an Android device/emulator is running. This may involve creation of
	///   AVD (virtual Android device to be executed inside the emulator)
	/// </summary>
	class AcquireAndroidTarget : MSBuildTimingTestCommand
	{
		public override string Target => "UNUSED";
		public override string ID => nameof (AcquireAndroidTarget);

		public AcquireAndroidTarget ()
			: base ("AcquireAndroidTarget", "Makes sure an Android device/emulator are running and available")
		{}

		protected override async Task<bool> Run (TestMSBuildTiming test)
		{
			var androidDevice = new AndroidDevice ();

			// TODO: we're not preserving emulatorProcessId till ReleaseAndroidTarget time, check why
			(bool success, int emulatorProcessId, string adbTarget, string _) = await androidDevice.Start ();
			if (success && State != null) {
				State.EmulatorProcessId = emulatorProcessId;
				State.AdbTarget = adbTarget;
				return true;
			}
			return success;
		}
	}
}
