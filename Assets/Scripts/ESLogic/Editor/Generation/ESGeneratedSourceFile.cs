using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Shared editor-only writer for deterministic ES generated C# files. Generators provide the
    /// complete text; this boundary owns path validation, content comparison, atomic replacement,
    /// and AssetDatabase import. It deliberately never edits authored GameCore data.
    /// </summary>
    internal static class ESGeneratedSourceFile
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        public static bool TryEnsureCurrent(
            string assetPath,
            string expectedContent,
            bool importAsset,
            out bool changed,
            out string error)
        {
            changed = false;
            error = null;
            if (!TryGetAbsolutePath(assetPath, out string absolutePath, out error))
                return false;

            string normalizedExpected = Normalize(expectedContent);
            string temporaryPath = null;
            try
            {
                if (File.Exists(absolutePath))
                {
                    string current = Normalize(File.ReadAllText(absolutePath, Utf8WithoutBom));
                    if (string.Equals(current, normalizedExpected, StringComparison.Ordinal))
                        return true;
                }

                string directory = Path.GetDirectoryName(absolutePath);
                if (string.IsNullOrEmpty(directory))
                {
                    error = "Generated source path has no parent directory: " + assetPath;
                    return false;
                }

                Directory.CreateDirectory(directory);
                temporaryPath = absolutePath + ".tmp";
                File.WriteAllText(temporaryPath, normalizedExpected, Utf8WithoutBom);
                ReplaceFile(temporaryPath, absolutePath);
                changed = true;

                if (importAsset)
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                return true;
            }
            catch (Exception exception)
            {
                error = "Failed to write generated source " + assetPath + ": " + exception.Message;
                return false;
            }
            finally
            {
                if (!string.IsNullOrEmpty(temporaryPath) && File.Exists(temporaryPath))
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch (IOException)
                    {
                        // A failed cleanup must not hide the original generation failure.
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // The next explicit generation attempt can safely replace this temp file.
                    }
                }
            }
        }

        public static bool IsCurrent(string assetPath, string expectedContent, out string error)
        {
            if (!TryGetAbsolutePath(assetPath, out string absolutePath, out error))
                return false;

            if (!File.Exists(absolutePath))
            {
                error = "Generated source is missing: " + assetPath;
                return false;
            }

            try
            {
                string current = File.ReadAllText(absolutePath, Utf8WithoutBom);
                if (!MatchesExpectedContent(current, expectedContent))
                {
                    error = "Generated source is stale: " + assetPath + "。请先执行“生成角色固定属性代码”，等待 Unity 编译完成后再 Bake。";
                    return false;
                }
            }
            catch (Exception exception)
            {
                error = "Cannot read generated source " + assetPath + ": " + exception.Message;
                return false;
            }

            error = null;
            return true;
        }

        internal static bool MatchesExpectedContent(string currentContent, string expectedContent)
        {
            return string.Equals(Normalize(currentContent), Normalize(expectedContent), StringComparison.Ordinal);
        }

        private static bool TryGetAbsolutePath(string assetPath, out string absolutePath, out string error)
        {
            absolutePath = null;
            error = null;
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = "Generated source must stay below Assets/: " + (assetPath ?? "<null>");
                return false;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            absolutePath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            string requiredPrefix = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                    + Path.DirectorySeparatorChar;
            if (!absolutePath.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
            {
                error = "Generated source escapes the project root: " + assetPath;
                absolutePath = null;
                return false;
            }

            return true;
        }

        private static void ReplaceFile(string temporaryPath, string destinationPath)
        {
            if (!File.Exists(destinationPath))
            {
                File.Move(temporaryPath, destinationPath);
                return;
            }

            try
            {
                File.Replace(temporaryPath, destinationPath, null);
            }
            catch (PlatformNotSupportedException)
            {
                CopyThenDeleteTemporary(temporaryPath, destinationPath);
            }
            catch (IOException)
            {
                // File.Replace is unavailable on some Unity/editor filesystem combinations.
                // The generated target is still wholly inside Assets and was already validated.
                CopyThenDeleteTemporary(temporaryPath, destinationPath);
            }
            catch (UnauthorizedAccessException)
            {
                CopyThenDeleteTemporary(temporaryPath, destinationPath);
            }
        }

        private static void CopyThenDeleteTemporary(string temporaryPath, string destinationPath)
        {
            File.Copy(temporaryPath, destinationPath, true);
            File.Delete(temporaryPath);
        }

        private static string Normalize(string content)
        {
            string normalized = (content ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");
            return normalized.EndsWith("\n", StringComparison.Ordinal) ? normalized : normalized + "\n";
        }
    }
}
