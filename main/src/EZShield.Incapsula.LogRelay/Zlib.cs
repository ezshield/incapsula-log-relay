using System.IO;
using ComponentAce.Compression.Libs.zlib;

namespace EZShield.Incapsula.LogRelay
{
    public static class ZLib
    {
        public static void Decompress(Stream inStream, Stream outStream)
        {
            using (var zstream = new ZOutputStream(outStream))
            {
                inStream.CopyTo(zstream);
                zstream.Flush();
            }
        }
    }
}