using Tomlyn;
using Tomlyn.Model;
using System.IO;

namespace KageKirin.UPMConfig
{
    internal static class UPMConfigSerializer
    {
        public static TomlTable Read(string filename)
        {
            var tomlString = File.ReadAllText(filename);
            return Toml.ToModel(tomlString);
        }

        public static void Write(string filename, TomlTable tomlData)
        {
            var tomlString = Toml.FromModel(tomlData);
            File.WriteAllText(filename, tomlString);
        }
    }
} //namespace KageKirin.UPMConfig
