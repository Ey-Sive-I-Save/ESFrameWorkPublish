using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.Hosting.Unity
{
    internal static class ConfigurationBuilderExtensions
    {
        /// <summary>
        /// Disables the default <see cref="FileConfigurationSource"/> behavior.
        /// </summary>
        /// <note>
        /// File system watcher drastically increases Unity Editor enter playmode time. Better keep ReloadOnChange turned off
        /// </note>
        /// <param name="builder"></param>
        public static void DisableFileConfigurationSourceReloadOnChange(this IConfigurationBuilder builder)
        {
            foreach (var source in builder.Sources)
            {
                if (source is FileConfigurationSource f)
                    f.ReloadOnChange = false;
            }
        }
    }
}