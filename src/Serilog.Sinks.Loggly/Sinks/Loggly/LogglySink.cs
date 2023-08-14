// Copyright 2014 Serilog Contributors
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Loggly;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;

namespace Serilog.Sinks.Loggly
{
    /// <summary>
    /// Writes log events to the Loggly.com service.
    /// </summary>
    public class LogglySink : IBatchedLogEventSink
    {
        readonly LogEventConverter _converter;
        bool _isLogglyCreateError = false;
        readonly LogglyClient _client;
        readonly LogglyConfigAdapter _adapter;
        LoggingLevelSwitch _levelSwitch;

        /// <summary>
        /// A reasonable default for the number of events posted in
        /// each batch.
        /// </summary>
        public const int DefaultBatchPostingLimit = 10;

        /// <summary>
        /// A reasonable default for the number of events to hold in
        /// the active queue.
        /// </summary>
        public const int DefaultBatchQueueSize = 100_000;

        /// <summary>
        /// A reasonable default time to wait between checking for event batches.
        /// </summary>
        public static readonly TimeSpan DefaultPeriod = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Construct a sink that saves logs to the specified storage account. Properties are being send as data and the level is used as tag.
        /// </summary>
        ///  <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>        
        public LogglySink(IFormatProvider formatProvider) : this(formatProvider, null, null, null)
        {
        }

        /// <summary>
        /// Construct a sink that saves logs to the specified storage account. Properties are being send as data and the level is used as tag.
        /// </summary>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="logglyConfig">Used to configure underlying LogglyClient programmaticaly. Otherwise use app.Config.</param>
        /// <param name="includes">Decides if the sink should include specific properties in the log message</param>
        public LogglySink(IFormatProvider formatProvider, LogglyConfiguration logglyConfig, LogIncludes includes, LoggingLevelSwitch levelSwitch)
        {
            if (levelSwitch == null)
            {
                levelSwitch = new LoggingLevelSwitch();
            }
            _levelSwitch = levelSwitch;

            if (logglyConfig != null)
            {
                _adapter = new LogglyConfigAdapter();
                try
                {
                    _adapter.ConfigureLogglyClient(logglyConfig);
                }
                catch (Exception e)
                {
                    SelfLog.WriteLine("LogglySink failed to instantiate. Error Message: {0}\n Stack Trace: {1}", e.Message, e.StackTrace);
                    _isLogglyCreateError = true;
                }
            }
            _client = new LogglyClient();
            _converter = new LogEventConverter(formatProvider, includes);
        }

        /// <summary>
        /// Emit a batch of log events, running asynchronously.
        /// </summary>
        /// <param name="batch">The events to emit.</param>
        async Task IBatchedLogEventSink.EmitBatchAsync(IEnumerable<LogEvent> batch)
        {
            if (_isLogglyCreateError)
            {
                return;
            }

            IEnumerable<LogEvent> leveledBatch = batch.Where(s => s.Level >= _levelSwitch.MinimumLevel);
            LogResponse response = await _client.Log(leveledBatch.Select(_converter.CreateLogglyEvent)).ConfigureAwait(false);

            switch (response.Code)
            {
                case ResponseCode.Error:
                    SelfLog.WriteLine("LogglySink received an Error response: {0}", response.Message);
                    break;
                case ResponseCode.Unknown:
                    SelfLog.WriteLine("LogglySink received an Unknown response: {0}", response.Message);
                    break;
            }
        }

        public async Task OnEmptyBatchAsync() { }
    }
}
