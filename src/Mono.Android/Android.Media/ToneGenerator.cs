using System;

namespace Android.Media
{
	public partial class ToneGenerator
	{
		[Obsolete ("It was based on wrong use of enum. Please use ToneGenerator(Stream,int) instead.")]
		public ToneGenerator (Stream streamType, Volume volume)
			: this (streamType, (int) volume)
		{
		}
	}
}
