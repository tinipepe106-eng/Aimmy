using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;

namespace MouseMovementLibraries.MKitSupport
{
    internal class MKitMouse
    {
        private const byte START_BYTE    = 0xAA;
        private const byte CMD_ONESHOT   = 0x04;
        private const byte CMD_BUTTON    = 0x07;
        private const byte BTN_L         = 1;

        private static SerialPort? _port;
        private static readonly SemaphoreSlim _txLock = new(1, 1);

        // ── Connection ────────────────────────────────────────────────────────────

        public static bool Open(string portName = "COM7", int baud = 115200)
        {
            try
            {
                _port = new SerialPort(portName, baud)
                {
                    ReadTimeout  = 500,
                    WriteTimeout = 500,
                    NewLine      = "\n",
                    DtrEnable    = true,
                };
                _port.Open();

                // Leonardo CDC needs a moment after DTR assertion
                Thread.Sleep(150);
                _port.DiscardInBuffer();

                // Verify firmware is alive with a text PING
                _port.WriteLine("PING");

                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 1500)
                {
                    try
                    {
                        string line = _port.ReadLine();
                        if (line.IndexOf("PONG", StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                    catch (TimeoutException) { }
                    Thread.Sleep(10);
                }

                Close();
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MKitMouse.Open failed: {ex.Message}");
                return false;
            }
        }

        public static void Close()
        {
            try { _port?.Close(); } catch { }
            _port = null;
        }

        public static bool IsOpen => _port?.IsOpen == true;

        // ── Mouse movement (called by MouseManager.MoveCrosshair) ─────────────────

        public static void Move(int x, int y)
        {
            if (_port is null || !_port.IsOpen) return;

            // Values are already clamped to -150..150 by MouseManager,
            // but the firmware wants -127..127 per packet — chunk if needed.
            SendMoveFrame((sbyte)Math.Clamp(x, -127, 127),
                          (sbyte)Math.Clamp(y, -127, 127));
        }

        // ── Button actions (called by MouseManager.GetMouseActions) ───────────────

        public static void mouse_down() => SendButtonFrame(BTN_L, true);
        public static void mouse_up()   => SendButtonFrame(BTN_L, false);

        // ── Binary frame helpers ──────────────────────────────────────────────────

        private static void SendMoveFrame(sbyte dx, sbyte dy)
        {
            var payload = new byte[] { (byte)dx, (byte)dy };
            SendFrame(CMD_ONESHOT, payload);
        }

        private static void SendButtonFrame(byte btn, bool pressed)
        {
            var payload = new byte[] { btn, (byte)(pressed ? 1 : 0) };
            SendFrame(CMD_BUTTON, payload);
        }

        private static void SendFrame(byte op, byte[] payload)
        {
            if (_port is null || !_port.IsOpen) return;

            byte len = (byte)payload.Length;
            byte chk = (byte)(op ^ len);
            var buf = new byte[3 + payload.Length + 1];
            buf[0] = START_BYTE;
            buf[1] = op;
            buf[2] = len;
            for (int i = 0; i < payload.Length; i++)
            {
                buf[3 + i] = payload[i];
                chk ^= payload[i];
            }
            buf[3 + payload.Length] = chk;

            _txLock.Wait();
            try   { _port.Write(buf, 0, buf.Length); }
            catch (Exception ex) { Debug.WriteLine($"MKitMouse.SendFrame: {ex.Message}"); }
            finally { _txLock.Release(); }
        }
    }
}
