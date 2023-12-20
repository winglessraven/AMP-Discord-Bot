using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBotPlugin
{
    internal class ResourceReader
    {
        public string ReadResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();

            // Resource name should include namespace
            string resourcePath = $"{assembly.GetName().Name}.{resourceName}";

            using (Stream stream = assembly.GetManifestResourceStream(resourcePath))
            {
                if (stream == null)
                    throw new FileNotFoundException($"Resource {resourceName} not found.");

                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }

    }
}
