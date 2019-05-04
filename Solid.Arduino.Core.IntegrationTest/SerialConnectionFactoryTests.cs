using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solid.Arduino.Core;
using Solid.Arduino.Firmata;
using System;
using System.Linq;
using Solid.Arduino.Serial;

namespace Solid.Arduino.Core.IntegrationTest
{
    [TestClass]
    public class SerialConnectionFactoryTests
    {
        [TestInitialize]
        public void TestInitialize()
        {


        }

        [TestCleanup]
        public void TestCleanup()
        {

        }

        private SerialConnectionFactory CreateFactory()
        {
            return new SerialConnectionFactory();
        }

        [TestMethod]
        public void GetDeviceNames_StateUnderTest_ExpectedBehavior()
        {
            // Arrange
            var serialConnectionFactory = CreateFactory();

            // Act
            var result = serialConnectionFactory.GetDeviceNames();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Create_NullName_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var serialConnectionFactory = CreateFactory();
            IDataConnectionConfiguration configuration = new SerialConnectionConfiguration();

            // Act
            var result = serialConnectionFactory.Create(
                null,
                configuration);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Create_EmptyName_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var serialConnectionFactory = CreateFactory();
            var deviceName = string.Empty;
            IDataConnectionConfiguration configuration = new SerialConnectionConfiguration();

            // Act
            var result = serialConnectionFactory.Create(
                deviceName,
                configuration);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Create_InvalidName_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var serialConnectionFactory = CreateFactory();
            var deviceName = "InvalidComPortName";
            IDataConnectionConfiguration configuration = new SerialConnectionConfiguration();

            // Act
            var result = serialConnectionFactory.Create(
                deviceName,
                configuration);
        }



        [TestMethod]
        public void Create_ValidName_ReturnsConnection()
        {
            // Arrange
            var serialConnectionFactory = CreateFactory();
            var deviceName = serialConnectionFactory.GetDeviceNames().First();
            var configuration = new SerialConnectionConfiguration { BaudRate = SerialBaudRate.Bps_9600 };

            // Act
            var result = serialConnectionFactory.Create(
                deviceName,
                configuration);

            // Assert
            Assert.IsNotNull(result);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Create_ValidName_NullConfiguration_ThrowsArgumentNullException()
        {
            // Arrange
            var serialConnectionFactory = CreateFactory();
            var deviceName = serialConnectionFactory.GetDeviceNames().First();

            // Act
            var result = serialConnectionFactory.Create(
                deviceName,
                null);
        }
    }
}
