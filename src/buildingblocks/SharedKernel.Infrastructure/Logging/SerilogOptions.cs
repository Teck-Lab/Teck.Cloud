using SharedKernel.Core.Options;

namespace SharedKernel.Infrastructure.Logging
{
    /// <summary>
    /// The serilog options.
    /// </summary>
    public class SerilogOptions : IOptionsRoot
    {
        /// <summary>
        /// Gets or sets the elastic search url.
        /// </summary>
        public Uri ElasticSearchUrl { get; set; } = null!;

        /// <summary>
        /// Gets or sets  a value indicating whether to write converts to file.
        /// </summary>
        public bool WriteToFile { get; set; }

        /// <summary>
        /// Gets or sets the retention file count.
        /// </summary>
        public int RetentionFileCount { get; set; } = 5;

        /// <summary>
        /// Gets or sets a value indicating whether structured console logging.
        /// </summary>
        public bool StructuredConsoleLogging { get; set; }

        /// <summary>
        /// Gets or sets the minimum log level.
        /// </summary>
        public string MinimumLogLevel { get; set; } = "Information";

        /// <summary>
        /// Gets or sets  a value indicating whether to enable erichers.
        /// </summary>
        public bool EnableErichers { get; set; } = true;

        /// <summary>
        /// Gets or sets  a value indicating whether to overide minimum log level.
        /// </summary>
        public bool OverideMinimumLogLevel { get; set; } = true;
    }
}
