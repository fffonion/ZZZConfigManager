using System;
using System.IO;
using System.Text;


// From https://github.com/CollapseLauncher/Collapse/issues/466

namespace ZZZConfigManager
{
    internal static class Sleepy
    {
        private enum BinaryHeaderEnum
        {
            SerializedStreamHeader = 0,
            Object = 1,
            ObjectWithMap = 2,
            ObjectWithMapAssemId = 3,
            ObjectWithMapTyped = 4,
            ObjectWithMapTypedAssemId = 5,
            ObjectString = 6,
            Array = 7,
            MemberPrimitiveTyped = 8,
            MemberReference = 9,
            ObjectNull = 10,
            MessageEnd = 11,
            Assembly = 12,
            ObjectNullMultiple256 = 13,
            ObjectNullMultiple = 14,
            ArraySinglePrimitive = 15,
            ArraySingleObject = 16,
            ArraySingleString = 17,
            CrossAppDomainMap = 18,
            CrossAppDomainString = 19,
            CrossAppDomainAssembly = 20,
            MethodCall = 21,
            MethodReturn = 22,
            BinaryReference = -1
        }

        private enum BinaryTypeEnum
        {
            Primitive = 0,
            String = 1,
            Object = 2,
            ObjectUrt = 3,
            ObjectUser = 4,
            ObjectArray = 5,
            StringArray = 6,
            PrimitiveArray = 7,
        }

        private enum BinaryArrayTypeEnum
        {
            Single = 0,
            Jagged = 1,
            Rectangular = 2,
            SingleOffset = 3,
            JaggedOffset = 4,
            RectangularOffset = 5,
        }

        private enum InternalArrayTypeE
        {
            Empty = 0,
            Single = 1,
            Jagged = 2,
            Rectangular = 3,
            Base64 = 4,
        }

        internal static string ReadString(string filePath, byte[] magic)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            if (magic == null) throw new ArgumentNullException(nameof(magic));

            FileInfo fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
                throw new FileNotFoundException("[Sleepy::ReadString] File does not exist!");

            using (FileStream stream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return ReadString(stream, magic);
            }
        }

        internal static string ReadString(Stream stream, byte[] magic)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (magic == null) throw new ArgumentNullException(nameof(magic));
            if (!stream.CanRead) throw new ArgumentException("[Sleepy::ReadString] Stream must be readable!", nameof(stream));

            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                reader.EmulateSleepyBinaryFormatterHeaderAssertion();

                int length = reader.GetBinaryFormatterDataLength();
                int magicLength = magic.Length;

