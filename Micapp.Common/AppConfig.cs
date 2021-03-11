namespace Micapp.Common
{
    public class AppConfig
    {
        public const int ControlPort = 65321;
        public const int DataPort = 65322;
        public const int ControlBufferSize = 4 * 1024;
        public const int DataBufferSize = 64 * 1024;
        public const int SampleRate = 48000;
        public const int Bits = 16;
        public const int FrameSize = 480;
        public const int EncoderBitrate = 24000;
    }
}
