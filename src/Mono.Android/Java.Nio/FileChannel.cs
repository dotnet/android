#if ANDROID_24 && (NET || !ANDROID_34)

using System;

namespace Java.Nio.Channels
{
	public partial class FileChannel
	{
/*
This had to be added for API compatibility with earlier API Levels.

It is a newly introduced breakage for OpenJDK migration.
FileChannel now implements SeekableByteChannel, which never existed, and
they require those methods to return ISeekableByteChannel in C#, not
FileChannel whereas FileChannel is ISeekableByteChannel.

So they were first changed in the metadata fixup first, but then it resulted
in the API breakage. Therefore, I'm reverting the changes in metadata
and adding explicit interface methods here instead.
*/
		ISeekableByteChannel? ISeekableByteChannel.Position (long newPosition)
		{
			return Position (newPosition);
		}

		ISeekableByteChannel? ISeekableByteChannel.Truncate (long size)
		{
			return Truncate (size);
		}
	}
}

#endif

