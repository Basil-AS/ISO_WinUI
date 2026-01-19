using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IMAPI2Lib;
using ModernDisk.Core.Interop;

namespace ModernDisk.Core.Services
{
    public sealed class DiscWriterService
    {
        public sealed class RecorderInfo
        {
            public RecorderInfo(string uniqueId, string displayName)
            {
                UniqueId = uniqueId;
                DisplayName = displayName;
            }

            public string UniqueId { get; }
            public string DisplayName { get; }

            public override string ToString() => DisplayName;
        }

        public sealed class WriteProgress
        {
            public long ElapsedTime { get; init; }
            public long EstimatedTotalTime { get; init; }

            public int PercentComplete => EstimatedTotalTime > 0
                ? (int)((ElapsedTime * 100) / EstimatedTotalTime)
                : 0;
        }

        public IReadOnlyList<RecorderInfo> GetAvailableRecorders()
        {
            var recorders = new List<RecorderInfo>();
            var discMaster = new MsftDiscMaster2();

            foreach (var recorderId in discMaster)
            {
                if (recorderId is not string id || string.IsNullOrWhiteSpace(id))
                    continue;

                var recorder = new MsftDiscRecorder2();
                recorder.InitializeDiscRecorder(id);

                string name = string.Join(" ", new[] { recorder.ManufacturerID, recorder.ProductID })
                    .Trim();

                if (string.IsNullOrWhiteSpace(name))
                    name = "Optical Drive";

                recorders.Add(new RecorderInfo(id, name));
            }

            return recorders;
        }

        public Task BurnIsoAsync(
            string isoPath,
            string recorderId,
            IProgress<WriteProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(isoPath))
                throw new ArgumentException("ISO path is required.", nameof(isoPath));

            if (!File.Exists(isoPath))
                throw new FileNotFoundException("ISO file not found.", isoPath);

            if (string.IsNullOrWhiteSpace(recorderId))
                throw new ArgumentException("Recorder ID is required.", nameof(recorderId));

            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var thread = new Thread(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var recorder = new MsftDiscRecorder2();
                    recorder.InitializeDiscRecorder(recorderId);

                    var dataWriter = new MsftDiscFormat2Data
                    {
                        Recorder = recorder,
                        ClientName = "ModernDiskUtility",
                        VerificationLevel = IMAPI_BURN_VERIFICATION_LEVEL.IMAPI_BURN_VERIFICATION_FULL,
                        BufferUnderrunFreeDisabled = 0
                    };

                    DDiscFormat2DataEvents_OnProgressEvent? handler = null;
                    handler = (object sender, long elapsedTime, long estimatedTotalTime) =>
                    {
                        progress?.Report(new WriteProgress
                        {
                            ElapsedTime = elapsedTime,
                            EstimatedTotalTime = estimatedTotalTime
                        });
                    };

                    dataWriter.OnProgressEvent += handler;

                    try
                    {
                        using var stream = File.OpenRead(isoPath);
                        var comStream = new ComStreamWrapper(stream, () => File.OpenRead(isoPath));
                        dataWriter.Write(comStream);
                    }
                    finally
                    {
                        if (handler != null)
                            dataWriter.OnProgressEvent -= handler;
                    }

                    tcs.TrySetResult(true);
                }
                catch (OperationCanceledException ex)
                {
                    tcs.TrySetCanceled(ex.CancellationToken);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            return tcs.Task;
        }
    }
}
