using System.Runtime.InteropServices;

namespace PermaLocke.App.Services;

/// <summary>
/// Media Foundation, only what the killcam video needs (1.0.5.5): H.264 in an MP4 out of BGRA frames, and BGRA frames
/// back out of it. Built into Windows 10 and 11; nothing shipped.
/// </summary>
internal static class Mf
{
    public static readonly Guid MajorType = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    public static readonly Guid Subtype = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    public static readonly Guid Video = new("73646976-0000-0010-8000-00AA00389B71");
    public static readonly Guid H264 = new("34363248-0000-0010-8000-00AA00389B71");
    public static readonly Guid Rgb32 = new("00000016-0000-0010-8000-00AA00389B71");
    public static readonly Guid AvgBitrate = new("20332624-fb0d-4d9e-bd0d-cbf6786c102e");
    public static readonly Guid Interlace = new("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
    public static readonly Guid FrameSize = new("1652c33d-d6b2-4012-b834-72030849a37d");
    public static readonly Guid FrameRate = new("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
    public static readonly Guid PixelAspect = new("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6");
    public static readonly Guid DefaultStride = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
    public static readonly Guid EnableVideoProcessing = new("fb394f3d-ccf1-42ee-bbb3-f9b845d5681d");

    public const int Version = 0x00020070;
    public const int FirstVideoStream = unchecked((int)0xFFFFFFFC);
    public const int EndOfStream = 0x2;

    [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)] public static extern void MFStartup(int version, int flags);
    [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)] public static extern void MFCreateMediaType(out IMFMediaType type);
    [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)] public static extern void MFCreateAttributes(out IMFAttributes attributes, int size);
    [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)] public static extern void MFCreateMemoryBuffer(int length, out IMFMediaBuffer buffer);
    [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)] public static extern void MFCreateSample(out IMFSample sample);

    [DllImport("mfreadwrite.dll", ExactSpelling = true, PreserveSig = false)]
    public static extern void MFCreateSinkWriterFromURL([MarshalAs(UnmanagedType.LPWStr)] string url, IntPtr stream, IMFAttributes? attributes, out IMFSinkWriter writer);

    [DllImport("mfreadwrite.dll", ExactSpelling = true, PreserveSig = false)]
    public static extern void MFCreateSourceReaderFromURL([MarshalAs(UnmanagedType.LPWStr)] string url, IMFAttributes? attributes, out IMFSourceReader reader);

    public static long Pack(int high, int low) => ((long)high << 32) | (uint)low;

    public static IMFMediaType VideoType(Guid subtype, int width, int height, int fps)
    {
        MFCreateMediaType(out var type);
        type.SetGUID(MajorType, Video);
        type.SetGUID(Subtype, subtype);
        type.SetUINT32(Interlace, 2);
        type.SetUINT64(FrameSize, Pack(width, height));
        type.SetUINT64(FrameRate, Pack(fps, 1));
        type.SetUINT64(PixelAspect, Pack(1, 1));
        return type;
    }
}

// Las interfaces COM llevan la tabla entera hasta el último método que se usa: el orden es lo que cuenta.
[ComImport, Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFAttributes
{
    void GetItem(); void GetItemType(); void CompareItem(); void Compare();
    void GetUINT32([MarshalAs(UnmanagedType.LPStruct)] Guid key, out int value);
    void GetUINT64([MarshalAs(UnmanagedType.LPStruct)] Guid key, out long value);
    void GetDouble(); void GetGUID(); void GetStringLength(); void GetString(); void GetAllocatedString(); void GetBlobSize();
    void GetBlob(); void GetAllocatedBlob(); void GetUnknown(); void SetItem(); void DeleteItem(); void DeleteAllItems();
    void SetUINT32([MarshalAs(UnmanagedType.LPStruct)] Guid key, int value);
    void SetUINT64([MarshalAs(UnmanagedType.LPStruct)] Guid key, long value);
    void SetDouble();
    void SetGUID([MarshalAs(UnmanagedType.LPStruct)] Guid key, [MarshalAs(UnmanagedType.LPStruct)] Guid value);
}

[ComImport, Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFMediaType
{
    void GetItem(); void GetItemType(); void CompareItem(); void Compare();
    void GetUINT32([MarshalAs(UnmanagedType.LPStruct)] Guid key, out int value);
    void GetUINT64([MarshalAs(UnmanagedType.LPStruct)] Guid key, out long value);
    void GetDouble(); void GetGUID(); void GetStringLength(); void GetString(); void GetAllocatedString(); void GetBlobSize();
    void GetBlob(); void GetAllocatedBlob(); void GetUnknown(); void SetItem(); void DeleteItem(); void DeleteAllItems();
    void SetUINT32([MarshalAs(UnmanagedType.LPStruct)] Guid key, int value);
    void SetUINT64([MarshalAs(UnmanagedType.LPStruct)] Guid key, long value);
    void SetDouble();
    void SetGUID([MarshalAs(UnmanagedType.LPStruct)] Guid key, [MarshalAs(UnmanagedType.LPStruct)] Guid value);
}

[ComImport, Guid("045FA593-8799-42b8-BC8D-8968C6453507"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFMediaBuffer
{
    void Lock(out IntPtr buffer, out int maxLength, out int currentLength);
    void Unlock();
    void GetCurrentLength(out int length);
    void SetCurrentLength(int length);
}

[ComImport, Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFSample
{
    void GetItem(); void GetItemType(); void CompareItem(); void Compare(); void GetUINT32(); void GetUINT64(); void GetDouble();
    void GetGUID(); void GetStringLength(); void GetString(); void GetAllocatedString(); void GetBlobSize(); void GetBlob();
    void GetAllocatedBlob(); void GetUnknown(); void SetItem(); void DeleteItem(); void DeleteAllItems(); void SetUINT32();
    void SetUINT64(); void SetDouble(); void SetGUID(); void SetString(); void SetBlob(); void SetUnknown(); void LockStore();
    void UnlockStore(); void GetCount(); void GetItemByIndex(); void CopyAllItems();
    void GetSampleFlags(); void SetSampleFlags();
    void GetSampleTime(out long time);
    void SetSampleTime(long time);
    void GetSampleDuration(out long duration);
    void SetSampleDuration(long duration);
    void GetBufferCount(); void GetBufferByIndex();
    void ConvertToContiguousBuffer(out IMFMediaBuffer buffer);
    void AddBuffer(IMFMediaBuffer buffer);
}

[ComImport, Guid("3137f1cd-fe5e-4805-a5d8-fb477448cb3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFSinkWriter
{
    void AddStream(IMFMediaType type, out int index);
    void SetInputMediaType(int index, IMFMediaType type, IMFAttributes? parameters);
    void BeginWriting();
    void WriteSample(int index, IMFSample sample);
    void SendStreamTick(); void PlaceMarker(); void NotifyEndOfSegment(); void Flush();
    void Finalize_();
}

[ComImport, Guid("70ae66f2-c809-4e4f-8915-bdcb406b7993"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFSourceReader
{
    void GetStreamSelection(); void SetStreamSelection(); void GetNativeMediaType();
    void GetCurrentMediaType(int stream, out IMFMediaType type);
    void SetCurrentMediaType(int stream, IntPtr reserved, IMFMediaType type);
    void SetCurrentPosition();
    void ReadSample(int stream, int flags, out int actualStream, out int streamFlags, out long timestamp, out IMFSample? sample);
}
