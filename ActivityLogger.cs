using System;
using NuGet.Common;

namespace ProjectCreator.Service
{
    public class ActivityLogger : ILogger
    {
        private readonly string _filePath;

        public ActivityLogger(string filePath)
        {
            _filePath = filePath;
        }
        private void LogToFile(string message)
        {
            try
            {
                using StreamWriter writer = new(_filePath, append: true);
                writer.WriteLine($"{message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }

        public void PrintData(string data)
        {
            LogToFile(data);
        }
        public void LogInformation(string data)
        {
            LogToFile($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{data}");
        }
        public void LogMinimal(string data)
        {
            LogToFile(data);
        }
        
        public void LogWarning(string data) => LogToFile($"WARNING: {data}");
        public void LogError(string data) => LogToFile($"ERROR: {data}");

        void ILogger.LogDebug(string data)
        {
            throw new NotImplementedException();
        }

        void ILogger.LogVerbose(string data)
        {
            throw new NotImplementedException();
        }

        void ILogger.LogWarning(string data)
        {
            throw new NotImplementedException();
        }

        void ILogger.LogError(string data)
        {
            throw new NotImplementedException();
        }

        void ILogger.LogInformationSummary(string data)
        {
            throw new NotImplementedException();
        }

        void ILogger.Log(LogLevel level, string data)
        {
            throw new NotImplementedException();
        }

        Task ILogger.LogAsync(LogLevel level, string data)
        {
            throw new NotImplementedException();
        }

        void ILogger.Log(ILogMessage message)
        {
            throw new NotImplementedException();
        }

        Task ILogger.LogAsync(ILogMessage message)
        {
            throw new NotImplementedException();
        }
    }
}
