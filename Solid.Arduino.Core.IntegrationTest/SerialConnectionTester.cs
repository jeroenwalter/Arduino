using System;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Solid.Arduino;
using Solid.Arduino.Core;
using Solid.Arduino.Firmata;

namespace Solid.Arduino.IntegrationTest
{
    /// <summary>
    /// Performs tests with a connected Arduino device.
    /// </summary>
    [TestClass]
    public class SerialConnectionTester
    {
        /// <summary>
        /// Finds a live serial connection by issuing a Firmata SysEx Firmware query (0xF0 0x79 0xF7).
        /// </summary>
        /// <remarks>
        /// Requires sketch StandardFirmata.ino to run on the connected device.
        /// </remarks>
        [TestMethod]
        public void FindSerialConnection_FirmataEnabled()
        {
            using (var arduinoConnection = SerialConnection.Find())
            {
                Assert.IsNotNull(arduinoConnection);
            }
        }

        /// <summary>
        /// Finds a live serial connection by issuing a Firmata SysEx Firmware query (0xF0 0x79 0xF7).
        /// </summary>
        /// <remarks>
        /// Requires sketch StandardFirmata.ino to run on the connected device.
        /// </remarks>
        [TestMethod]
        public void FindFirmata_FirmataEnabled()
        {
          var baudRate = SerialBaudRate.Bps_57600; // StandardFirmata.ino works on this baudrate.
          var portNames = SerialPort.GetPortNames();
          var timeOutInMs = 100;
          bool found = false;

          foreach (var portName in portNames)
          {
            using (var arduinoConnection = SerialConnection.FindFirmata(portName, baudRate, timeOutInMs))
            {
              if (arduinoConnection != null)
              {
                found = true;
                break;
              }
            }
          }

          Assert.IsTrue(found);
        }

    /// <summary>
    /// Finds a live serial connection by issuing a query string.
    /// </summary>
    /// <remarks>
    /// Requires sketch SerialReply.ino to run on the connected device.
    /// (This sketch can be found in this project.)
    /// </remarks>
    [TestMethod]
        public void FindSerialConnection_Serial()
        {
            using (var arduinoConnection = SerialConnection.Find("Hello?", "Arduino!"))
            {
                Assert.IsNotNull(arduinoConnection);
            }
        }
    }
}
