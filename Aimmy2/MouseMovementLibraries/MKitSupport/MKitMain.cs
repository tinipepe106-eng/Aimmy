using System.Windows;
using MouseMovementLibraries.MKitSupport;

namespace Aimmy2.MouseMovementLibraries.MKitSupport
{
    internal class MKitMain
    {
        public bool Load(string portName = "COM16")
        {
            bool connected = MKitMouse.Open(portName);
            if (!connected)
            {
                MessageBox.Show(
                    $"Could not connect to MKit Arduino on {portName}.\n\n" +
                    "Make sure:\n" +
                    "• The Arduino is plugged in\n" +
                    "• The correct COM port is set in MKitMain.cs\n" +
                    "• No other program is using the port",
                    "Aimmy - MKit");
                return false;
            }

            return true;
        }

        public void Unload()
        {
            MKitMouse.Close();
        }
    }
}
