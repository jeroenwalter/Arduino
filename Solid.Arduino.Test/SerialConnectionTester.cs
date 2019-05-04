using System.Collections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solid.Arduino.Firmata;
using Solid.Arduino.Serial;

namespace Solid.Arduino.Test
{
    [TestClass]
    public class SerialConnectionTester
    {
        [TestMethod]
        [Ignore]
        public void SerialConnection_Constructor_WithoutParameters()
        {
            //var connection = new SerialConnection();
            //Assert.AreEqual(100, connection.ReadTimeout);
            //Assert.AreEqual(100, connection.WriteTimeout);
            //Assert.AreEqual(115200, connection.BaudRate);
        }

        [TestMethod]
        public void SerialConnection_Constructor_WithParameters()
        {
            var connection = new SerialConnection("COM1", SerialBaudRate.Bps_115200);
            Assert.AreEqual(100, connection.ReadTimeout);
            Assert.AreEqual(100, connection.WriteTimeout);
            Assert.AreEqual(115200, connection.BaudRate);
        }

        [TestMethod]
        public void SerialConnection_OpenAndClose()
        {
            var connection = new SerialConnection("COM1", SerialBaudRate.Bps_115200);
            connection.Open();
            connection.Close();
        }

        [TestMethod]
        public void SerialConnection_OpenAndDoubleClose()
        {
            var connection = new SerialConnection("COM1", SerialBaudRate.Bps_115200);
            connection.Open();
            connection.Close();
            connection.Close();
        }
    }
}
