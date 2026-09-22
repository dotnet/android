namespace Java.IO
{
	public partial class ObjectStreamField
	{
		protected virtual void SetOffset (int newValue)
		{
#pragma warning disable CS0618 // This method is the supported wrapper for the legacy property setter.
			Offset = newValue;
#pragma warning restore CS0618
		}
	}
}
