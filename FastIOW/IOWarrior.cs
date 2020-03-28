﻿/*
 *   
 *   Copyright 2020 Florian Porsch <tederean@gmail.com>
 *   
 *   This program is free software; you can redistribute it and/or modify
 *   it under the terms of the GNU Lesser General Public License as published by
 *   the Free Software Foundation; either version 3 of the License, or
 *   (at your option) any later version.
 *   
 *   This program is distributed in the hope that it will be useful,
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *   GNU Lesser General Public License for more details.
 *   
 *   You should have received a copy of the GNU Lesser General Public License
 *   along with this program; if not, write to the Free Software
 *   Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston,
 *   MA 02110-1301 USA.
 *
 */
using System;
using System.IO;
using System.Linq;
using System.Text;
using Tederean.FastIOW.Internal;

namespace Tederean.FastIOW
{

  /// <summary>
  /// Represents a pysical IOWarrior device.
  /// </summary>
  public class IOWarrior
  {

    /// <summary>
    /// Returns address value of this device.
    /// </summary>
    public int IOWHandle { get; private set; }

    /// <summary>
    /// Returns IOWarrior model type of this device.
    /// </summary>
    public IOWarriorType Type { get; private set; }

    /// <summary>
    /// Returns unique serial number of this device.
    /// </summary>
    public string SerialNumber { get; private set; }

    /// <summary>
    /// Returns true if this device is connected, otherwise false.
    /// </summary>
    public bool Connected { get; internal set; }

    /// <summary>
    /// Returns true if I2C interface is enabled, otherwise false.
    /// </summary>
    public bool I2CEnabled { get; private set; }

    /// <summary>
    /// Holds data for writing pin states.
    /// Modify it only if you know what you are doing.
    /// </summary>
    public byte[] IOPinsWriteReport { get; private set; }

    /// <summary>
    /// Holds data for reading pin states.
    /// Modify it only if you know what you are doing.
    /// </summary>
    public byte[] IOPinsReadReport { get; private set; }


    internal IOWarrior(int index)
    {
      IOWHandle = NativeLib.IowKitGetDeviceHandle(index + 1);
      Connected = true;

      StringBuilder serialNumberBuilder = new StringBuilder();
      NativeLib.IowKitGetSerialNumber(IOWHandle, serialNumberBuilder);
      SerialNumber = serialNumberBuilder.ToString();

      var productId = NativeLib.IowKitGetProductId(IOWHandle);

      Type = IOWarriorType.GetAll<IOWarriorType>().Where(entry => entry.Id == productId).FirstOrDefault() ?? IOWarriorType.Unknown;

      var report = NewReport(Pipe.SPECIAL_MODE);
      report[0] = 0xFF;

      // Get state using special mode
      WriteReport(report, Pipe.SPECIAL_MODE);
      var result = ReadReport(Pipe.SPECIAL_MODE).Take(Type.StandardReportSize).ToArray();
      result[0] = 0x00;

      IOPinsWriteReport = result.ToArray();
      IOPinsReadReport = result.ToArray();
    }

    private void CheckClosed()
    {
      if (!Connected)
        throw new InvalidOperationException(Type.Name + " (ID:" + Type.Id + " SN:" + SerialNumber + ") is already closed.");
    }

    private void CheckPipe(Pipe pipe)
    {
      if (pipe == null)
      {
        throw new ArgumentNullException("Pipe cannot be null.");
      }

      if (!Type.SupportedPipes.Contains(pipe))
      {
        throw new ArgumentException(Type.Name + " does not support pipe mode " + pipe.Name + ".");
      }
    }

    private void CheckPin(int pin)
    {
      if (pin < 8)
      {
        throw new ArgumentNullException("Non existing pin.");
      }

      if (Type == IOWarriorType.IOWarrior24 && pin > 23)
      {
        throw new ArgumentNullException("Non existing pin on " + Type.Name + ".");
      }

      if (Type == IOWarriorType.IOWarrior28 && pin > 25)
      {
        throw new ArgumentNullException("Non existing pin on " + Type.Name + ".");
      }

      // TODO: Add other iows here...
    }

    /// <summary>
    /// Returns a new report at given pipe size. All bytes set to be zero.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    public byte[] NewReport(Pipe pipe)
    {
      CheckPipe(pipe);

      if (pipe == Pipe.IO_PINS)
      {
        return Enumerable.Repeat((byte)0x0, Type.StandardReportSize).ToArray();
      }

      return Enumerable.Repeat((byte)0x0, Type.SpecialReportSize).ToArray();
    }

    /// <summary>
    /// Write byte array report generated by NewReport()
    /// to IOWarrior device, using given pipe.
    /// Use this method only if you know what you are doing.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="InvalidOperationException"/>
    public void WriteReport(byte[] report, Pipe pipe)
    {
      CheckClosed();
      CheckPipe(pipe);

      NativeLib.IowKitWrite(IOWHandle, pipe.Id, report, report.Length);
    }

    /// <summary>
    /// Returns byte array report read from IOWarrior device using given pipe.
    /// Use this method only if you know what you are doing.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="InvalidOperationException"/>
    public byte[] ReadReport(Pipe pipe)
    {
      CheckClosed();
      CheckPipe(pipe);

      var report = NewReport(pipe);

      NativeLib.IowKitRead(IOWHandle, pipe.Id, report, report.Length);
      return report;
    }

