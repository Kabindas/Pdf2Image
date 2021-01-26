using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Pdf2Image
{
    public enum FileType
    {
        Unknown,
        Jpeg,
        Bmp,
        Gif,
        Png,
        Pdf
    }

    public static class FilesHelper
    {
        private static readonly Dictionary<FileType, byte[]> KNOWN_FILE_HEADERS = new Dictionary<FileType, byte[]>()
        {
            { FileType.Jpeg, new byte[]{ 0xFF, 0xD8 }}, // JPEG
		    { FileType.Bmp, new byte[]{ 0x42, 0x4D }}, // BMP
		    { FileType.Gif, new byte[]{ 0x47, 0x49, 0x46 }}, // GIF
		    { FileType.Png, new byte[]{ 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }}, // PNG
		    { FileType.Pdf, new byte[]{ 0x25, 0x50, 0x44, 0x46 }} // PDF
        };

        public static FileType GetKnownFileType(ReadOnlySpan<byte> data)
        {
            foreach (var check in KNOWN_FILE_HEADERS)
            {
                if (data.Length >= check.Value.Length)
                {
                    var slice = data.Slice(0, check.Value.Length);
                    if (slice.SequenceEqual(check.Value))
                    {
                        return check.Key;
                    }
                }
            }

            return FileType.Unknown;
        }
    }
}
