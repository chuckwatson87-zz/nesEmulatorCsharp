using System;
using System.Collections.Generic;
using System.Text;
using PixelEngine;
using static nesEmulatorCsharp.Olc6502;

namespace nesEmulatorCsharp
{
    public class Demo : Game {

        Bus NES = new Bus();
        Dictionary<UInt16, string> mapAsm = new Dictionary<UInt16, string>();
        private readonly Pixel White = new Pixel(255, 255, 255);
        private readonly Pixel Green = new Pixel(0, 255, 0);
        private readonly Pixel Red = new Pixel(255, 00, 0);
        private readonly Pixel Cyan = new Pixel(0, 255, 255);
        private readonly Pixel Dark_Blue = new Pixel(0, 0, 139);
      

        public Demo()
        {
            base.AppName = "Chuck demo";
        }


        private void DrawRam(int x, int y, UInt16 nAddr, int nRows, int nColumns)
        {
            int nRamX = x, nRamY = y;
            for (int row = 0; row < nRows; row++)
            {
                var sOffset = "$" + hex(nAddr, 4) + ":";
                
                for (int col = 0; col < nColumns; col++)
                {
                    sOffset += " " + hex(NES.Read(nAddr, true), 2);
                    nAddr += 1;
                }
                var point = new Point(nRamX, nRamY);
                DrawText(point, sOffset, new Pixel(255, 255, 255));
                nRamY += 10;
            }
        }

        private void DrawCpu(int x, int y)
        {
            DrawText(new Point(x, y), "STATUS:", White);
            DrawText(new Point(x + 64, y), "N", (NES.CPU.StatusRegister &  ToByte((int)FLAGS6502.Negative)) != 0 ? Green : Red);
            DrawText(new Point(x + 80, y), "V", (NES.CPU.StatusRegister & ToByte((int)FLAGS6502.Overflow)) != 0 ? Green : Red);
            DrawText(new Point(x + 96, y), "-", (NES.CPU.StatusRegister & ToByte((int)FLAGS6502.Unused)) != 0 ? Green : Red);
            DrawText(new Point(x + 112, y), "B", (NES.CPU.StatusRegister & ToByte((int)FLAGS6502.Break)) != 0 ? Green : Red);
            DrawText(new Point(x + 128, y), "D", (NES.CPU.StatusRegister & ToByte((int)FLAGS6502.DecmalMode)) != 0 ? Green : Red);
            DrawText(new Point(x + 144, y), "I", (NES.CPU.StatusRegister & ToByte((int)FLAGS6502.DisableInterrupts)) != 0 ? Green : Red);
            DrawText(new Point(x + 160, y), "Z", (NES.CPU.StatusRegister & ToByte((int)FLAGS6502.Zero)) != 0 ? Green : Red);
            DrawText(new Point(x + 178, y), "C", (NES.CPU.StatusRegister & ToByte((int)FLAGS6502.Carry)) != 0 ? Green : Red);
            DrawText(new Point(x, y + 10), "PC: $" + hex(NES.CPU.ProgramCounter, 4), White);
            DrawText(new Point(x, y + 20), "A: $" + hex(NES.CPU.AccumulatorRegister, 2) + "  [" + NES.CPU.AccumulatorRegister.ToString() + "]", White);
            DrawText(new Point(x, y + 30), "X: $" + hex(NES.CPU.XRegister, 2) + "  [" + NES.CPU.XRegister.ToString() + "]", White);
            DrawText(new Point(x, y + 40), "Y: $" + hex(NES.CPU.YRegister, 2) + "  [" + NES.CPU.YRegister.ToString() + "]", White);
            DrawText(new Point(x, y + 50), "Stack P: $" + hex(NES.CPU.StackPointer, 4), White);

        }

        private void DrawCode(int x, int y, int nLines)
        {
            string asmValue;
           
            int nLineY = (nLines >> 1) * 10 + y;
            if (mapAsm.TryGetValue(NES.CPU.ProgramCounter, out asmValue))
            {
                DrawText(new Point(x, nLineY), asmValue, Cyan);
                while (nLineY < (nLines * 10) + y)
                {
                    nLineY += 10;
                    string asmValue2;
                    if (mapAsm.TryGetValue(++NES.CPU.ProgramCounter, out asmValue2))
                    {
                        DrawText(new Point(x, nLineY), asmValue2, White);
                    }
                }
            }

            nLineY = (nLines >> 1) * 10 + y;
            if (mapAsm.TryGetValue(NES.CPU.ProgramCounter, out asmValue))
            {
                while (nLineY > y)
                {
                    nLineY -= 10;
                    string asmValue2;
                    if (mapAsm.TryGetValue(--NES.CPU.ProgramCounter, out asmValue2))
                    {
                        DrawText(new Point(x, nLineY), asmValue2, White);
                    }
                }
            }
        }

        public override void OnCreate()
        {
            // Load Program (assembled at https://www.masswerk.at/6502/assembler.html)
            /*
                *=$8000
                LDX #10
                STX $0000
                LDX #3
                STX $0001
                LDY $0000
                LDA #0
                CLC
                loop
                ADC $0001
                DEY
                BNE loop
                STA $0002
                NOP
                NOP
                NOP
            */

            // Convert hex string into bytes for RAM
            //Check this if something goes wrong
            var ss = "A2 0A 8E 00 00 A2 03 8E 01 00 AC 00 00 A9 00 18 6D 01 00 88 D0 FA 8D 02 00 EA EA EA";
            

            UInt16 nOffset = 0x8000;
            foreach (char c in ss)
            {
                //NES.RAM[nOffset++] = (uint8_t)std::stoul(b, nullptr, 16);
                NES.RAM[nOffset++] = Convert.ToByte(c.ToString(), fromBase: 16);
            }

            // Set Reset Vector
            NES.RAM[0xFFFC] = 0x00;
            NES.RAM[0xFFFD] = 0x80;

            // Dont forget to set IRQ and NMI vectors if you want to play with those

            // Extract dissassembly
            mapAsm = NES.CPU.Disassemble(0x0000, 0xFFFF);

            // Reset
            NES.CPU.Reset();
        }

        public override void OnUpdate(float elapsed)
        {
            Clear(Dark_Blue);


            if (GetKey(Key.Space).Pressed)
            {
                do
                {
                    NES.CPU.Clock();
                }
                while (!NES.CPU.Complete());
            }

            if (GetKey(Key.R).Pressed)
                NES.CPU.Reset();

            if (GetKey(Key.I).Pressed)
                NES.CPU.InterruptRequest();

            if (GetKey(Key.N).Pressed)
                NES.CPU.NonMaskableInterruptRequest();

            // Draw Ram Page 0x00		
            DrawRam(2, 2, 0x0000, 16, 16);
            DrawRam(2, 182, 0x8000, 16, 16);
            DrawCpu(448, 2);
            DrawCode(448, 72, 26);


            DrawText(new Point(10, 370), "SPACE = Step Instruction    R = RESET    I = IRQ    N = NMI", White);

        }

        private byte ToByte(int number)
        {
            return Convert.ToByte(number);
        }


        private object hex(UInt32 n, byte d)
        {

            String s = new String(Convert.ToChar(d), '0');
            StringBuilder sb = new StringBuilder(s);

            for (int i = d - 1; i >= 0; i--, n >>= 4)
                sb[i] = "0123456789ABCDEF"[Convert.ToInt16(n & 0xF)];
            return s;
        }


    }

    public class main
    {
        public int start()
        {
            Demo demo = new Demo();
            demo.Construct(680, 480, 2, 2);
            demo.Start();
            return 0;
        }
    }
}