    /// <summary>
    /// Read byte array report from IOWarrior device using given pipe.
    /// Returns true if report contains new data, otherwise false.
    /// Use this method only if you know what you are doing.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="InvalidOperationException"/>
    public bool ReadReportNonBlocking(Pipe pipe, out byte[] report)
    {
      CheckClosed();
      CheckPipe(pipe);

      report = NewReport(pipe);

      return report.Length == NativeLib.IowKitReadNonBlocking(IOWHandle, pipe.Id, report, report.Length);
    }

    /// <summary>
    /// Enable I2C interface on this IOWarrior device.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public void EnableI2C()
    {
      var report = NewReport(Type.I2CPipe);

      // I2C Enable
      report[0] = 0x01;
      report[1] = 0x01;

      WriteReport(report, Type.I2CPipe);
      I2CEnabled = true;
    }

    /// <summary>
    /// Disable I2C interface on this IOWarrior device.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public void DisableI2C()
    {
      if (!I2CEnabled) return;

      var report = NewReport(Type.I2CPipe);

      // I2C Disable
      report[0] = 0x01;

      WriteReport(report, Type.I2CPipe);
      I2CEnabled = false;
    }

    /// <summary>
    /// Write bytes to given 7bit I2C address. Length of data byte array can reach from 0 to 6.
    /// </summary>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="InvalidOperationException"/>
    public void I2CWriteBytes(byte address, params byte[] data)
    {
      if (!I2CEnabled) throw new InvalidOperationException("I2C is not enabled.");
      if (address.GetBit(7)) throw new ArgumentException("Illegal I2C Address: " + address);
      if (data.Length > 6) throw new ArgumentException("Data length must be between 0 and 6.");

      var report = NewReport(Type.I2CPipe);

      report[0] = 0x02; // I2C Write

      // Write IOW I2C settings
      report[1] = (byte)(1 + data.Length);
      report[1].SetBit(7, true); // Enable start bit
      report[1].SetBit(6, true); // Enable stop bit

      // Write address byte
      report[2] = (byte)(address << 1);
      report[2].SetBit(0, false); // false -> write

      // Write data bytes
      for (int index = 0; index < data.Length; index++)
      {
        report[3 + index] = data[index];
      }

      WriteReport(report, Type.I2CPipe);

      var result = ReadReport(Type.I2CPipe);

      if (result[1].GetBit(7))
      {
        throw new IOException("Error while writing data.");
      }
    }

    /// <summary>
    /// Read in bytes from given 7bit I2C address. Size of returned read in array
    /// equals parameter length, set to value from 0 to 6.
    /// </summary>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="InvalidOperationException"/>
    public byte[] I2CReadBytes(byte address, int length)
    {
      if (!I2CEnabled) throw new InvalidOperationException("I2C is not enabled.");
      if (address.GetBit(7)) throw new ArgumentException("Illegal I2C Address: " + address);
      if (length > 6 || length < 0) throw new ArgumentException("Data length must be between 0 and 6.");

      var report = NewReport(Type.I2CPipe);

      report[0] = 0x03; // I2C Read

      report[1] = (byte)length; // Amount of bytes to read

      // Write address byte
      report[2] = (byte)(address << 1);
      report[2].SetBit(0, true); // true -> read

      report[3] = (byte)length; // Amount of bytes to read

      WriteReport(report, Type.I2CPipe);

      var result = ReadReport(Type.I2CPipe);

      if (result[1].GetBit(7))
      {
        throw new IOException("Error while reading data.");
      }

      return result.Skip(2).Take(length).ToArray();
    }

    /// <summary>
    /// Returns read in two bytes from given 7bit I2C address.
    /// </summary>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="InvalidOperationException"/>
    public ushort I2CRead2Bytes(byte address)
    {
      var result = I2CReadBytes(address, 2);

      return (ushort)((result[0] << 8) + result[1]);
    }

    /// <summary>
    /// Set input output pin to given state.
    /// See static class Pinout for pin definitions.
    /// See static class PinState for state definitions.
    /// </summary>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="InvalidOperationException"/>
    public void DigitalWrite(int pin, bool state)
    {
      CheckPin(pin);
      CheckClosed();

      if (IOPinsWriteReport[pin / 8].GetBit(pin % 8) != state)
      {
        IOPinsWriteReport[pin / 8].SetBit(pin % 8, state);
        IOPinsWriteReport[0] = 0x00;

        WriteReport(IOPinsWriteReport, Pipe.IO_PINS);
      }
    }

    /// <summary>
    /// Returns state of input output pin.
    /// See static class Pinout for pin definitions.
    /// Call this method only if output is set to high.
    /// </summary>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="InvalidOperationException"/>
    public bool DigitalRead(int pin)
    {
      CheckPin(pin);
      CheckClosed();

      if (!IOPinsWriteReport[pin / 8].GetBit(pin % 8))
      {
        throw new InvalidOperationException("Cannot read pin while it's output is low.");
      }

      byte[] report;
      if (ReadReportNonBlocking(Pipe.IO_PINS, out report))
      {
        IOPinsReadReport = report;
      }

      return IOPinsReadReport[pin / 8].GetBit(pin % 8);
    }
  }
}