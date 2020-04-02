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
using Tederean.FastIOW.Internal;

namespace Tederean.FastIOW
{

  public class IOWarrior28L : IOWarriorBase, I2CDevice
  {

    public override string Name => "IOWarrior28L";

    public override IOWarriorType Type => IOWarriorType.IOWarrior28L;

    protected override int StandardReportSize => 5;

    protected override int SpecialReportSize => 64;

    protected override Pipe[] SupportedPipes => new[] { Pipe.IO_PINS, Pipe.SPECIAL_MODE };

    public I2CInterface I2CInterface { get; private set; }


    public const int P0_0 = 1 * 8 + 0;
    public const int P0_1 = 1 * 8 + 1;
    public const int P0_2 = 1 * 8 + 2;
    public const int P0_3 = 1 * 8 + 3;
    public const int P0_4 = 1 * 8 + 4;
    public const int P0_5 = 1 * 8 + 5;
    public const int P0_6 = 1 * 8 + 6;
    public const int P0_7 = 1 * 8 + 7;

    public const int P1_0 = 2 * 8 + 0;
    public const int P1_1 = 2 * 8 + 1;
    public const int P1_2 = 2 * 8 + 2;
    public const int P1_3 = 2 * 8 + 3;
    public const int P1_4 = 2 * 8 + 4;
    public const int P1_5 = 2 * 8 + 5;
    public const int P1_6 = 2 * 8 + 6;
    public const int P1_7 = 2 * 8 + 7;

    public const int P2_1 = 3 * 8 + 1;

    public const int I2C_SCL = P0_1;
    public const int I2C_SDA = P0_2;


    internal IOWarrior28L(int handle) : base(handle)
    {
      I2CInterface = new I2CInterfaceImplementation(this, Pipe.SPECIAL_MODE, 6);
    }


    protected override bool IsValidDigitalPin(int pin)
    {
      return pin >= P0_0 && (pin <= P1_7 || pin == P2_1);
    }
  }
}