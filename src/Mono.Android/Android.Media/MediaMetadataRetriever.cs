namespace Android.Media {

	partial class MediaMetadataRetriever {

		public string? ExtractMetadata (int keyCode)
		{
			return ExtractMetadata ((MetadataKey) keyCode);
		}

		public Android.Graphics.Bitmap? GetFrameAtTime (long timeUs, int option)
		{
			return GetFrameAtTime (timeUs, (Option) option);
		}
	}
}
