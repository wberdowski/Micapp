using Android.App;
using Android.Content.PM;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Support.V7.App;
using Android.Widget;
using BitSerializer;
using Concentus.Enums;
using Concentus.Structs;
using Micapp.Common;
using System;
using System.Net;
using System.Net.Sockets;
using Xamarin.Essentials;

namespace Micapp
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true, ScreenOrientation = ScreenOrientation.Portrait)]
    public class MainActivity : AppCompatActivity
    {
        private EditText editTextAddress;
        private Button buttonConnect;

        private static ChannelIn RECORDER_CHANNELS = ChannelIn.Mono;
        private static Android.Media.Encoding RECORDER_AUDIO_ENCODING = Android.Media.Encoding.Pcm16bit;
        private AudioRecord recorder = null;
        const int BufferSize = AppConfig.FrameSize * 2;
        byte[] buffer = new byte[BufferSize];

        Socket controlSocket = new Socket(SocketType.Stream, ProtocolType.IP);
        Socket dataSocket = new Socket(SocketType.Dgram, ProtocolType.IP);
        IPEndPoint dataEp;
        byte[] controlRecvBuffer = new byte[AppConfig.ControlBufferSize];

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            DeviceDisplay.KeepScreenOn = true;

            editTextAddress = FindViewById<EditText>(Resource.Id.editTextAddress);

            dataSocket.Bind(new IPEndPoint(IPAddress.Any, AppConfig.DataPort));

            buttonConnect = FindViewById<Button>(Resource.Id.buttonConnect);
            buttonConnect.Click += ButtonConnect_Click;

            int bufferSize = AudioRecord.GetMinBufferSize(AppConfig.SampleRate, RECORDER_CHANNELS, RECORDER_AUDIO_ENCODING);
        }

        private async void ButtonConnect_Click(object sender, System.EventArgs e)
        {
            if (!controlSocket.Connected)
            {
                controlSocket.BeginConnect(editTextAddress.Text, AppConfig.ControlPort, OnConnect, null);
                buttonConnect.Enabled = false;
                buttonConnect.Text = "Connecting...";
            }
            else
            {
                controlSocket.Send(BinarySerializer.Serialize(new ControlFrame(OpCode.DisconnectRequest, new byte[0])));
                controlSocket.Disconnect(true);
                buttonConnect.Enabled = true;
                buttonConnect.Text = "Connect";

                if (recorder != null)
                    recorder.Stop();
            }
        }

        private void OnConnect(IAsyncResult ar)
        {
            try
            {
                controlSocket.EndConnect(ar);

                dataEp = new IPEndPoint(((IPEndPoint)controlSocket.RemoteEndPoint).Address, AppConfig.DataPort);

                controlSocket.BeginReceive(controlRecvBuffer, 0, controlRecvBuffer.Length, SocketFlags.None, OnControlReceive, null);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    buttonConnect.Enabled = true;
                    buttonConnect.Text = "Disconnect";
                });

                var frame = new ControlFrame(OpCode.RegisterUsernameRequest, System.Text.Encoding.UTF8.GetBytes(Build.Model));
                controlSocket.Send(BinarySerializer.Serialize(frame));
            }
            catch (SocketException ex)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    buttonConnect.Enabled = true;
                    buttonConnect.Text = "Connect";
                });

                Console.WriteLine(ex.StackTrace);
            }
        }

        private void OnControlReceive(IAsyncResult ar)
        {
            int len = 0;

            try
            {
                len = controlSocket.EndReceive(ar);
            }
            catch
            {
                return;
            }

            Console.WriteLine($"Received {len} bytes.");

            var frame = BinarySerializer.Deserialize<ControlFrame>(controlRecvBuffer);

            if (frame.opCode == OpCode.UsernameRegisteredResponse)
            {
                StartTransmitting();
            }

            controlSocket.BeginReceive(controlRecvBuffer, 0, controlRecvBuffer.Length, SocketFlags.None, OnControlReceive, null);
        }

        private void StartTransmitting()
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                var result = await Permissions.CheckStatusAsync<Permissions.Microphone>();

                if (result == PermissionStatus.Granted)
                {
                    if (recorder == null)
                    {
                        recorder = new AudioRecord(AudioSource.Mic, AppConfig.SampleRate, RECORDER_CHANNELS, RECORDER_AUDIO_ENCODING, BufferSize);
                    }

                    recorder.StartRecording();

                    var thread = new System.Threading.Thread(Runner);
                    thread.Start();
                }
                else
                {
                    await Permissions.RequestAsync<Permissions.Microphone>();
                }
            });
        }

        private void Runner()
        {
            OpusEncoder encoder = new OpusEncoder(AppConfig.SampleRate, 1, OpusApplication.OPUS_APPLICATION_RESTRICTED_LOWDELAY);
            encoder.Bitrate = AppConfig.EncoderBitrate;

            while (recorder.RecordingState == RecordState.Recording && controlSocket.Connected)
            {
                int length = recorder.Read(buffer, 0, buffer.Length);

                short[] inputAudioSamples = new short[length / 2];
                byte[] outputBuffer = new byte[1275];

                Buffer.BlockCopy(buffer, 0, inputAudioSamples, 0, length);

                int encodedLen = encoder.Encode(inputAudioSamples, 0, AppConfig.FrameSize, outputBuffer, 0, outputBuffer.Length);

                dataSocket.SendTo(outputBuffer, 0, encodedLen, SocketFlags.None, dataEp);
            }

        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}