namespace Micapp.Common
{
    public class ControlFrame
    {
        public OpCode opCode { get; set; }
        public byte[] Payload { get; set; }

        public ControlFrame()
        {

        }

        public ControlFrame(OpCode opCode)
        {
            this.opCode = opCode;
        }

        public ControlFrame(OpCode opCode, byte[] payload) : this(opCode)
        {
            Payload = payload;
        }
    }
}
