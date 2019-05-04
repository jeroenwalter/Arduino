using System;
using System.IO.Ports;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solid.Arduino.Firmata;
using Solid.Arduino.Serial;

namespace Solid.Arduino.Core.IntegrationTest
{
    /// <summary>
    /// Performs tests with a connected Arduino device.
    /// </summary>
    [TestClass]
    public class SerialConnectionFinderTester
    {
        private SerialConnectionFinder _serialConnectionFinder;
        private SerialConnectionFactory _serialConnectionFactory;

        [TestInitialize]
        public void Initialize()
        {
            _serialConnectionFactory = new SerialConnectionFactory();
            _serialConnectionFinder = new SerialConnectionFinder(_serialConnectionFactory, null);
        }

        /// <summary>
        /// Finds a live serial connection by issuing a Firmata SysEx Firmware query (0xF0 0x79 0xF7).
        /// </summary>
        /// <remarks>
        /// Requires sketch StandardFirmata.ino to run on the connected device.
        /// </remarks>
        [TestMethod]
        public void FindFirmata_FirmataSessionFound()
        {
            using (var arduinoConnection = _serialConnectionFinder.FindFirmata())
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
        public void FindFirmata_OnSpecificPort_FirmataSessionFound()
        {
            var baudRate = SerialBaudRate.Bps_57600; // StandardFirmata.ino works on this baudrate.
            var portNames = SerialPort.GetPortNames();
            var timeOutInMs = 100;
            bool found = false;

            foreach (var portName in portNames)
            {
                using (var arduinoConnection = _serialConnectionFinder.FindFirmata(portName, baudRate, timeOutInMs))
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
        /// 
        /// If you run this test without the SerialReply.ino, but with for example the StandardFirmata.ino necessary
        /// for the other tests, then FindNonFirmata MIGHT (and most probably will) throw an exception in a worker thread.
        /// This exception isn't caught and may cause the entire test run to be aborted instead of only this test to fail.
        /// Setting an AppDomain.CurrentDomain.UnhandledException handler is not enough to prevent this.
        /// </remarks>
        [TestMethod]
        public void FindNonFirmata_SerialConnectionFound()
        {
            using (var arduinoConnection = _serialConnectionFinder.FindNonFirmata("Hello?", "Arduino!"))
            {
                Assert.IsNotNull(arduinoConnection);
            }
        }
    }
}
