using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils.Iso9660;

namespace ModernDisk.Core.Services
{
    public sealed class IsoBuilderService
    {
        public sealed class BuildProgress
        {
            public int TotalFiles { get; init; }
            public int ProcessedFiles { get; init; }
            public string CurrentFile { get; init; } = string.Empty;
            public long TotalBytes { get; init; }
            public long ProcessedBytes { get; init; }
        }

        public Task<string> CreateIsoFileAsync(
            string sourceDirectory,
            string outputPath,
            string volumeLabel,
            IProgress<BuildProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory))
                throw new ArgumentException("Source directory is required.", nameof(sourceDirectory));

            if (!Directory.Exists(sourceDirectory))
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");

            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path is required.", nameof(outputPath));

            string label = (volumeLabel ?? string.Empty).Trim();
            if (label.Length == 0)
                label = "DATA";

            if (label.Length > 32)
                label = label[..32];

            label = label.ToUpperInvariant();

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var allFiles = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
                long totalBytes = 0;
                foreach (var file in allFiles)
                {
                    totalBytes += new FileInfo(file).Length;
                }

                var builder = new CDBuilder
                {
                    UseJoliet = true,
                    VolumeIdentifier = label
                };

                var state = new ProgressState(allFiles, totalBytes);
                AddDirectoryToIso(sourceDirectory, sourceDirectory, builder, state, progress, cancellationToken);

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
                using var fs = File.Create(outputPath);
                builder.Build(fs);

                return outputPath;
            }, cancellationToken);
        }

        private sealed class ProgressState
        {
            public ProgressState(string[] files, long totalBytes)
            {
                Files = files;
                TotalBytes = totalBytes;
            }

            public string[] Files { get; }
            public long TotalBytes { get; }
            public int ProcessedFiles { get; set; }
            public long ProcessedBytes { get; set; }
        }

        private static void AddDirectoryToIso(
            string sourcePath,
            string baseDirectory,
            CDBuilder builder,
            ProgressState state,
            IProgress<BuildProgress>? progress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var directory in Directory.GetDirectories(sourcePath))
            {
                string relativeDir = Path.GetRelativePath(baseDirectory, directory)
                    .Replace("\\", "/");

                string isoDir = "/" + relativeDir.ToUpperInvariant();
                builder.AddDirectory(isoDir);

                AddDirectoryToIso(directory, baseDirectory, builder, state, progress, cancellationToken);
            }

            foreach (var file in Directory.GetFiles(sourcePath))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string relativePath = Path.GetRelativePath(baseDirectory, file)
                    .Replace("\\", "/");

                string isoPath = "/" + relativePath.ToUpperInvariant();
                builder.AddFile(file, isoPath);

                var info = new FileInfo(file);
                state.ProcessedFiles += 1;
                state.ProcessedBytes += info.Length;

                progress?.Report(new BuildProgress
                {
                    TotalFiles = state.Files.Length,
                    ProcessedFiles = state.ProcessedFiles,
                    CurrentFile = info.Name,
                    TotalBytes = state.TotalBytes,
                    ProcessedBytes = state.ProcessedBytes
                });
            }
        }
    }
}
