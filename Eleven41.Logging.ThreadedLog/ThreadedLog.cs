using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Eleven41.Logging
{
	public class ThreadedLog : ILog
	{
		private ILog _log = null;
		private ConcurrentQueue<LogRecord> _records = new ConcurrentQueue<LogRecord>();
		private Thread _thread;

		/// <summary>
		/// Constructs a ThreadedLog object.
		/// </summary>
		/// <param name="log"></param>
		public ThreadedLog(ILog log)
		{
			_log = log;
			this.DateTimeProvider = new Eleven41.Logging.DateTimeProviders.DefaultDateTimeProvider();


			// Start the thread
			_thread = new Thread(new ThreadStart(Run));
			_thread.Start();
		}

		IDateTimeProvider _dateTimeProvider;

		public IDateTimeProvider DateTimeProvider
		{
			get
			{
				return _dateTimeProvider;
			}
			set
			{
				if (value == null)
					throw new ArgumentNullException();
				_dateTimeProvider = value;
			}
		}

		#region ILog Members


		public void Log(LogLevels level, string sFormat, params Object[] args)
		{
			LogRecord record = new LogRecord(this.DateTimeProvider.GetCurrentDateTime(), level, null, sFormat, args);
			_records.Enqueue(record);
		}

		public void Log(DateTime date, LogLevels level, string sFormat, params object[] args)
		{
			LogRecord record = new LogRecord(date, level, null, sFormat, args);
			_records.Enqueue(record);
		}

		public void Log(LogLevels level, Dictionary<string, object> data, string sFormat, params object[] args)
		{
			LogRecord record = new LogRecord(this.DateTimeProvider.GetCurrentDateTime(), level, data, sFormat, args);
			_records.Enqueue(record);
		}

		public void Log(DateTime date, LogLevels level, Dictionary<string, object> data, string sFormat, params object[] args)
		{
			LogRecord record = new LogRecord(date, level, data, sFormat, args);
			_records.Enqueue(record);
		}

		#endregion

		/// <summary>
		/// Record of each message to be logged.
		/// </summary>
		private class LogRecord
		{
			private DateTime _date;
			private DateTime _insertionDate;
			private LogLevels _level;
			private string _messageFormat;
			private Object[] _args;
			private Dictionary<string, object> _data;

			/// <summary>
			/// Constructs a LogRecord object.
			/// </summary>
			/// <param name="level">Log level of the message.</param>
			/// <param name="messageFormat">Message to be logged.</param>
			public LogRecord(DateTime date, LogLevels level, Dictionary<string, object> data, string messageFormat, Object[] args)
			{
				_insertionDate = DateTime.UtcNow; // For bookkeeping
				_date = date;
				_level = level;
				_data = data;
				if (_data == null)
					_data = new Dictionary<string, object>();
				_messageFormat = messageFormat;
				_args = args;
			}

			/// <summary>
			/// Log the message to the supplied event log.
			/// </summary>
			/// <param name="log"></param>
			public void Log(ILog log)
			{
				_data["sendDelay"] = (DateTime.UtcNow - _insertionDate).TotalSeconds; // Bookkeeping

				// Log using our information
				log.Log(_date, _level, _data, _messageFormat, _args);
			}
		}

		private bool _isSendAllMessages = false;
		private ManualResetEvent _evStop = new ManualResetEvent(false);

		/// <summary>
		/// Stops the thread.
		/// </summary>
		public void Stop()
		{
			_evStop.Set();
		}

		/// <summary>
		/// Stops the thread and waits for it to complete.
		/// </summary>
		public void StopAndWait()
		{
			_isSendAllMessages = true;
			_evStop.Set();
			_thread.Join();
		}

		private void Run()
		{
			while (true)
			{
				// Should we stop the thread?
				if (_evStop.WaitOne(5, false))
					break;

				// Process the logs until we run out
				while (ProcessLogs())
					;
			}

			// We need to stop the thread, but should
			// we send all left-over messages?
			if (_isSendAllMessages)
			{
				while (ProcessLogs())
					;
			}
		}

		private bool ProcessLogs()
		{
			LogRecord record = null;

			// Get a record from the queue of log entries
			if (!_records.TryDequeue(out record))
				return false;

			// Log the record
			record.Log(_log);
			return true;
		}
	}
}
