﻿using System;
using BizHawk.Emulation.Common;

namespace BizHawk.Client.Common
{
	public partial class BkmMovie : IMovie
	{
		private readonly PlatformFrameRates _frameRates = new PlatformFrameRates();
		private bool _makeBackup = true;
		private bool _changes;
		private int? _loopOffset;

		public BkmMovie(string filename)
			: this()
		{
			Rerecords = 0;
			Filename = filename;
			Loaded = !string.IsNullOrWhiteSpace(filename);
		}

		public BkmMovie()
		{
			Header = new BkmHeader();
			Header[HeaderKeys.MOVIEVERSION] = "BizHawk v0.0.1";
			Filename = string.Empty;
			_preloadFramecount = 0;

			IsCountingRerecords = true;
			_mode = Moviemode.Inactive;
			_makeBackup = true;
		}

		#region Properties

		public ILogEntryGenerator LogGeneratorInstance()
		{
			return new BkmLogEntryGenerator();
		}

		public string PreferredExtension { get { return "bkm"; } }
		public BkmHeader Header { get; private set; }
		public string Filename { get; set; }
		public bool IsCountingRerecords { get; set; }
		public bool Loaded { get; private set; }
		
		public int InputLogLength
		{
			get { return _log.Count; }
		}

		public double FrameCount
		{
			get
			{
				if (_loopOffset.HasValue)
				{
					return double.PositiveInfinity;
				}
				
				if (Loaded)
				{
					return _log.Count;
				}

				return _preloadFramecount;
			}
		}

		public bool Changes
		{
			get { return _changes; }
		}

		public double Fps
		{
			get
			{
				var system = Header[HeaderKeys.PLATFORM];
				var pal = Header.ContainsKey(HeaderKeys.PAL) &&
					Header[HeaderKeys.PAL] == "1";

				return _frameRates[system, pal];
			}
		}

		public TimeSpan Time
		{
			get
			{
				var dblseconds = GetSeconds(Loaded ? _log.Count : _preloadFramecount);
				var seconds = (int)(dblseconds % 60);
				var days = seconds / 86400;
				var hours = seconds / 3600;
				var minutes = (seconds / 60) % 60;
				var milliseconds = (int)((dblseconds - seconds) * 1000);
				return new TimeSpan(days, hours, minutes, seconds, milliseconds);
			}
		}

		#endregion

		#region Public Log Editing

		public string GetInput(int frame)
		{
			if (frame < FrameCount && frame >= 0)
			{

				int getframe;

				if (_loopOffset.HasValue)
				{
					if (frame < _log.Count)
					{
						getframe = frame;
					}
					else
					{
						getframe = ((frame - _loopOffset.Value) % (_log.Count - _loopOffset.Value)) + _loopOffset.Value;
					}
				}
				else
				{
					getframe = frame;
				}

				return _log[getframe];
			}

			Finish();
			return string.Empty;
		}

		public void ClearFrame(int frame)
		{
			var lg = LogGeneratorInstance();
			SetFrameAt(frame, lg.EmptyEntry);
			_changes = true;
		}

		public void AppendFrame(IController source)
		{
			var lg = LogGeneratorInstance();
			lg.SetSource(source);
			_log.Add(lg.GenerateLogEntry());
			_changes = true;
		}

		public void Truncate(int frame)
		{
			if (frame < _log.Count)
			{
				_log.RemoveRange(frame, _log.Count - frame);
				_changes = true;
			}
		}

		public void PokeFrame(int frame, IController source)
		{
			var lg = LogGeneratorInstance();
			lg.SetSource(source);

			_changes = true;
			SetFrameAt(frame, lg.GenerateLogEntry());
		}

		public void RecordFrame(int frame, IController source)
		{
			// Note: Truncation here instead of loadstate will make VBA style loadstates
			// (Where an entire movie is loaded then truncated on the next frame
			// this allows users to restore a movie with any savestate from that "timeline"
			if (Global.Config.VBAStyleMovieLoadState)
			{
				if (Global.Emulator.Frame < _log.Count)
				{
					Truncate(Global.Emulator.Frame);
				}
			}

			var lg = LogGeneratorInstance();
			lg.SetSource(source);
			SetFrameAt(frame, lg.GenerateLogEntry());

			_changes = true;
		}

		#endregion

		private double GetSeconds(int frameCount)
		{
			double frames = frameCount;
			
			if (frames < 1)
			{
				return 0;
			}

			return frames / Fps;
		}

		private void SetFrameAt(int frameNum, string frame)
		{
			if (_log.Count > frameNum)
			{
				_log[frameNum] = frame;
			}
			else
			{
				_log.Add(frame);
			}
		}
	}
}