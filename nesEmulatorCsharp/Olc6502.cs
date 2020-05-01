using System;
using System.Collections.Generic;

namespace nesEmulatorCsharp
{
    public class Olc6502
    {
        private Bus Bus;

        public enum FLAGS6502
        {
            Carry = 1 << 0,
            Zero = 1 << 1,
            DisableInterrupts = 1 << 2,
            DecmalMode = 1 << 3, //Decimal Mode
            Break = 1 << 4,
            Unused = 1 << 5, 
            Overflow = 1 << 6, 
            Negative = 1 << 7
        };

        public byte AccumulatorRegister = 0x00;
        public byte XRegister = 0x00;
        public byte YRegister = 0x00;
        public byte StackPointer = 0x00;
        public UInt16 ProgramCounter = 0x0000;
        public byte StatusRegister = 0x00;

        public byte Fetched = 0x00;
        public UInt16 AbsoluteAddress = 0x0000;
        public UInt16 RelativeAddress = 0x0000;
        public byte CurrentOpCode = 0x00;
        public byte Cycles = 0;


        private struct Instruction
        {
            public string Name;
            public Func<byte> OpCodeFunc;
            public Func<byte> AddressModeFunc;
            public byte Cycles;
        }

        private List<Instruction> InstructionLookup;


        public Olc6502()
        {
            InstructionLookup = new List<Instruction>()
            {
                new Instruction()
                {
                    Name ="BRK",
                    OpCodeFunc = BRK,
                    AddressModeFunc = IMP,
                    Cycles = 2

                }
            };
        }

        public void ConnectBus(Bus bus)
        {
            Bus = bus;
        }

        private byte read(UInt16 a)
        {

            return Bus.read(a);
        }

        //Addressing Modes, more going here, with better names
        public byte IMP()
        {
            Fetched = AccumulatorRegister;
            return 0; 
        }

        public byte IMM()
        {
            AbsoluteAddress = ProgramCounter++;
            return 0;
        }

        public byte ZP0()
        {
            AbsoluteAddress = read(ProgramCounter);
            ProgramCounter++;
            AbsoluteAddress &= 0x00FF;
            return 0;
        }

        public byte ZPX()
        {
            AbsoluteAddress = ToU16Bit(read(ProgramCounter) + XRegister);
            ProgramCounter++;
            AbsoluteAddress &= 0x00FF;
            return 0;
        }

        public byte ZPY()
        {
            AbsoluteAddress = ToU16Bit(read(ProgramCounter) + YRegister);
            ProgramCounter++;
            AbsoluteAddress &= 0x00FF;
            return 0;
        }


        //OpCodes, more going here
        public byte BRK()
        {
            ProgramCounter++;
            SetFlag(FLAGS6502.DisableInterrupts, true);
            
            Write(ToU16Bit(0x0100 + StackPointer), ToByte((ProgramCounter >> 8) & 0x00FF));
            StackPointer--;
            Write(ToU16Bit(0x0100 + StackPointer), ToByte(ProgramCounter & 0x00FF));
            StackPointer--;

            SetFlag(FLAGS6502.Break, true);
            Write(ToU16Bit(0x0100 + StackPointer), StatusRegister);
            StackPointer--;
            SetFlag(FLAGS6502.Break, false);

            //ProgramCounter = ToU16Bit(read(0xFFFE)) | ToU16Bit(read(0xFFFF) << 8);
            ProgramCounter = ToU16Bit(read(0xFFFE) | (read(0xFFFF) << 8));

            return 0;
        }

        public byte XXX()
        {
            return 0;
        }

        //CPU functions
        public void Clock()
        {
            if (Cycles == 0)
            {
                CurrentOpCode = read(ProgramCounter);
                ProgramCounter++;

                var instruction = InstructionLookup[CurrentOpCode];

                //Get starting number of cycles
                Cycles = instruction.Cycles;

                var additionalAddressCycle = instruction.AddressModeFunc();
                var additionalOpCodeCycle = instruction.OpCodeFunc();

                Cycles += ToByte((additionalAddressCycle & additionalOpCodeCycle));
            }

            Cycles--;
        }

        public void Reset()
        {

        }

        public void InterruptRequest()
        {

        }

        public void NonMaskableInterruptRequest()
        {

        }

        //helpers
        public byte Fetch()
        {
            return Fetched;
        }

        private void Write(UInt16 a, byte d)
        {
            Bus.write(a, d);
        }

        private byte GetFlag(FLAGS6502 flag)
        {
            return (byte)flag;
        }

        private void SetFlag(FLAGS6502 flag, bool v)
        {

        }


        private UInt16 ToU16Bit(int number)
        {
            return Convert.ToUInt16(number);
        }

        private byte ToByte(int number)
        {
            return Convert.ToByte(number);
        }

    }
}