                char[] bufferChars = new char[length];
                try
                {
                    CreateEvil(magic, out bool[] evil, out int evilsCount);
                    int j = InternalDecode(magic, evil, reader, length, magicLength, bufferChars);
                    reader.EmulateSleepyBinaryFormatterFooterAssertion();
                    return new string(bufferChars, 0, j);
                }
                finally
                {
                    Array.Clear(bufferChars, 0, bufferChars.Length);
                }
            }
        }

        private static int InternalDecode(byte[] magic, bool[] evil, BinaryReader reader, int length, int magicLength, char[] bufferChars)
        {
            bool eepy = false;
            int j = 0;
            int i = 0;

            while (i < length)
            {
                var n = i % magicLength;
                byte c = reader.ReadByte();
                byte ch = (byte)(c ^ magic[n]);

                if (evil[n])
                {
                    eepy = ch != 0;
                }
                else
                {
                    if (eepy)
                    {
                        ch += 0x40;
                        eepy = false;
                    }
                    bufferChars[j++] = (char)ch;
                }

                i++;
            }
            return j;
        }

        internal static void WriteString(string filePath, string content, byte[] magic)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (magic == null) throw new ArgumentNullException(nameof(magic));

            string fileDir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(fileDir) && !Directory.Exists(fileDir))
            {
                Directory.CreateDirectory(fileDir);
            }

            FileInfo fileInfo = new FileInfo(filePath);
            using (FileStream stream = fileInfo.Open(FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                WriteString(stream, content, magic);
            }
        }

        internal static void WriteString(Stream stream, string content, byte[] magic)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (magic == null) throw new ArgumentNullException(nameof(magic));
            if (!stream.CanWrite) throw new ArgumentException("[Sleepy::WriteString] Stream must be writable!", nameof(stream));
            if (magic.Length == 0) throw new ArgumentException("[Sleepy::WriteString] Magic cannot be empty!", nameof(magic));

            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.EmulateSleepyBinaryFormatterHeaderWrite();

                int contentLen = content.Length;
                byte[] contentBytes = new byte[contentLen * 2];
                byte[] encodedBytes = new byte[contentLen * 2];

                try
                {
                    CreateEvil(magic, out bool[] evil, out int evilsCount);
                    int bytesWritten = Encoding.UTF8.GetBytes(content, 0, contentLen, contentBytes, 0);
                    int h = InternalWrite(magic, contentLen, contentBytes, encodedBytes, evil);

                    writer.Write7BitEncodedInt(h);
                    writer.BaseStream.Write(encodedBytes, 0, h);
                    writer.EmulateSleepyBinaryFormatterFooterWrite();
                }
                finally
                {
                    Array.Clear(contentBytes, 0, contentBytes.Length);
                    Array.Clear(encodedBytes, 0, encodedBytes.Length);
                }
            }
        }

        private static int InternalWrite(byte[] magic, int contentLen, byte[] contentBytes, byte[] encodedBytes, bool[] evil)
        {
            int h = 0;
            int i = 0;
            int j = 0;

            while (j < contentLen)
            {
                int n = i % magic.Length;
                byte ch = contentBytes[j];
                if (evil[n])
                {
                    byte eepy = 0;
                    if (contentBytes[j] > 0x40)
                    {
                        ch -= 0x40;
                        eepy = 1;
                    }
                    encodedBytes[h++] = (byte)(eepy ^ magic[n]);
                    n = ++i % magic.Length;
                }

                encodedBytes[h++] = (byte)(ch ^ magic[n]);
                i++;
                j++;
            }
            return h;
        }

        private static void CreateEvil(byte[] magic, out bool[] evilist, out int evilsCount)
        {
            int magicLength = magic.Length;
            evilist = new bool[magicLength];
            evilsCount = 0;

            for (int i = 0; i < magicLength; i++)
            {
                evilist[i] = (magic[i] & 0xC0) == 0xC0;
                if (evilist[i]) evilsCount++;
            }
        }

        private static void EmulateSleepyBinaryFormatterHeaderAssertion(this BinaryReader reader)
        {
            reader.LogAssertInfoByteEnum(BinaryHeaderEnum.SerializedStreamHeader);
            reader.LogAssertInfoInt32Enum(BinaryHeaderEnum.Object);
            reader.LogAssertInfoInt32Enum(BinaryHeaderEnum.BinaryReference);
            reader.LogAssertInfoInt32Enum(BinaryTypeEnum.String);
            reader.LogAssertInfoInt32Enum(BinaryArrayTypeEnum.Single);
            reader.LogAssertInfoByteEnum(BinaryTypeEnum.StringArray);
            reader.LogAssertInfoInt32Enum(InternalArrayTypeE.Single);
        }

        private static void EmulateSleepyBinaryFormatterFooterAssertion(this BinaryReader reader)
        {
            reader.LogAssertInfoByteEnum(BinaryHeaderEnum.MessageEnd);
        }

        private static void EmulateSleepyBinaryFormatterHeaderWrite(this BinaryWriter writer)
        {
            writer.WriteEnumAsByte(BinaryHeaderEnum.SerializedStreamHeader);
            writer.WriteEnumAsInt32(BinaryHeaderEnum.Object);
            writer.WriteEnumAsInt32(BinaryHeaderEnum.BinaryReference);
            writer.WriteEnumAsInt32(BinaryTypeEnum.String);
            writer.WriteEnumAsInt32(BinaryArrayTypeEnum.Single);
            writer.WriteEnumAsByte(BinaryTypeEnum.StringArray);
            writer.WriteEnumAsInt32(InternalArrayTypeE.Single);
        }

        private static void EmulateSleepyBinaryFormatterFooterWrite(this BinaryWriter writer)
        {
            writer.WriteEnumAsByte(BinaryHeaderEnum.MessageEnd);
        }

        private static void WriteEnumAsByte<T>(this BinaryWriter writer, T headerEnum) where T : struct, Enum
        {
            writer.Write((byte)Convert.ToInt32(headerEnum));
        }

        private static void WriteEnumAsInt32<T>(this BinaryWriter writer, T headerEnum) where T : struct, Enum
        {
            writer.Write(Convert.ToInt32(headerEnum));
        }

        private static void LogAssertInfoByteEnum<T>(this BinaryReader stream, T assertHeaderEnum) where T : struct, Enum
        {
            int currentInt = stream.ReadByte();
            LogAssertInfo(stream, assertHeaderEnum, currentInt);
        }

        private static void LogAssertInfoInt32Enum<T>(this BinaryReader stream, T assertHeaderEnum) where T : struct, Enum
        {
            int currentInt = stream.ReadInt32();
            LogAssertInfo(stream, assertHeaderEnum, currentInt);
        }

        private static void LogAssertInfo<T>(BinaryReader reader, T assertHeaderEnum, int currentInt) where T : struct, Enum
        {
            int intAssertCasted = Convert.ToInt32(assertHeaderEnum);
            if (intAssertCasted != currentInt)
            {
                string assertHeaderEnumValueName = Enum.GetName(typeof(T), assertHeaderEnum);
                T comparedEnumCasted = (T)Enum.ToObject(typeof(T), currentInt);
                string comparedHeaderEnumValueName = Enum.GetName(typeof(T), comparedEnumCasted);

                throw new InvalidDataException(string.Format("[Sleepy::LogAssertInfo] BinaryFormatter header is not valid at stream pos: {0:x8}. Expecting object enum: {1} but getting: {2} instead!",
                    reader.BaseStream.Position,
                    assertHeaderEnumValueName,
                    comparedHeaderEnumValueName));
            }
        }

        private static int GetBinaryFormatterDataLength(this BinaryReader reader)
        {
            return reader.Read7BitEncodedInt();
        }

        private static int Read7BitEncodedInt(this BinaryReader reader)
        {
            int count = 0;
            int shift = 0;
            byte b;
            do
            {
                if (shift == 35)
                    throw new FormatException("Format_Bad7BitInt32");
                b = reader.ReadByte();
                count |= (b & 0x7F) << shift;
                shift += 7;
            }
            while ((b & 0x80) != 0);
            return count;
        }

        private static void Write7BitEncodedInt(this BinaryWriter writer, int value)
        {
            uint v = (uint)value;
            while (v >= 0x80)
            {
                writer.Write((byte)(v | 0x80));
                v >>= 7;
            }
            writer.Write((byte)v);
        }
    }
}