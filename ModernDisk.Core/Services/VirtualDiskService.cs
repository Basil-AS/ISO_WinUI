using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace ModernDisk.Core.Services
{
    public sealed class VirtualDiskService
    {
        private const uint VirtualStorageTypeDeviceIso = 1;
        private static readonly Guid VirtualStorageTypeVendorMicrosoft =
            new("EC984AEC-A0A9-44e7-B6B6-1DED8D2EA4E1");

        [Flags]
        private enum VirtualDiskAccessMask : uint
        {
            Read = 0x00010000,
            Detach = 0x00040000
        }

        [Flags]
        private enum OpenVirtualDiskFlag : uint
        {
            None = 0
        }

        [Flags]
        private enum AttachVirtualDiskFlag : uint
        {
            None = 0,
            ReadOnly = 1,
            PermanentLifetime = 4
        }

        [Flags]
        private enum DetachVirtualDiskFlag : uint
        {
            None = 0
        }

        private enum OpenVirtualDiskVersion : uint
        {
            Version1 = 1
        }

        private enum AttachVirtualDiskVersion : uint
        {
            Version1 = 1
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VirtualStorageType
        {
            public uint DeviceId;
            public Guid VendorId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct OpenVirtualDiskParameters
        {
            public OpenVirtualDiskVersion Version;
            public OpenVirtualDiskParametersV1 Version1;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct OpenVirtualDiskParametersV1
        {
            public uint RWDepth;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AttachVirtualDiskParameters
        {
            public AttachVirtualDiskVersion Version;
            public AttachVirtualDiskParametersV1 Version1;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AttachVirtualDiskParametersV1
        {
            public uint Reserved;
        }

        [DllImport("virtdisk.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint OpenVirtualDisk(
            ref VirtualStorageType virtualStorageType,
            string path,
            VirtualDiskAccessMask virtualDiskAccessMask,
            OpenVirtualDiskFlag flags,
            ref OpenVirtualDiskParameters parameters,
            out IntPtr handle);

        [DllImport("virtdisk.dll", SetLastError = true)]
        private static extern uint AttachVirtualDisk(
            IntPtr virtualDiskHandle,
            IntPtr securityDescriptor,
            AttachVirtualDiskFlag flags,
            uint providerSpecificFlags,
            ref AttachVirtualDiskParameters parameters,
            IntPtr overlapped);

        [DllImport("virtdisk.dll", SetLastError = true)]
        private static extern uint DetachVirtualDisk(
            IntPtr virtualDiskHandle,
            DetachVirtualDiskFlag flags,
            uint providerSpecificFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        public void MountIso(string isoPath)
        {
            if (string.IsNullOrWhiteSpace(isoPath))
                throw new ArgumentException("ISO path is required.", nameof(isoPath));

            if (!File.Exists(isoPath))
                throw new FileNotFoundException("ISO file not found.", isoPath);

            var storageType = new VirtualStorageType
            {
                DeviceId = VirtualStorageTypeDeviceIso,
                VendorId = VirtualStorageTypeVendorMicrosoft
            };

            var openParameters = new OpenVirtualDiskParameters
            {
                Version = OpenVirtualDiskVersion.Version1,
                Version1 = new OpenVirtualDiskParametersV1 { RWDepth = 1 }
            };

            uint openResult = OpenVirtualDisk(
                ref storageType,
                isoPath,
                VirtualDiskAccessMask.Read,
                OpenVirtualDiskFlag.None,
                ref openParameters,
                out IntPtr handle);

            if (openResult != 0)
                throw CreateWin32Exception("OpenVirtualDisk", openResult);

            try
            {
                var attachParameters = new AttachVirtualDiskParameters
                {
                    Version = AttachVirtualDiskVersion.Version1,
                    Version1 = new AttachVirtualDiskParametersV1 { Reserved = 0 }
                };

                uint attachResult = AttachVirtualDisk(
                    handle,
                    IntPtr.Zero,
                    AttachVirtualDiskFlag.ReadOnly | AttachVirtualDiskFlag.PermanentLifetime,
                    0,
                    ref attachParameters,
                    IntPtr.Zero);

                if (attachResult != 0)
                    throw CreateWin32Exception("AttachVirtualDisk", attachResult);
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        public void UnmountIso(string isoPath)
        {
            if (string.IsNullOrWhiteSpace(isoPath))
                throw new ArgumentException("ISO path is required.", nameof(isoPath));

            if (!File.Exists(isoPath))
                throw new FileNotFoundException("ISO file not found.", isoPath);

            var storageType = new VirtualStorageType
            {
                DeviceId = VirtualStorageTypeDeviceIso,
                VendorId = VirtualStorageTypeVendorMicrosoft
            };

            var openParameters = new OpenVirtualDiskParameters
            {
                Version = OpenVirtualDiskVersion.Version1,
                Version1 = new OpenVirtualDiskParametersV1 { RWDepth = 1 }
            };

            uint openResult = OpenVirtualDisk(
                ref storageType,
                isoPath,
                VirtualDiskAccessMask.Detach,
                OpenVirtualDiskFlag.None,
                ref openParameters,
                out IntPtr handle);

            if (openResult != 0)
                throw CreateWin32Exception("OpenVirtualDisk", openResult);

            try
            {
                uint detachResult = DetachVirtualDisk(handle, DetachVirtualDiskFlag.None, 0);
                if (detachResult != 0)
                    throw CreateWin32Exception("DetachVirtualDisk", detachResult);
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        private static Exception CreateWin32Exception(string operation, uint errorCode)
        {
            string message = errorCode <= int.MaxValue
                ? new Win32Exception((int)errorCode).Message
                : "Unknown error";

            return new InvalidOperationException(
                $"{operation} failed with 0x{errorCode:X}. {message}");
        }
    }
}
