using BitSerializer;
using Concentus.Structs;
using CSCore.CoreAudioAPI;
using CSCore.SoundOut;
using CSCore.Streams;
using Micapp.Common;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MicappReceiver
{
    public class Client : IDisposable
    {
        public string Name { get; set; }
        public Socket ControlSocket { get; set; }
        public Socket DataSocket { get; set; }

        public event EventHandler Disconnected;
        public event EventHandler NameRegistered;

        byte[] controlRecvBuffer = new byte[AppConfig.ControlBufferSize];
        byte[] dataRecvBuffer = new byte[AppConfig.DataBufferSize];
        EndPoint dataEndpoint;
        public WasapiOut wasapiOut;
        OpusDecoder decoder = new OpusDecoder(AppConfig.SampleRate, 1);
        WriteableBufferingSource outputAudioBuffer = new WriteableBufferingSource(new CSCore.WaveFormat(AppConfig.SampleRate, AppConfig.Bits, 1));

        public Client(Socket tcpSocket, Socket dataSocket)
        {
            wasapiOut = new WasapiOut();
            wasapiOut.Device = MMDeviceEnumerator.DefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            wasapiOut.Initialize(outputAudioBuffer);
            wasapiOut.Play();

            decoder = new OpusDecoder(AppConfig.SampleRate, 1);

            ControlSocket = tcpSocket;
            ControlSocket.BeginReceive(controlRecvBuffer, 0, controlRecvBuffer.Length, SocketFlags.None, OnControlReceive, null);

            dataEndpoint = new IPEndPoint(((IPEndPoint)tcpSocket.RemoteEndPoint).Address, AppConfig.DataPort);

            DataSocket = dataSocket;
            DataSocket.BeginReceiveFrom(dataRecvBuffer, 0, dataRecvBuffer.Length, SocketFlags.None, ref dataEndpoint, OnDataReceive, null);
        }

        private void OnDataReceive(IAsyncResult ar)
        {
            int len = 0;

            try
            {
                len = DataSocket.EndReceiveFrom(ar, ref dataEndpoint);
            }
            catch
            {
                return;
            }

            Debug.WriteLine("[UDP] " + len);

            if (!ControlSocket.Connected)
            {
                return;
            }

            // Decoding loop
            short[] outputBuffer = new short[AppConfig.FrameSize];

            int decodedLen = decoder.Decode(dataRecvBuffer, 0, len, outputBuffer, 0, AppConfig.FrameSize, false);

            byte[] bytes = new byte[decodedLen * 2];
            Buffer.BlockCopy(outputBuffer, 0, bytes, 0, decodedLen * 2);

            if (outputAudioBuffer != null)
            {
                outputAudioBuffer.Write(bytes, 0, bytes.Length);
            }

            DataSocket.BeginReceiveFrom(dataRecvBuffer, 0, dataRecvBuffer.Length, SocketFlags.None, ref dataEndpoint, OnDataReceive, null);
        }

        private void OnControlReceive(IAsyncResult ar)
        {
            int len = 0;

            try
            {
                len = ControlSocket.EndReceive(ar);
            }
            catch
            {
                OnDisconnect();
                return;
            }

            Debug.WriteLine("[TCP] " + len);

            if (len == 0)
            {
                if (ControlSocket.Poll(1000, SelectMode.SelectWrite))
                {
                    OnDisconnect();
                    return;
                }
            }

            var frame = BinarySerializer.Deserialize<ControlFrame>(controlRecvBuffer);

            Debug.WriteLine($"[TCP][{ControlSocket.RemoteEndPoint}] {frame.opCode}");

            if (frame.opCode == OpCode.DisconnectRequest)
            {
                OnDisconnect();
                return;
            }
            else if (frame.opCode == OpCode.RegisterUsernameRequest)
            {
                Name = Encoding.UTF8.GetString(frame.Payload);
                ControlSocket.Send(BinarySerializer.Serialize(new ControlFrame(OpCode.UsernameRegisteredResponse, new byte[0])));
                Debug.WriteLine($"Client registered as {Name}");
                NameRegistered?.Invoke(this, null);
            }

            ControlSocket.BeginReceive(controlRecvBuffer, 0, controlRecvBuffer.Length, SocketFlags.None, OnControlReceive, null);
        }

        internal void SwitchDevice(MMDevice selectedDevice)
        {
            wasapiOut.Stop();
            wasapiOut.Dispose();

            outputAudioBuffer.Dispose();
            outputAudioBuffer = null;
            outputAudioBuffer = new WriteableBufferingSource(new CSCore.WaveFormat(AppConfig.SampleRate, AppConfig.Bits, 1));

            wasapiOut = new WasapiOut();
            wasapiOut.Device = selectedDevice;
            wasapiOut.Initialize(outputAudioBuffer);
            wasapiOut.Play();
        }

        private void OnDisconnect()
        {
            Disconnected?.Invoke(this, null);

            try
            {
                ControlSocket.Close();
            }
            catch (IOException)
            {

            }
        }

        public void Dispose()
        {
            try
            {
                ControlSocket.Close();
            }
            catch (IOException)
            {

            }

            wasapiOut.Stop();
            wasapiOut.Dispose();
        }
    }
}
