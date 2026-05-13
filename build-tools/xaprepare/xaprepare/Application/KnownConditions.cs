namespace Xamarin.Android.Prepare
{
	public enum KnownConditions
	{
		/// <summary>
		///   If set, the outdated or missing programs will be installed. Default: set.
		/// </summary>
		AllowProgramInstallation,

		/// <summary>
		///   Ignore missing programs and do not signal an error. Default: unset.
		/// </summary>
		IgnoreMissingPrograms,

		/// <summary>
		///   If set, will cause checkout of the commercial dependencies mentioned in the `.external` file at
		///   the top of the repository. Default: unset
		/// </summary>
		IncludeCommercial,

		/// <summary>
		///   If this condition is set, the current scenario will take care of missing essential (<see
		///   cref="EssentialTools"/>) programs should they be missing.
		/// </summary>
		EnsureEssential,
	}
}
