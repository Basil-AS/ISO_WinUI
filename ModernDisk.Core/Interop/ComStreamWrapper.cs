using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace ModernDisk.Core.Interop
{
    public sealed class ComStreamWrapper : IStream
    {
        private readonly Stream _stream;
        private readonly Func<Stream>? _cloneFactory;

        public ComStreamWrapper(Stream stream, Func<Stream>? cloneFactory = null)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _cloneFactory = cloneFactory;
        }

        public void Read(byte[] pv, int cb, IntPtr pcbRead)
        {
            int read = _stream.Read(pv, 0, cb);
            if (pcbRead != IntPtr.Zero)
                Marshal.WriteInt32(pcbRead, read);
        }

        public void Write(byte[] pv, int cb, IntPtr pcbWritten)
        {
            _stream.Write(pv, 0, cb);
            if (pcbWritten != IntPtr.Zero)
                Marshal.WriteInt32(pcbWritten, cb);
        }

        public void Seek(long dlibMove, int dwOrigin, IntPtr plibNewPosition)
        {
            long pos = _stream.Seek(dlibMove, (SeekOrigin)dwOrigin);
            if (plibNewPosition != IntPtr.Zero)
                Marshal.WriteInt64(plibNewPosition, pos);
        }

        public void SetSize(long libNewSize) => _stream.SetLength(libNewSize);

        public void CopyTo(IStream pstm, long cb, IntPtr pcbRead, IntPtr pcbWritten)
        {
            const int bufferSize = 81920;
            byte[] buffer = new byte[bufferSize];
            long remaining = cb;
            int totalRead = 0;
            int totalWritten = 0;

            while (remaining > 0)
            {
                int toRead = remaining > bufferSize ? bufferSize : (int)remaining;
                int read = _stream.Read(buffer, 0, toRead);
                if (read == 0)
                    break;

                totalRead += read;
                remaining -= read;

                pstm.Write(buffer, read, IntPtr.Zero);
                totalWritten += read;
            }

            if (pcbRead != IntPtr.Zero)
                Marshal.WriteInt32(pcbRead, totalRead);
            if (pcbWritten != IntPtr.Zero)
                Marshal.WriteInt32(pcbWritten, totalWritten);
        }

        public void Commit(int grfCommitFlags) => _stream.Flush();

        public void Revert() => throw new NotSupportedException();

        public void LockRegion(long libOffset, long cb, int dwLockType) { }

        public void UnlockRegion(long libOffset, long cb, int dwLockType) { }

        public void Stat(out STATSTG pstatstg, int grfStatFlag)
        {
            pstatstg = new STATSTG
            {
                cbSize = _stream.Length,
                type = 2,
                grfMode = 0,
                grfLocksSupported = 0,
                clsid = Guid.Empty
            };
        }

        public void Clone(out IStream ppstm)
        {
            if (_cloneFactory == null)
                throw new NotSupportedException("Clone is not supported for this stream.");

            ppstm = new ComStreamWrapper(_cloneFactory(), _cloneFactory);
        }
    }
}
