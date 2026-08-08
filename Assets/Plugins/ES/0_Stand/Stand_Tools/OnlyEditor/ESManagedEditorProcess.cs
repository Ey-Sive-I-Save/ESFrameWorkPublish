#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ES
{
    /// <summary>所有受管外部进程必须实现的最小生命周期接口。</summary>
    public interface IESManagedProcessHandle : IDisposable
    {
        bool HasExited { get; }
        void Terminate();
    }

    /// <summary>
    /// 受管进程注册表。它不负责业务报告，只负责在域重载或宿主关闭前找到所有仍存活的句柄。
    /// </summary>
    public static class ESManagedProcessRegistry
    {
        private static readonly object sync = new object();
        private static readonly HashSet<IESManagedProcessHandle> active = new HashSet<IESManagedProcessHandle>();

        public static int ActiveCount
        {
            get { lock (sync) return active.Count; }
        }

        public static void Register(IESManagedProcessHandle handle)
        {
            if (handle == null) throw new ArgumentNullException(nameof(handle));
            lock (sync) active.Add(handle);
        }

        public static void Unregister(IESManagedProcessHandle handle)
        {
            if (handle == null) return;
            lock (sync) active.Remove(handle);
        }

        public static void TerminateAll()
        {
            IESManagedProcessHandle[] snapshot;
            lock (sync) snapshot = new List<IESManagedProcessHandle>(active).ToArray();
            List<Exception> failures = null;
            foreach (IESManagedProcessHandle handle in snapshot)
            {
                try
                {
                    if (!handle.HasExited) handle.Terminate();
                }
                catch (Exception exception)
                {
                    if (failures == null) failures = new List<Exception>();
                    failures.Add(exception);
                }
                finally
                {
                    try { handle.Dispose(); }
                    catch (Exception exception)
                    {
                        if (failures == null) failures = new List<Exception>();
                        failures.Add(exception);
                    }
                }
            }
            if (failures != null && failures.Count > 0)
                throw new AggregateException("域重载前仍有受管进程未能确认终止。", failures);
        }
    }

    public static class ESManagedProcessOutput
    {
        public const int MaximumCapturedCharacters = 4 * 1024 * 1024;
        public const int DrainTimeoutMilliseconds = 10000;

        public static async Task<string> ReadToEndBoundedAsync(StreamReader reader)
        {
            if (reader == null) return string.Empty;
            var builder = new StringBuilder();
            var buffer = new char[8192];
            int total = 0;
            while (true)
            {
                int read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                if (read <= 0) break;
                total += read;
                if (total > MaximumCapturedCharacters)
                    throw new InvalidDataException("受管进程输出超过 4 MiB 上限，拒绝继续占用 Editor 内存。");
                builder.Append(buffer, 0, read);
            }
            return builder.ToString();
        }

        public static string GetResult(Task<string> task, string streamName)
        {
            if (task == null) return string.Empty;
            if (!task.Wait(DrainTimeoutMilliseconds))
                throw new TimeoutException("受管进程 " + streamName + " 在 10 秒内未完成排空，可能存在脱离子进程持有管道句柄。");
            return task.GetAwaiter().GetResult();
        }
    }

    public static class ESManagedProcessTree
    {
#if UNITY_EDITOR_WIN
        private const int JobObjectExtendedLimitInformationClass = 9;
        private const uint JobObjectLimitKillOnJobClose = 0x00002000;

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicLimitInformation
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectExtendedLimitInformation
        {
            public JobObjectBasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr jobAttributes, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint length);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateJobObject(IntPtr job, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        private sealed class JobScope : IDisposable
        {
            private IntPtr handle;

            public JobScope(IntPtr value)
            {
                handle = value;
            }

            public bool TryTerminate()
                => handle != IntPtr.Zero && TerminateJobObject(handle, 1);

            public void Dispose()
            {
                if (handle == IntPtr.Zero) return;
                CloseHandle(handle);
                handle = IntPtr.Zero;
            }
        }
#endif

        public static IDisposable Attach(Process process)
        {
#if UNITY_EDITOR_WIN
            if (process == null || process.HasExited) return null;
            IntPtr job = CreateJobObject(IntPtr.Zero, null);
            if (job == IntPtr.Zero) return null;
            bool transferred = false;
            try
            {
                var limits = new JobObjectExtendedLimitInformation
                {
                    BasicLimitInformation = new JobObjectBasicLimitInformation
                    {
                        LimitFlags = JobObjectLimitKillOnJobClose,
                    },
                };
                IntPtr buffer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(JobObjectExtendedLimitInformation)));
                try
                {
                    Marshal.StructureToPtr(limits, buffer, false);
                    if (!SetInformationJobObject(job, JobObjectExtendedLimitInformationClass,
                            buffer, (uint)Marshal.SizeOf(typeof(JobObjectExtendedLimitInformation))))
                        return null;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }

                if (!AssignProcessToJobObject(job, process.Handle)) return null;
                transferred = true;
                return new JobScope(job);
            }
            finally
            {
                if (!transferred) CloseHandle(job);
            }
#else
            return null;
#endif
        }

        public static void Terminate(Process process, IDisposable attachment)
        {
            if (process == null || process.HasExited) return;
#if UNITY_EDITOR_WIN
            if (attachment is JobScope job && job.TryTerminate())
            {
                process.WaitForExit(5000);
                if (process.HasExited) return;
            }

            bool taskKillConfirmed = false;
            string taskKillPath = Path.Combine(Environment.SystemDirectory, "taskkill.exe");
            if (File.Exists(taskKillPath))
            {
                using (var killer = new Process())
                {
                    killer.StartInfo = new ProcessStartInfo
                    {
                        FileName = taskKillPath,
                        Arguments = "/PID " + process.Id + " /T /F",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };
                    if (killer.Start() && killer.WaitForExit(5000) && killer.ExitCode == 0)
                    {
                        taskKillConfirmed = true;
                        process.WaitForExit(5000);
                        if (process.HasExited) return;
                    }
                }
            }
#endif
            process.Kill();
            if (!process.WaitForExit(5000) && !process.HasExited)
                throw new InvalidOperationException("受管进程树终止未确认：PID=" + process.Id);
#if UNITY_EDITOR_WIN
            if (!taskKillConfirmed && !(attachment is JobScope))
                throw new InvalidOperationException("受管进程树缺少 Job Object，且 taskkill 未确认成功：PID=" + process.Id);
#endif
        }
    }

    /// <summary>ES Stand 层唯一的受管 Editor 外部进程句柄。</summary>
    public sealed class ESManagedEditorProcess : IESManagedProcessHandle
    {
        public const int DefaultTimeoutSeconds = 120;
        public const int MaximumTimeoutSeconds = 1800;

        private readonly object lifecycleSync = new object();
        private Process process;
        private readonly DateTimeOffset startedAtUtc;
        private readonly int timeoutSeconds;
        private readonly IDisposable processTreeScope;
        private readonly Task<string> standardOutputTask;
        private readonly Task<string> standardErrorTask;

        internal ESManagedEditorProcess(Process process, int timeoutSeconds)
        {
            this.process = process ?? throw new ArgumentNullException(nameof(process));
            if (timeoutSeconds < 1 || timeoutSeconds > MaximumTimeoutSeconds)
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "Editor 进程超时必须位于 1–1800 秒。");
            this.timeoutSeconds = timeoutSeconds;
            startedAtUtc = DateTimeOffset.UtcNow;
            processTreeScope = ESManagedProcessTree.Attach(process);
            if (process.StartInfo.RedirectStandardOutput)
                standardOutputTask = ESManagedProcessOutput.ReadToEndBoundedAsync(process.StandardOutput);
            if (process.StartInfo.RedirectStandardError)
                standardErrorTask = ESManagedProcessOutput.ReadToEndBoundedAsync(process.StandardError);
            ESManagedProcessRegistry.Register(this);
        }

        public bool HasExited
        {
            get
            {
                lock (lifecycleSync) return process == null || process.HasExited;
            }
        }
        public bool HasJobObject => processTreeScope != null;
        public bool HasTimedOut(DateTimeOffset nowUtc)
            => !HasExited && nowUtc - startedAtUtc > TimeSpan.FromSeconds(timeoutSeconds);

        public bool WaitForExit(int milliseconds)
        {
            Process current;
            lock (lifecycleSync) current = process;
            return current != null && current.WaitForExit(milliseconds);
        }

        public bool TryGetExitCode(out int exitCode)
        {
            exitCode = -1;
            Process current;
            lock (lifecycleSync) current = process;
            if (current == null || !current.HasExited) return false;
            exitCode = current.ExitCode;
            return true;
        }

        public string ReadStandardOutputToEnd()
            => ESManagedProcessOutput.GetResult(standardOutputTask, "stdout");

        public string ReadStandardErrorToEnd()
            => ESManagedProcessOutput.GetResult(standardErrorTask, "stderr");

        public void Terminate()
        {
            lock (lifecycleSync)
            {
                if (process == null || process.HasExited) return;
                ESManagedProcessTree.Terminate(process, processTreeScope);
            }
        }

        public void Dispose()
        {
            lock (lifecycleSync)
            {
                if (process == null) return;
                if (!process.HasExited) Terminate();
                ESManagedProcessRegistry.Unregister(this);
                processTreeScope?.Dispose();
                process.Dispose();
                process = null;
            }
        }
    }

    public static class ESManagedEditorProcessRunner
    {
        public static ESManagedEditorProcess StartPythonProbe(string interpreterPath, string projectRoot,
            int timeoutSeconds)
        {
            if (string.IsNullOrWhiteSpace(interpreterPath))
                throw new ArgumentException("Python 解释器路径不能为空。", nameof(interpreterPath));
            return StartValidated(new ProcessStartInfo
            {
                FileName = interpreterPath,
                Arguments = "--version",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            }, projectRoot, timeoutSeconds);
        }

        private static ESManagedEditorProcess StartValidated(ProcessStartInfo startInfo, string projectRoot,
            int timeoutSeconds)
        {
#if !UNITY_EDITOR
                throw new PlatformNotSupportedException("ES 受管 Editor 进程入口只能在 Unity Editor 中运行。");
#else
            if (startInfo == null) throw new ArgumentNullException(nameof(startInfo));
            if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentException("项目根目录不能为空。", nameof(projectRoot));
            if (timeoutSeconds < 1 || timeoutSeconds > ESManagedEditorProcess.MaximumTimeoutSeconds)
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "Editor 进程超时必须位于 1–1800 秒。");
            if (startInfo.UseShellExecute) throw new InvalidOperationException("受管 Editor 进程禁止 Shell 执行。");
            if (string.IsNullOrWhiteSpace(startInfo.FileName)) throw new InvalidOperationException("受管 Editor 进程缺少可执行入口。");
            if (!startInfo.RedirectStandardOutput || !startInfo.RedirectStandardError)
                throw new InvalidOperationException("受管 Editor 进程必须同时重定向 stdout/stderr。");

            string executable = Path.GetFullPath(startInfo.FileName);
            if (!File.Exists(executable)) throw new FileNotFoundException("受管 Editor 可执行入口不存在。", executable);
            if (ESManagedFileIO.ContainsExistingReparsePoint(executable))
                throw new UnauthorizedAccessException("受管 Editor 可执行入口不能穿过 junction 或 symlink。");
            string root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(startInfo.WorkingDirectory))
                throw new InvalidOperationException("受管 Editor 进程必须声明工作目录。");
            string workingDirectory = Path.GetFullPath(startInfo.WorkingDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(workingDirectory)
                || (!string.Equals(workingDirectory, root, StringComparison.OrdinalIgnoreCase)
                    && !workingDirectory.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                throw new UnauthorizedAccessException("受管 Editor 进程工作目录必须位于项目根目录内。");
            if (ESManagedFileIO.ContainsExistingReparsePoint(workingDirectory))
                throw new UnauthorizedAccessException("受管 Editor 进程工作目录不能穿过 junction 或 symlink。");

            startInfo.FileName = executable;
            startInfo.WorkingDirectory = workingDirectory;
            Process process = new Process { StartInfo = startInfo };
            bool started = false;
            try
            {
                if (!process.Start()) throw new InvalidOperationException("受管 Editor 进程未能启动。");
                started = true;
                return new ESManagedEditorProcess(process, timeoutSeconds);
            }
            catch
            {
                Exception cleanupFailure = null;
                try
                {
                    if (started && !process.HasExited) ESManagedProcessTree.Terminate(process, null);
                }
                catch (Exception exception) { cleanupFailure = exception; }
                process.Dispose();
                if (cleanupFailure != null)
                    throw new AggregateException("受管 Editor 进程启动后初始化失败，且进程树终止未确认。", cleanupFailure);
                throw;
            }
#endif
        }

        public static ESManagedEditorProcess StartPowerShell(ProcessStartInfo startInfo, string projectRoot,
            int timeoutSeconds)
        {
#if !UNITY_EDITOR_WIN
            throw new PlatformNotSupportedException("ES 受管 PowerShell 入口当前只支持 Windows Editor。");
#else
            if (startInfo == null) throw new ArgumentNullException(nameof(startInfo));
            if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentException("项目根目录不能为空。", nameof(projectRoot));
            if (timeoutSeconds < 1 || timeoutSeconds > ESManagedEditorProcess.MaximumTimeoutSeconds)
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "Editor 进程超时必须位于 1–1800 秒。");
            if (startInfo.UseShellExecute) throw new InvalidOperationException("受管 Editor PowerShell 禁止 Shell 执行。");
            if (string.IsNullOrWhiteSpace(startInfo.FileName)) throw new InvalidOperationException("受管 Editor PowerShell 缺少解释器路径。");
            if (!startInfo.RedirectStandardOutput || !startInfo.RedirectStandardError)
                throw new InvalidOperationException("受管 Editor PowerShell 必须同时重定向 stdout/stderr。");

            string expected = Path.GetFullPath(Path.Combine(Environment.SystemDirectory, "powershell.exe"));
            string actual = Path.GetFullPath(startInfo.FileName);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("受管 Editor PowerShell 只能使用系统 powershell.exe。");
            if (string.IsNullOrWhiteSpace(startInfo.WorkingDirectory))
                throw new InvalidOperationException("受管 Editor PowerShell 必须声明工作目录。");

            string root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string workingDirectory = Path.GetFullPath(startInfo.WorkingDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(workingDirectory)
                || (!string.Equals(workingDirectory, root, StringComparison.OrdinalIgnoreCase)
                    && !workingDirectory.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                throw new UnauthorizedAccessException("受管 Editor PowerShell 工作目录必须位于项目根目录内。");
            if (ESManagedFileIO.ContainsExistingReparsePoint(workingDirectory))
                throw new UnauthorizedAccessException("受管 Editor PowerShell 工作目录不能穿过 junction/symlink。");

            startInfo.FileName = expected;
            startInfo.WorkingDirectory = workingDirectory;
            Process process = new Process { StartInfo = startInfo };
            bool started = false;
            try
            {
                if (!process.Start()) throw new InvalidOperationException("受管 Editor PowerShell 未能启动。");
                started = true;
                return new ESManagedEditorProcess(process, timeoutSeconds);
            }
            catch
            {
                Exception cleanupFailure = null;
                try
                {
                    if (started && !process.HasExited) ESManagedProcessTree.Terminate(process, null);
                }
                catch (Exception exception) { cleanupFailure = exception; }
                process.Dispose();
                if (cleanupFailure != null)
                    throw new AggregateException("受管 Editor PowerShell 启动后初始化失败，且进程树终止未确认。", cleanupFailure);
                throw;
            }
#endif
        }
    }
}
#endif
