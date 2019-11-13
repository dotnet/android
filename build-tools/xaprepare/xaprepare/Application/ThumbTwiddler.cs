using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Xamarin.Android.Prepare
{
	partial class ThumbTwiddler : AppObject
	{
		// yeah, I know, I know :P
		// Feel free to add more cheesy messages - the more the merrier! :)
		static readonly List <string> alternateTwiddlerMessages = new List <string> {
			"Working",
			"Twiddle de dum, twiddle de de",
			"Busy",
			"So much stuff to do, so little time",
			"'Round and 'round I go",
			"Row, row, row your boat",
			"1 2 3, left, left",
			"And the time slowly passes",
			"Tick, tock",
			"Yawn... not finished yet",
			"The paint dries slowly",
			"Slowly rotate the wheels of time",
			"What's these few minutes compared to eternity",
			"Patience, my friend",
			"To wait, or not to wait",
			"\"Wait\" is my second name",
			"And wait I shall",
			"Still busy",
			"Working relentlessly",
			"Busy, as always",
			"Busy, twiddling",
			"Still not done",
		};

		readonly Context context;
		readonly string[] twiddler;
		readonly long twiddleInterval;
		readonly int twiddleSteps;
		readonly bool dullMode;
		readonly bool showElapsedTime;

		int twiddleIndex;
		Timer twiddleTimer;
		Stopwatch watch;
		Random random;

		public ThumbTwiddler (Context context, bool dullMode, bool showElapsedTime)
		{
			this.context = context ?? throw new ArgumentNullException (nameof (context));
			if (!context.InteractiveSession)
				dullMode = true;

			twiddler = context.Characters.Twiddler;
			if (twiddler == null || twiddler.Length == 0) {
				dullMode = true;
				twiddleSteps = 0;
			} else {
				twiddleSteps = twiddler.Length;
			}

			if (!dullMode)
				twiddleInterval = 1000 / twiddler.Length;
			else {
				random = new Random ();
				twiddleInterval = 10000;
			}

			this.dullMode = dullMode;
			this.showElapsedTime = showElapsedTime;
			twiddleIndex = 0;
		}

		public void Start ()
		{
			if (twiddleTimer != null)
				return;

			if (showElapsedTime) {
				watch = new Stopwatch ();
				watch.Start ();
			}

			twiddleTimer = new Timer (Twiddle);
			twiddleTimer.Change (250, twiddleInterval);
		}

		public void Stop ()
		{
			if (twiddleTimer == null)
				return;

			twiddleTimer.Dispose ();
			twiddleTimer = null;
			if (watch != null) {
				WriteTwiddler ($"Elapsed: {GetElapsedTime ()}", showElapsed: false, skipLogFile: false);
			} else
				WriteTwiddler ("        ", showElapsed: false);
		}

		void Twiddle (object state)
		{
			if (dullMode) {
				string wittyMessage = alternateTwiddlerMessages[random.Next (0, alternateTwiddlerMessages.Count - 1)];
				Log.StatusLine ($"{wittyMessage}... {GetElapsedTime ()}", ConsoleColor.Gray, skipLogFile: true);
				return;
			}

			if (twiddleIndex >= twiddleSteps)
				twiddleIndex = 0;

			WriteTwiddler (twiddler [twiddleIndex++], watch != null);
		}

		void WriteTwiddler (string s, bool showElapsed, bool skipLogFile = true)
		{
			int curRow = Utilities.ConsoleCursorTop;
			int curColumn = 0; // Console.CursorLeft is curiously unreliable on Mono, the cursor jumps around

			try {
				Utilities.ConsoleSetCursorPosition (0, Utilities.ConsoleWindowHeight - 1);
				string message;
				if (!showElapsed)
					message = s;
				else
					message = $"{s} {GetElapsedTime ()}";

				Log.Status (message, ConsoleColor.White, skipLogFile: skipLogFile);
			} catch {
				// ignore
			} finally {
				Utilities.ConsoleSetCursorPosition (curColumn, curRow);
			}
		}

		string GetElapsedTime ()
		{
			if (watch == null)
				return String.Empty;

			return watch.Elapsed.ToString ();
		}
	}
}
