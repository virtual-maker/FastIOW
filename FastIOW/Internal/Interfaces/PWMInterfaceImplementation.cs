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
using System.Linq;

namespace Tederean.FastIOW.Internal
{

  public class PWMInterfaceImplementation : PWMInterface
  {

    public bool Enabled { get; private set; }

    private IOWarriorBase IOWarrior { get; set; }

    private int[] m_PWMPins;
    public int[] PWMPins
    {
      get => m_PWMPins?.ToArray() ?? default;
      private set => m_PWMPins = value;
    }

    private byte[] PWMWriteReport { get; set; }

    internal PWMConfig SelectedChannels
    {
      get => (PWMConfig) PWMWriteReport[1];
      private set => PWMWriteReport[1] = (byte)value;
    }

    private ushort PWM1
    {
      get => (ushort)((PWMWriteReport[5] << 8) + PWMWriteReport[4]);
      set
      {
        PWMWriteReport[4] = (byte)(0xFF & value);
        PWMWriteReport[5] = (byte)(value >> 8);
      }
    }

    private ushort PWM2
    {
      get => (ushort)((PWMWriteReport[10] << 8) + PWMWriteReport[9]);
      set
      {
        PWMWriteReport[9] = (byte)(0xFF & value);
        PWMWriteReport[10] = (byte)(value >> 8);
      }
    }


    internal PWMInterfaceImplementation(IOWarriorBase IOWarrior, int[] PWMPins)
    {
      this.IOWarrior = IOWarrior;
      this.PWMPins = PWMPins;
      PWMWriteReport = setupReport(IOWarrior);

      // Set to a secure state.
      Enabled = true;
      Disable();
    }

    /// <summary>
    /// Check if PWM interface is supported from device,
    /// e.g. IO-Warrior56 Dongle (USB to I2C and SPI) don't support this interface.
    /// </summary>
    /// <param name="IOWarrior">The device to test for interface support</param>
    /// <returns></returns>
    public static bool IsInterfaceSupported(IOWarriorBase IOWarrior)
    {
      var report = setupReport(IOWarrior);
      lock (IOWarrior.SyncObject)
      {
        return IOWarrior.TryWriteReport(report, Pipe.SPECIAL_MODE);
      }
    }

    private static byte[] setupReport(IOWarriorBase IOWarrior)
    {
      // PWM setup: Output frequency ~ 732 Hz at 16bit resolution.
      var report = IOWarrior.NewReport(Pipe.SPECIAL_MODE);
      report[0] = ReportId.PWM_SETUP;

      // Set Per1 to 65535
      report[2] = 0xFF;
      report[3] = 0xFF;

      // PWM1 Master Clock 48 MHz
      report[6] = 0x03;

      // Set Per2 to 65535
      report[7] = 0xFF;
      report[8] = 0xFF;

      // PWM2 Master Clock 48 MHz
      report[11] = 0x03;

      return report;
    }

    public void Enable(PWMConfig config)
    {
      lock (IOWarrior.SyncObject)
      {
        if (!Enum.IsDefined(typeof(PWMConfig), config)) throw new ArgumentException("Invalid channel.");

        if (IOWarrior.Type == IOWarriorType.IOWarrior56)
        {
          if (IOWarrior.Revision < 0x2000) throw new InvalidOperationException("PWM interface is only supported by IOWarrior firmware 2.0.0.0 or higher.");

          if (config == PWMConfig.PWM_1To2)
          {
            if (IOWarrior.Revision < 0x2002) throw new InvalidOperationException("PWM_2 is only supported by IOWarrior firmware 2.0.0.2 or higher.");

            if ((IOWarrior as IOWarrior56).SPI.Enabled) throw new InvalidOperationException("PWM_2 cannot be used while SPI is enabled.");
          }
        }

        SelectedChannels = config;
        PWM1 = 0;
        PWM2 = 0;

        IOWarrior.WriteReport(PWMWriteReport, Pipe.SPECIAL_MODE);
        Enabled = true;
      }
    }

    public void Disable()
    {
      lock (IOWarrior.SyncObject)
      {
        if (!Enabled) return;

        SelectedChannels = 0x00; // Disable

        IOWarrior.WriteReport(PWMWriteReport, Pipe.SPECIAL_MODE);
        Enabled = false;
      }
    }

    public void AnalogWrite(int pin, ushort value)
    {
      lock (IOWarrior.SyncObject)
      {
        if (!Enabled) throw new InvalidOperationException("PWM interface is not enabled.");
        if (!Array.Exists<int>(PWMPins, element => element == pin)) throw new ArgumentException("Not a PWM capable pin.");
        if (!IsChannelActivated(pin)) throw new ArgumentException("PWM channel not enabled.");

        int index = PinToChannelIndex(pin);

        if (index == 0) PWM1 = value;
        if (index == 1) PWM2 = value;

        IOWarrior.WriteReport(PWMWriteReport, Pipe.SPECIAL_MODE);
      }
    }

    private int PinToChannelIndex(int pin)
    {
      return Array.IndexOf<int>(PWMPins, pin);
    }

    private bool IsChannelActivated(int pin)
    {
      int index = PinToChannelIndex(pin);

      return index > -1 && index < (int)SelectedChannels;
    }
  }
}
