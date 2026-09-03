using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using BenchmarkDotNet.Attributes;

namespace Xamarin.Android.Benchmarks;

[MemoryDiagnoser]
[InvocationCount (OperationsPerIteration)]
public class AndroidObjectCreationBenchmarks
{
	const int OperationsPerIteration = 256;

	readonly global::Android.Net.Uri baseUri;
	readonly int [] colors = new int [64];
	readonly global::Android.Net.Uri? [] appendedUris = new global::Android.Net.Uri? [OperationsPerIteration];
	readonly Intent? [] chooserIntents = new Intent? [OperationsPerIteration];
	readonly ApplicationInfo? [] applicationInfos = new ApplicationInfo? [OperationsPerIteration];
	readonly Bitmap? [] threeArgumentBitmaps = new Bitmap? [OperationsPerIteration];
	readonly Bitmap? [] fourArgumentBitmaps = new Bitmap? [OperationsPerIteration];
	readonly Bitmap.Config bitmapConfig;
	readonly PackageManager packageManager;
	readonly string packageName;
	readonly Intent intent;
	int appendedUriIndex;
	int chooserIntentIndex;
	int applicationInfoIndex;
	int threeArgumentBitmapIndex;
	int fourArgumentBitmapIndex;

	public AndroidObjectCreationBenchmarks ()
	{
		baseUri = global::Android.Net.Uri.Parse ("https://example.com/benchmark")
			?? throw new InvalidOperationException ("Could not parse the base URI.");
		Context context = Application.Context;
		packageManager = context.PackageManager ?? throw new InvalidOperationException ("PackageManager is unavailable.");
		packageName = context.PackageName ?? throw new InvalidOperationException ("Package name is unavailable.");
		bitmapConfig = Bitmap.Config.Argb8888
			?? throw new InvalidOperationException ("Could not obtain the ARGB_8888 bitmap configuration.");
		intent = new Intent (Intent.ActionView, baseUri);
	}

	[GlobalCleanup]
	public void Cleanup ()
	{
		CleanupAppendedUris ();
		CleanupChooserIntents ();
		CleanupApplicationInfos ();
		CleanupThreeArgumentBitmaps ();
		CleanupFourArgumentBitmaps ();
		intent.Dispose ();
		baseUri.Dispose ();
	}

	[IterationSetup (Target = nameof (WithAppendedPathTwoArguments))]
	public void SetupAppendedUris () => appendedUriIndex = 0;

	[IterationCleanup (Target = nameof (WithAppendedPathTwoArguments))]
	public void CleanupAppendedUris () => DisposeAll (appendedUris);

	[Benchmark]
	public global::Android.Net.Uri? WithAppendedPathTwoArguments ()
	{
		return appendedUris [appendedUriIndex++] = global::Android.Net.Uri.WithAppendedPath (baseUri, "child");
	}

	[IterationSetup (Target = nameof (CreateChooserTwoArguments))]
	public void SetupChooserIntents () => chooserIntentIndex = 0;

	[IterationCleanup (Target = nameof (CreateChooserTwoArguments))]
	public void CleanupChooserIntents () => DisposeAll (chooserIntents);

	[Benchmark]
	public Intent? CreateChooserTwoArguments ()
	{
		return chooserIntents [chooserIntentIndex++] = Intent.CreateChooser (intent, "benchmark");
	}

	[IterationSetup (Target = nameof (GetApplicationInfoTwoArguments))]
	public void SetupApplicationInfos () => applicationInfoIndex = 0;

	[IterationCleanup (Target = nameof (GetApplicationInfoTwoArguments))]
	public void CleanupApplicationInfos () => DisposeAll (applicationInfos);

	[Benchmark]
	public ApplicationInfo? GetApplicationInfoTwoArguments ()
	{
		return applicationInfos [applicationInfoIndex++] =
			packageManager.GetApplicationInfo (packageName, PackageInfoFlags.MetaData);
	}

	[IterationSetup (Target = nameof (CreateBitmapThreeArguments))]
	public void SetupThreeArgumentBitmaps () => threeArgumentBitmapIndex = 0;

	[IterationCleanup (Target = nameof (CreateBitmapThreeArguments))]
	public void CleanupThreeArgumentBitmaps () => DisposeAll (threeArgumentBitmaps);

	[Benchmark]
	public Bitmap? CreateBitmapThreeArguments ()
	{
		return threeArgumentBitmaps [threeArgumentBitmapIndex++] = Bitmap.CreateBitmap (8, 8, bitmapConfig);
	}

	[IterationSetup (Target = nameof (CreateBitmapFourArguments))]
	public void SetupFourArgumentBitmaps () => fourArgumentBitmapIndex = 0;

	[IterationCleanup (Target = nameof (CreateBitmapFourArguments))]
	public void CleanupFourArgumentBitmaps () => DisposeAll (fourArgumentBitmaps);

	[Benchmark]
	public Bitmap? CreateBitmapFourArguments ()
	{
		return fourArgumentBitmaps [fourArgumentBitmapIndex++] = Bitmap.CreateBitmap (colors, 8, 8, bitmapConfig);
	}

	static void DisposeAll<T> (T? [] values)
		where T : Java.Lang.Object
	{
		for (int i = 0; i < values.Length; i++) {
			values [i]?.Dispose ();
			values [i] = null;
		}
	}
}
