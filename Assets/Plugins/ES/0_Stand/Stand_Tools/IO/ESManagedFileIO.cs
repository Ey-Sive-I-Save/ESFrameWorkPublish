using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ES
{
    /// <summary>
    /// ES 受管文件写入边界：校验根目录、拒绝 junction/symlink，并通过临时文件提升产物。
    /// 不依赖 UnityEditor，Runtime 与 Editor 均可复用；调用方仍必须明确声明允许写入的根目录。
    /// </summary>
    public static class ESManagedFileIO
    {
        private readonly struct FileIdentity
        {
            public readonly long Size;
            public readonly string Sha256;

            public FileIdentity(long size, string sha256)
            {
                Size = size;
                Sha256 = sha256 ?? string.Empty;
            }
        }

        public static string NormalizeFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidDataException("文件路径不能为空。");
            string fullPath = Path.GetFullPath(path.Trim());
            string root = Path.GetPathRoot(fullPath);
            if (!string.IsNullOrEmpty(root) && string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
                return root;
            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static bool IsWithinRoot(string candidate, string root)
        {
            string candidateFull = NormalizeFullPath(candidate);
            string rootFull = NormalizeFullPath(root);
            return string.Equals(candidateFull, rootFull, StringComparison.OrdinalIgnoreCase)
                || candidateFull.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        public static bool ContainsExistingReparsePoint(string path)
        {
            string candidate = NormalizeFullPath(path);
            string root = Path.GetPathRoot(candidate);
            if (string.IsNullOrEmpty(root))
                return false;

            string current = root;
            string relative = candidate.Substring(root.Length);
            foreach (string segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                if (string.IsNullOrEmpty(segment))
                    continue;
                current = Path.Combine(current, segment);
                if (!Directory.Exists(current) && !File.Exists(current))
                    break;
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    return true;
            }
            return false;
        }

        public static void EnsureNoNestedReparsePoints(string directory)
        {
            string root = NormalizeFullPath(directory);
            if (!Directory.Exists(root))
                return;

            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                if (ContainsReparsePoint(current))
                    throw new UnauthorizedAccessException("受管目录不能包含 junction/symlink：" + current);

                foreach (string file in Directory.EnumerateFiles(current, "*", SearchOption.TopDirectoryOnly))
                {
                    if (ContainsReparsePoint(file))
                        throw new UnauthorizedAccessException("受管目录不能包含重解析文件：" + file);
                }

                foreach (string child in Directory.EnumerateDirectories(current, "*", SearchOption.TopDirectoryOnly))
                {
                    if (ContainsReparsePoint(child))
                        throw new UnauthorizedAccessException("受管目录不能包含 junction/symlink：" + child);
                    pending.Push(child);
                }
            }
        }

        /// <summary>不使用 SearchOption.AllDirectories，遇到重解析点立即拒绝并停止遍历。</summary>
        public static IEnumerable<string> EnumerateFilesSafely(string directory, string pattern = "*")
        {
            string root = NormalizeFullPath(directory);
            if (!Directory.Exists(root))
                yield break;

            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                if (ContainsReparsePoint(current))
                    throw new UnauthorizedAccessException("递归扫描不能穿过 junction/symlink：" + current);

                foreach (string file in Directory.EnumerateFiles(current, pattern, SearchOption.TopDirectoryOnly))
                {
                    if (ContainsReparsePoint(file))
                        throw new UnauthorizedAccessException("递归扫描发现重解析文件：" + file);
                    yield return file;
                }

                foreach (string child in Directory.EnumerateDirectories(current, "*", SearchOption.TopDirectoryOnly))
                {
                    if (ContainsReparsePoint(child))
                        throw new UnauthorizedAccessException("递归扫描不能穿过 junction/symlink：" + child);
                    pending.Push(child);
                }
            }
        }

        public static IEnumerable<string> EnumerateDirectoriesSafely(string directory)
        {
            string root = NormalizeFullPath(directory);
            if (!Directory.Exists(root))
                yield break;

            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                if (ContainsReparsePoint(current))
                    throw new UnauthorizedAccessException("递归扫描不能穿过 junction/symlink：" + current);

                foreach (string child in Directory.EnumerateDirectories(current, "*", SearchOption.TopDirectoryOnly))
                {
                    if (ContainsReparsePoint(child))
                        throw new UnauthorizedAccessException("递归扫描不能穿过 junction/symlink：" + child);
                    yield return child;
                    pending.Push(child);
                }
            }
        }

        public static void EnsurePath(string path, bool requireFile = false, params string[] allowedRoots)
        {
            string candidate = NormalizeFullPath(path);
            if (allowedRoots == null || allowedRoots.Length == 0)
                throw new InvalidDataException("必须声明至少一个受管写入根目录。");

            bool inside = false;
            foreach (string root in allowedRoots)
            {
                if (!string.IsNullOrWhiteSpace(root) && IsWithinRoot(candidate, root))
                {
                    inside = true;
                    break;
                }
            }
            if (!inside)
                throw new UnauthorizedAccessException("文件路径越出受管写入根目录：" + path);
            if (ContainsExistingReparsePoint(candidate))
                throw new UnauthorizedAccessException("文件路径不能穿过 junction/symlink：" + path);
            if (requireFile && File.Exists(candidate) && ContainsReparsePoint(candidate))
                throw new UnauthorizedAccessException("目标文件不能是重解析文件：" + path);
        }

        public static void WriteTextAtomic(string path, string text, Encoding encoding = null, params string[] allowedRoots)
        {
            string destination = NormalizeFullPath(path);
            EnsurePath(destination, false, allowedRoots);
            WriteAtomic(destination, true, stream =>
            {
                using (var writer = new StreamWriter(stream, encoding ?? new UTF8Encoding(false), 4096, true))
                    writer.Write(text ?? string.Empty);
            });
        }

        public static void WriteTextAtomicCreateNew(
            string path,
            string text,
            Encoding encoding = null,
            params string[] allowedRoots)
        {
            string destination = NormalizeFullPath(path);
            EnsurePath(destination, false, allowedRoots);
            WriteAtomic(destination, false, stream =>
            {
                using (var writer = new StreamWriter(stream, encoding ?? new UTF8Encoding(false), 4096, true))
                    writer.Write(text ?? string.Empty);
            });
        }

        public static void WriteBytesAtomic(string path, byte[] bytes, params string[] allowedRoots)
        {
            string destination = NormalizeFullPath(path);
            EnsurePath(destination, false, allowedRoots);
            WriteAtomic(destination, true, stream => stream.Write(bytes ?? Array.Empty<byte>(), 0, bytes?.Length ?? 0));
        }

        public static void CopyFileAtomic(string sourcePath, string destinationPath, params string[] allowedRoots)
        {
            string source = NormalizeFullPath(sourcePath);
            string destination = NormalizeFullPath(destinationPath);
            if (!File.Exists(source))
                throw new FileNotFoundException("源文件不存在。", source);
            EnsurePath(source, true, allowedRoots);
            EnsurePath(destination, false, allowedRoots);
            string directory = Path.GetDirectoryName(destination);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidDataException("目标目录无效：" + destinationPath);
            Directory.CreateDirectory(directory);
            EnsurePath(destination, false, allowedRoots);

            FileIdentity sourceIdentity = CaptureFileIdentity(source);
            bool destinationExisted = File.Exists(destination);
            FileIdentity destinationIdentity = destinationExisted ? CaptureFileIdentity(destination) : default;
            string temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.Copy(source, temporary, false);
                FileIdentity temporaryIdentity = CaptureFileIdentity(temporary);
                EnsureFileIdentity(temporary, sourceIdentity, "复制暂存文件与源文件不一致");
                EnsureFileIdentity(source, sourceIdentity, "源文件在复制期间发生变化");
                EnsureDestinationState(destination, destinationExisted, destinationIdentity);
                Promote(temporary, destination, temporaryIdentity, destinationExisted, destinationIdentity);
            }
            finally
            {
                TryDeleteFile(temporary);
            }
        }

        public static void DeleteDirectory(string path, string allowedRoot)
        {
            string directory = NormalizeFullPath(path);
            string root = NormalizeFullPath(allowedRoot);
            if (!IsWithinRoot(directory, root) || string.Equals(directory, root, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("禁止删除受管根目录或越界目录：" + path);
            if (ContainsExistingReparsePoint(directory))
                throw new UnauthorizedAccessException("禁止删除穿过 junction/symlink 的目录：" + path);
            EnsureNoNestedReparsePoints(directory);
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

        public static void DeleteFile(string path, string allowedRoot)
        {
            string file = NormalizeFullPath(path);
            string root = NormalizeFullPath(allowedRoot);
            if (!IsWithinRoot(file, root) || string.Equals(file, root, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("禁止删除受管根目录或越界文件：" + path);
            EnsurePath(file, true, root);
            if (File.Exists(file))
                File.Delete(file);
            if (File.Exists(file))
                throw new IOException("受管文件删除后仍然存在：" + file);
        }

        /// <summary>用于用户通过 SaveFilePanel 明确选择的导出路径；仍拒绝重解析点并原子提升。</summary>
        public static void WriteTextAtUserSelectedPath(string path, string text, Encoding encoding = null)
        {
            string destination = NormalizeFullPath(path);
            string directory = Path.GetDirectoryName(destination);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidDataException("用户选择的导出目录无效。");
            if (ContainsExistingReparsePoint(directory) || (File.Exists(destination) && ContainsReparsePoint(destination)))
                throw new UnauthorizedAccessException("用户选择的导出路径不能穿过 junction/symlink：" + path);
            Directory.CreateDirectory(directory);
            if (ContainsExistingReparsePoint(directory))
                throw new UnauthorizedAccessException("导出目录在创建后变成重解析路径：" + path);
            WriteAtomic(destination, true, stream =>
            {
                using (var writer = new StreamWriter(stream, encoding ?? new UTF8Encoding(false), 4096, true))
                    writer.Write(text ?? string.Empty);
            });
        }

        private static void WriteAtomic(
            string destination,
            bool replaceExisting,
            Action<FileStream> writerAction)
        {
            string directory = Path.GetDirectoryName(destination);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidDataException("目标目录无效：" + destination);
            Directory.CreateDirectory(directory);
            if (ContainsExistingReparsePoint(directory) || (File.Exists(destination) && ContainsReparsePoint(destination)))
                throw new UnauthorizedAccessException("目标路径在写入时出现 junction/symlink：" + destination);

            bool destinationExisted = File.Exists(destination);
            FileIdentity destinationIdentity = destinationExisted ? CaptureFileIdentity(destination) : default;
            if (!replaceExisting && destinationExisted)
                throw new IOException("仅新建写入拒绝覆盖既有文件：" + destination);
            string temporary = Path.Combine(directory, "." + Path.GetFileName(destination) + ".tmp-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    writerAction(stream);
                    // 将用户态缓冲明确推送到文件系统后再提升，避免“写入成功”但进程中断后留下未完整产物。
                    stream.Flush(true);
                }
                FileIdentity temporaryIdentity = CaptureFileIdentity(temporary);
                EnsureDestinationState(destination, destinationExisted, destinationIdentity);
                if (replaceExisting)
                    Promote(temporary, destination, temporaryIdentity, destinationExisted, destinationIdentity);
                else
                {
                    File.Move(temporary, destination);
                    EnsureFileIdentity(destination, temporaryIdentity, "仅新建提交后的最终文件不完整");
                }
            }
            finally
            {
                TryDeleteFile(temporary);
            }
        }

        private static void Promote(
            string temporary,
            string destination,
            FileIdentity expectedNewIdentity,
            bool destinationExisted,
            FileIdentity expectedDestinationIdentity)
        {
            string directory = Path.GetDirectoryName(destination);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidDataException("目标目录无效：" + destination);
            if (ContainsExistingReparsePoint(directory)
                || (File.Exists(destination) && ContainsReparsePoint(destination)))
                throw new UnauthorizedAccessException("提交路径在提升时出现 junction/symlink：" + destination);
            EnsureDestinationState(destination, destinationExisted, expectedDestinationIdentity);

            if (!destinationExisted)
            {
                File.Move(temporary, destination);
                EnsureFileIdentity(destination, expectedNewIdentity, "首次提交后的最终文件不完整");
                return;
            }

            try
            {
                File.Replace(temporary, destination, null);
            }
            catch (PlatformNotSupportedException)
            {
                PromoteWithBackup(temporary, destination, expectedNewIdentity, expectedDestinationIdentity);
            }
            catch (IOException)
            {
                PromoteWithBackup(temporary, destination, expectedNewIdentity, expectedDestinationIdentity);
            }
            catch (UnauthorizedAccessException)
            {
                PromoteWithBackup(temporary, destination, expectedNewIdentity, expectedDestinationIdentity);
            }
            EnsureFileIdentity(destination, expectedNewIdentity, "原子替换后的最终文件不完整");
        }

        /// <summary>
        /// File.Replace 不可用时的受控降级。绝不使用 File.Copy(..., true) 覆盖目标：
        /// 先把旧文件移动为同目录备份，只有新文件移动成功后才清理备份；移动失败则尝试原样恢复旧文件。
        /// </summary>
        private static void PromoteWithBackup(
            string temporary,
            string destination,
            FileIdentity expectedNewIdentity,
            FileIdentity expectedDestinationIdentity)
        {
            string directory = Path.GetDirectoryName(destination);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidDataException("目标目录无效：" + destination);
            if (!File.Exists(temporary))
                throw new FileNotFoundException("临时文件不存在，不能提交。", temporary);
            if (!File.Exists(destination))
                throw new IOException("替换提交的原目标在备份前消失，拒绝覆盖可能的并发修改：" + destination);
            if (ContainsExistingReparsePoint(directory) || ContainsReparsePoint(destination))
                throw new UnauthorizedAccessException("备份提交不能穿过 junction/symlink：" + destination);
            EnsureFileIdentity(destination, expectedDestinationIdentity, "原目标在备份前发生变化");

            string backup = Path.Combine(directory, "." + Path.GetFileName(destination) + ".backup-" + Guid.NewGuid().ToString("N"));
            File.Move(destination, backup);
            EnsureFileIdentity(backup, expectedDestinationIdentity, "旧目标备份后身份不一致");
            bool committed = false;
            try
            {
                File.Move(temporary, destination);
                if (!File.Exists(destination) || ContainsReparsePoint(destination))
                    throw new IOException("临时文件提升后目标文件不可用：" + destination);
                EnsureFileIdentity(destination, expectedNewIdentity, "备份提交后的最终文件不完整");
                committed = true;
            }
            catch (Exception commitException)
            {
                try
                {
                    if (File.Exists(destination))
                        throw new IOException("提交失败后目标路径已被其他进程重新创建，拒绝覆盖外部修改：" + destination);
                    if (!File.Exists(backup))
                        throw new FileNotFoundException("提交失败后旧目标备份丢失。", backup);
                    EnsureFileIdentity(backup, expectedDestinationIdentity, "恢复前旧目标备份已被修改");
                    File.Move(backup, destination);
                    EnsureFileIdentity(destination, expectedDestinationIdentity, "恢复后的旧目标身份不一致");
                }
                catch (Exception restoreException)
                {
                    throw new AggregateException("文件提交失败且旧文件恢复失败。", commitException, restoreException);
                }

                throw;
            }
            finally
            {
                if (committed && File.Exists(backup))
                {
                    try
                    {
                        File.Delete(backup);
                        if (File.Exists(backup))
                            throw new IOException("提交成功后旧文件备份未能删除：" + backup);
                    }
                    catch (Exception cleanupException)
                    {
                        throw new IOException("新文件已完成提交，但旧文件备份清理失败；已保留现场：" + backup, cleanupException);
                    }
                }
            }
        }

        private static FileIdentity CaptureFileIdentity(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("无法读取不存在文件的身份。", path);
            if (ContainsReparsePoint(path))
                throw new UnauthorizedAccessException("不能为重解析文件创建身份快照：" + path);

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 hash = SHA256.Create())
            {
                byte[] bytes = hash.ComputeHash(stream);
                return new FileIdentity(stream.Length, BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant());
            }
        }

        private static void EnsureFileIdentity(string path, FileIdentity expected, string context)
        {
            FileIdentity actual = CaptureFileIdentity(path);
            if (actual.Size != expected.Size || !string.Equals(actual.Sha256, expected.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new IOException(context + "：" + path);
        }

        private static void EnsureDestinationState(string destination, bool expectedToExist, FileIdentity expectedIdentity)
        {
            bool exists = File.Exists(destination);
            if (Directory.Exists(destination))
                throw new IOException("目标路径已被目录占用：" + destination);
            if (exists != expectedToExist)
                throw new IOException("目标文件在提交前存在状态发生变化，拒绝覆盖并发修改：" + destination);
            if (exists)
                EnsureFileIdentity(destination, expectedIdentity, "目标文件在提交前身份发生变化");
        }

        private static bool ContainsReparsePoint(string path)
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // 不覆盖原始异常；调用方可在下一次门禁中清理残留暂存文件。
            }
        }
    }

}
