﻿using EnoCore.Models.Database;
using EnoCore.Models.Json;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace EnoCore.Logging
{
    public class EnoLogMessageFileLogger : ILogger
    {
        private readonly IEnoLogMessageProvider Provider;
        private readonly string CategoryName;

        public EnoLogMessageFileLogger(IEnoLogMessageProvider provider, string categoryName)
        {
            Provider = provider;
            CategoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state)
        {
            return Provider.ScopeProvider?.Push(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if ((this as ILogger).IsEnabled(logLevel))
            {
                EnoLogMessage message = new EnoLogMessage
                {
                    Message = exception?.Message ?? state?.ToString() ?? "",
                    Module = CategoryName,
                    Tool = Provider.Tool,
                    Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    Severity = Severity(logLevel),
                    SeverityLevel = SeverityLevel(logLevel)
                };

                if (Provider.ScopeProvider != null)
                {
                    Provider.ScopeProvider.ForEachScope((value, loggingProps) =>
                    {
                        if (value is IEnumerable<KeyValuePair<string, object>> props)
                        {
                            foreach (var pair in props)
                            {
                                if (pair.Value is CheckerTask task)
                                    message.FromCheckerTask(task);
                            }
                        }
                    },
                    state);
                }
                Provider.Log($"##ENOLOGMESSAGE {JsonSerializer.Serialize(message)}\n");
            }
        }

        private string Severity(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.None => "DEBUG",
                LogLevel.Trace => "DEBUG",
                LogLevel.Debug => "DEBUG",
                LogLevel.Information => "INFO",
                LogLevel.Warning => "WARNING",
                LogLevel.Error => "ERROR",
                LogLevel.Critical => "CRITICAL",
                _ => throw new InvalidOperationException()
            };
        }

        private long SeverityLevel(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.None => 0,
                LogLevel.Trace => 0,
                LogLevel.Debug => 0,
                LogLevel.Information => 1,
                LogLevel.Warning => 2,
                LogLevel.Error => 3,
                LogLevel.Critical => 4,
                _ => throw new InvalidOperationException()
            };
        }
    }
}