using System;
using System.Text;

namespace ES.EditorInternal
{
    public enum ESAgentArtifactImportState : byte
    {
        Applied = 0,
        FailedBeforeWrite = 1,
        RolledBack = 2,
        RollbackUnconfirmed = 3
    }

    public sealed class ESAgentArtifactFileOperation
    {
        public string SourcePath { get; set; }
        public string TargetPath { get; set; }
        public string BackupPath { get; set; }
    }

    public interface IESAgentArtifactFileIO
    {
        bool FileExists(string path);
        void CopyAtomically(string sourcePath, string targetPath);
        void DeleteFile(string path);
        string ComputeSha256(string path);
    }

    public sealed class ESAgentArtifactPhysicalFileIO : IESAgentArtifactFileIO
    {
        public bool FileExists(string path)
        {
            ESAgentArtifactGenerationWorkspace.EnsureProjectReadPath(path);
            return System.IO.File.Exists(path);
        }
        public void CopyAtomically(string sourcePath, string targetPath)
            => ESAgentArtifactGenerationWorkspace.CopyFileAtomically(sourcePath, targetPath);
        public void DeleteFile(string path)
        {
            ESAgentArtifactGenerationWorkspace.EnsureProjectWritePath(path);
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
        public string ComputeSha256(string path)
        {
            ESAgentArtifactGenerationWorkspace.EnsureProjectReadPath(path);
            return ESAgentArtifactGenerationWorkspace.ComputeSha256(path);
        }
    }

    public sealed class ESAgentArtifactImportResult
    {
        public ESAgentArtifactImportState State { get; internal set; }
        public string PrimaryError { get; internal set; }
        public string[] RecoveryErrors { get; internal set; } = Array.Empty<string>();
        public bool Succeeded => State == ESAgentArtifactImportState.Applied;
        public bool RollbackConfirmed => State == ESAgentArtifactImportState.RolledBack;

        public string BuildDiagnostic()
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(PrimaryError)) builder.AppendLine(PrimaryError.Trim());
            if (RecoveryErrors != null && RecoveryErrors.Length > 0)
            {
                builder.AppendLine("恢复核对错误：");
                foreach (string error in RecoveryErrors)
                    if (!string.IsNullOrWhiteSpace(error)) builder.AppendLine("- " + error.Trim());
            }
            return builder.ToString().Trim();
        }
    }
}
