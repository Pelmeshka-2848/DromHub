using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DromHub.Data
{
    /// <summary>
    /// Класс DatabaseResetGuard отвечает за логику компонента DatabaseResetGuard.
    /// </summary>
    public static class DatabaseResetGuard
    {
        public const string CommandLineToken = "--reset-db";
        public const string EnvironmentVariableName = "DROMHUB_FORCE_RESET";
        /// <summary>
        /// Метод IsResetRequested выполняет основную операцию класса.
        /// </summary>

        public static bool IsResetRequested(IConfiguration? configuration, string? launchArguments, ILogger logger)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            var arguments = ParseArguments(launchArguments);
            bool commandLineRequested = arguments.Any(arg => string.Equals(arg, CommandLineToken, StringComparison.OrdinalIgnoreCase));
            bool environmentRequested = TryReadEnvironmentFlag(out var envFlag) && envFlag;
            bool configurationRequested = configuration?.GetValue<bool?>("Database:ForceResetOnStartup") ?? false;
            bool developerRequested = configuration?.GetValue<bool?>("DeveloperSettings:ForceResetOnStartup") ?? false;

            bool requested = commandLineRequested || environmentRequested || configurationRequested || developerRequested;

            if (requested)
            {
                logger.LogWarning(
                    "Database force reset requested. Sources => CommandLine: {CommandLine}, EnvironmentVariable: {Environment}, Configuration: {Configuration}, DeveloperSettings: {Developer}",
                    commandLineRequested,
                    environmentRequested,
                    configurationRequested,
                    developerRequested);
            }

            return requested;
        }
        /// <summary>
        /// Метод ParseArguments выполняет основную операцию класса.
        /// </summary>

        private static IEnumerable<string> ParseArguments(string? launchArguments)
        {
            if (string.IsNullOrWhiteSpace(launchArguments))
            {
                return Array.Empty<string>();
            }

            var tokens = new List<string>();
            var builder = new StringBuilder();
            bool insideQuotes = false;

            foreach (var character in launchArguments)
            {
                if (character == '"')
                {
                    insideQuotes = !insideQuotes;
                    continue;
                }

                if (char.IsWhiteSpace(character) && !insideQuotes)
                {
                    if (builder.Length > 0)
                    {
                        tokens.Add(builder.ToString());
                        builder.Clear();
                    }
                }
                else
                {
                    builder.Append(character);
                }
            }

            if (builder.Length > 0)
            {
                tokens.Add(builder.ToString());
            }

            return tokens;
        }
        /// <summary>
        /// Метод TryReadEnvironmentFlag выполняет основную операцию класса.
        /// </summary>

        private static bool TryReadEnvironmentFlag(out bool value)
        {
            var envValue = Environment.GetEnvironmentVariable(EnvironmentVariableName);
            if (string.IsNullOrWhiteSpace(envValue))
            {
                value = false;
                return false;
            }

            if (bool.TryParse(envValue, out value))
            {
                return true;
            }

            if (string.Equals(envValue, "1", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            if (string.Equals(envValue, "0", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }

            value = false;
            return false;
        }
    }
}
