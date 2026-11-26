using System.Text;
using System.Xml;

namespace Chummer.Avalonia;

internal static class DesktopCharacterFileReader
{
    private static readonly Encoding BigEndianUtf32 = new UTF32Encoding(bigEndian: true, byteOrderMark: true);
    private static readonly Encoding[] UnicodeEncodings =
    [
        Encoding.UTF32, BigEndianUtf32,
        Encoding.Unicode, Encoding.BigEndianUnicode, Encoding.UTF8
    ];

    internal static string Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ReadOnlySpan<byte> body = bytes;
        // Check four-byte signatures before the overlapping UTF-16LE BOM.
        foreach (Encoding encoding in UnicodeEncodings)
        {
            if (body.StartsWith(encoding.Preamble))
                return Strict(encoding).GetString(body[encoding.Preamble.Length..]);
        }

        if (body.Length >= 4)
        {
            if (body[0] == 0 && body[1] == 0 && body[2] == 0)
                return Strict(BigEndianUtf32).GetString(body);
            if (body[1] == 0 && body[2] == 0 && body[3] == 0)
                return Strict(Encoding.UTF32).GetString(body);
            if (body[0] == 0)
                return Strict(Encoding.BigEndianUnicode).GetString(body);
            if (body[1] == 0)
                return Strict(Encoding.Unicode).GetString(body);
        }

        Encoding transport = Encoding.UTF8;
        if (body.StartsWith("<?xml"u8) && body.Length > 5 && body[5] is 9 or 10 or 13 or 32)
        {
            int end = body.IndexOf("?>"u8);
            if (end < 0)
                throw new InvalidDataException("The character file has an incomplete XML declaration.");
            using var declaration = new StringReader(Encoding.ASCII.GetString(body[..(end + 2)]));
            using XmlReader reader = XmlReader.Create(declaration);
            reader.Read();
            if (reader.GetAttribute("encoding") is { } name)
            {
                Encoding declared = Encoding.GetEncoding(name);
                // Older Chummer files wrote a UTF-16 declaration as UTF-8 bytes.
                // An ASCII '<?xml' prefix establishes the actual transport in that case.
                transport = declared.CodePage is 1200 or 1201 ? Encoding.UTF8 : declared;
            }
        }

        return Strict(transport).GetString(body);
    }

    private static Encoding Strict(Encoding encoding) => Encoding.GetEncoding(
        encoding.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
}
