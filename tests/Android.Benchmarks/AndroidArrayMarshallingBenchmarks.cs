using Android.App;
using Android.App.AppSearch;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Util;
using BenchmarkDotNet.Attributes;
using Java.Security;
using System.Runtime.Versioning;

namespace Xamarin.Android.Benchmarks;

[MemoryDiagnoser]
[SupportedOSPlatform ("android31.0")]
public class AndroidArrayMarshallingBenchmarks
{
	const int Base64ByteCount = 344;
	const int ByteCount = 256;
	const string BytesProperty = "bytes";
	const string BytesArrayProperty = "bytesArray";
	const int DigestByteCount = 32;

	readonly byte [] bytes = new byte [ByteCount];
	readonly byte [] encodedBytes;
	readonly Bitmap bitmap;
	readonly Context context;
	readonly GenericDocument document;
	readonly MessageDigest messageDigest;
	readonly Parcel parcel;
	readonly int [] pixels = new int [64];
	readonly IntPtr preparedByteArray;
	readonly IntPtr preparedJaggedByteArray;

	public AndroidArrayMarshallingBenchmarks ()
	{
		context = Application.Context;
		encodedBytes = Base64.Encode (bytes, Base64Flags.NoWrap)
			?? throw new InvalidOperationException ("Could not Base64-encode the benchmark input.");
		Bitmap.Config bitmapConfig = Bitmap.Config.Argb8888
			?? throw new InvalidOperationException ("Could not obtain the ARGB_8888 bitmap configuration.");
		bitmap = Bitmap.CreateBitmap (8, 8, bitmapConfig);
		messageDigest = MessageDigest.GetInstance ("SHA-256")
			?? throw new InvalidOperationException ("Could not obtain SHA-256.");
		parcel = Parcel.Obtain ();
		parcel.WriteByteArray (bytes);
		IntPtr localByteArray = global::Android.Runtime.JNIEnv.NewArray (bytes);
		try {
			preparedByteArray = global::Android.Runtime.JNIEnv.NewGlobalRef (localByteArray);
		} finally {
			global::Android.Runtime.JNIEnv.DeleteLocalRef (localByteArray);
		}
		IntPtr localJaggedByteArray = global::Android.Runtime.JNIEnv.NewArray<byte []> ([bytes, bytes, bytes, bytes]);
		try {
			preparedJaggedByteArray = global::Android.Runtime.JNIEnv.NewGlobalRef (localJaggedByteArray);
		} finally {
			global::Android.Runtime.JNIEnv.DeleteLocalRef (localJaggedByteArray);
		}

		using var builder = new GenericDocument.Builder ("benchmark", "1", "ByteDocument");
		builder.SetPropertyBytes (BytesProperty, [bytes]);
		builder.SetPropertyBytes (BytesArrayProperty, [bytes, bytes, bytes, bytes]);
		document = builder.Build ();
	}

	[GlobalSetup]
	public void Setup ()
	{
		byte []? byteArray = GetByteArray ();
		if (byteArray is null || byteArray.Length != bytes.Length)
			throw new InvalidOperationException ("GenericDocument byte array length is incorrect.");
		byte [] []? jaggedByteArray = GetJaggedByteArray ();
		if (jaggedByteArray is null || jaggedByteArray.Length != 4)
			throw new InvalidOperationException ("GenericDocument jagged byte array length is incorrect.");
	}

	[GlobalCleanup]
	public void Cleanup ()
	{
		document.Dispose ();
		messageDigest.Dispose ();
		bitmap.Dispose ();
		parcel.Dispose ();
		global::Android.Runtime.JNIEnv.DeleteGlobalRef (preparedByteArray);
		global::Android.Runtime.JNIEnv.DeleteGlobalRef (preparedJaggedByteArray);
	}

	[Benchmark]
	public byte []? EncodeByteArray ()
	{
		return Base64.Encode (bytes, Base64Flags.NoWrap);
	}

	[Benchmark]
	public byte []? DecodeByteArray ()
	{
		return Base64.Decode (encodedBytes, Base64Flags.NoWrap);
	}

	[Benchmark]
	public byte []? MarshallParcel ()
	{
		return parcel.Marshall ();
	}

	[Benchmark]
	public byte []? DigestByteArray ()
	{
		return messageDigest.Digest (bytes);
	}

	[Benchmark]
	public void GetBitmapPixels ()
	{
		bitmap.GetPixels (pixels, 0, 8, 0, 0, 8, 8);
	}

	[Benchmark]
	public string []? GetPackagesForUid ()
	{
		return context.PackageManager?.GetPackagesForUid (global::Android.OS.Process.MyUid ());
	}

	[Benchmark]
	public byte []? GetByteArray ()
	{
		return document.GetPropertyBytes (BytesProperty);
	}

	[Benchmark]
	public byte [] []? GetJaggedByteArray ()
	{
		return document.GetPropertyBytesArray (BytesArrayProperty);
	}

	[Benchmark]
	public byte []? ConvertPreparedByteArray ()
	{
		return (byte []?) global::Android.Runtime.JNIEnv.GetArray (
			preparedByteArray,
			global::Android.Runtime.JniHandleOwnership.DoNotTransfer,
			typeof (byte));
	}

	[Benchmark]
	public byte [] []? ConvertPreparedJaggedByteArray ()
	{
		return (byte [] []?) global::Android.Runtime.JNIEnv.GetArray (
			preparedJaggedByteArray,
			global::Android.Runtime.JniHandleOwnership.DoNotTransfer,
			typeof (byte []));
	}

	[Benchmark]
	public byte [] AllocateDigestSizedByteArray ()
	{
		return new byte [DigestByteCount];
	}

	[Benchmark]
	public byte [] AllocateByteArray ()
	{
		return new byte [ByteCount];
	}

	[Benchmark]
	public byte [] AllocateBase64SizedByteArray ()
	{
		return new byte [Base64ByteCount];
	}

	[Benchmark]
	public byte [] [] AllocateJaggedByteArray ()
	{
		return [
			new byte [ByteCount],
			new byte [ByteCount],
			new byte [ByteCount],
			new byte [ByteCount],
		];
	}
}
