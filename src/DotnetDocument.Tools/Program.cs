using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Threading;
using System.Threading.Tasks;
using DotnetDocument.Tools.CLI;
using DotnetDocument.Tools.Config;
using DotnetDocument.Tools.Handlers;
using DotnetDocument.Tools.Utils;
using DotnetDocument.ToolsF;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace DotnetDocument.Tools
{
    /// <summary>
    /// The program class
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Main the args
        /// </summary>
        /// <param name="args">The args</param>
        /// <returns>A task containing the int</returns>
        public static async Task<int> Main(string[] args)
        {
            // Declare the dotnet-document command
            var documentCommand = new RootCommand("dotnet-document")
            {
                // Add sub commands
                ApplyCommand.Create(HandleApplyAsync),
                ConfigCommand.Create(HandleConfigAsync)
            };

            // Declare a new command line builder
            return await new CommandLineBuilder(documentCommand)
                .UseDefaults()
                .UseExceptionHandler(ExceptionFilter.Handle)
                .UseMiddleware(MeasureMiddleware.Handle)
                .Build()
                .InvokeAsync(args);
        }

        /// <summary>
        /// Handles the apply using the specified path
        /// </summary>
        /// <param name="path">The path</param>
        /// <param name="verbosity">The verbosity</param>
        /// <param name="config">The config</param>
        /// <param name="dryRun">The dry run</param>
        /// <param name="console">The console</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <exception cref="Exception">No implementation found for service {nameof(IApplyDocumentHandler)}</exception>
        /// <returns>A task containing the int</returns>
        private static async Task<int> HandleApplyAsync(string path, string verbosity, string config,
            bool dryRun, IConsole console, CancellationToken cancellationToken)
        {
            // Configure the logger
            var logger = LoggingUtils.ConfigureLogger(verbosity);

            logger.Verbose("dotnet-runtime version: {Version}", VersionUtils.GetRuntimeVersion());

            if (VersionUtils.TryGetVersion(out var version))
                logger.Verbose("dotnet-document version: {Version}", version);

            logger.Verbose("Verbosity {verbosity} converted to log level {level}",
                verbosity, LoggingUtils.ParseLogLevel(verbosity));
            logger.Verbose("Path to document: {path}", path);
            logger.Verbose("Is dry run: {dryRun}", dryRun);
            logger.Verbose("Config file from args: {config}", config);

            var configFilePath = IdentifyConfigFileToUse(config);

            // Declare a new service collection
            var services = new ServiceCollection();
            services.AddLogging(o => o.AddSerilog(logger));

            // Configure services collection
            services.ConfigureFromFile(configFilePath);
            services.AddDotnetDocument();

            // Build the service provider
            var serviceProvider = services.BuildServiceProvider();

            var handler = serviceProvider.GetService<IApplyDocumentHandler>();

            if (handler is null)
            {
                logger.Error("No implementation found for service {Type}", nameof(IApplyDocumentHandler));

                throw new Exception($"No implementation found for service {nameof(IApplyDocumentHandler)}");
            }

            var result = handler.Apply(path, dryRun);

            return await Task.FromResult((int)result);
        }

        /// <summary>
        /// Handles the config using the specified default
        /// </summary>
        /// <param name="@default">The default</param>
        /// <param name="config">The config</param>
        /// <param name="console">The console</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <exception cref="Exception">No implementation found for service {nameof(IDocumentConfigHandler)}</exception>
        /// <returns>A task containing the int</returns>
        private static async Task<int> HandleConfigAsync(bool @default, string config, IConsole console,
            CancellationToken cancellationToken)
        {
            // Configure the logger
            var logger = LoggingUtils.ConfigureLogger(null);

            var configFilePath = IdentifyConfigFileToUse(config);

            // Declare a new service collection
            var services = new ServiceCollection();
            services.AddLogging(o => o.AddSerilog(logger));

            // Configure services collection
            services.ConfigureFromFile(configFilePath);
            services.AddDotnetDocument();

            // Build the service provider
            var serviceProvider = services.BuildServiceProvider();

            var handler = serviceProvider.GetService<IDocumentConfigHandler>();

            if (handler is null)
            {
                logger.Error("No implementation found for service {Type}", nameof(IDocumentConfigHandler));

                throw new Exception($"No implementation found for service {nameof(IDocumentConfigHandler)}");
            }

            if (@default) return await Task.FromResult((int)handler.PrintDefaultConfig());

            return await Task.FromResult((int)handler.PrintCurrentConfig());
        }

        /// <summary>
        /// Identifies the config file to use using the specified args config file path
        /// </summary>
        /// <param name="argsConfigFilePath">The args config file path</param>
        /// <returns>The config file path</returns>
        private static string? IdentifyConfigFileToUse(string? argsConfigFilePath)
        {
            string? configFilePath = null;

            // If the config file path is provided via -c use it
            if (!string.IsNullOrWhiteSpace(argsConfigFilePath))
            {
                Log.Logger.Debug("Using config file provided via CLI: {Path}", argsConfigFilePath);

                // Take the config file path from -c
                configFilePath = argsConfigFilePath;
            }
            // If no -c was provided fall back to the env var called DOTNET_DOCUMENT_CONFIG_FILE
            else if (EnvVar.TryGetConfigFile(out var envVarConfigFilePath))
            {
                Log.Logger.Debug("Using config file provided via env: {Path}", envVarConfigFilePath);

                configFilePath = envVarConfigFilePath;
            }

            return configFilePath;
        }
    }
}
