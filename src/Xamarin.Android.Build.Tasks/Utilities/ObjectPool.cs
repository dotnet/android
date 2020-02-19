using System;
using System.Collections.Concurrent;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// A class for pooling and reusing objects. See MemoryStreamPool.
	/// 
	/// Based on:
	/// https://docs.microsoft.com/dotnet/standard/collections/thread-safe/how-to-create-an-object-pool
	/// https://docs.microsoft.com/dotnet/api/system.buffers.arraypool-1
	/// </summary>
	class ObjectPool<T>
	{
		readonly ConcurrentBag<T> bag = new ConcurrentBag<T>();
		readonly Func<T> generator;

		public ObjectPool (Func<T> generator)
		{
			if (generator == null)
				throw new ArgumentNullException (nameof (generator));
			this.generator = generator;
		}

		public virtual T Rent ()
		{
			if (bag.TryTake (out T item))
				return item;
			return generator ();
		}

		public virtual void Return (T item)
		{
			bag.Add (item);
		}
	}
}
