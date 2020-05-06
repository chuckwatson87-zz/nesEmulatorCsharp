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

        private byte Read(UInt16 a)
        {

            return Bus.Read(a);
        }

        //Addressing Modes, more going here, with better names
        //Implied
        public byte IMP()
        {
            Fetched = AccumulatorRegister;
            return 0;
        }

        //Immediate
        public byte IMM()
        {
            AbsoluteAddress = ProgramCounter++;
            return 0;
        }

        //Zero Page
        public byte ZP0()
        {
            AbsoluteAddress = Read(ProgramCounter);
            ProgramCounter++;
            AbsoluteAddress &= 0x00FF;
            return 0;
        }

        //Zero Page X offset
        public byte ZPX()
        {
            AbsoluteAddress = ToU16Bit(Read(ProgramCounter) + XRegister);
            ProgramCounter++;
            AbsoluteAddress &= 0x00FF;
            return 0;
        }

        //Zero Page Y offset
        public byte ZPY()
        {
            AbsoluteAddress = ToU16Bit(Read(ProgramCounter) + YRegister);
            ProgramCounter++;
            AbsoluteAddress &= 0x00FF;
            return 0;
        }

        //Absolute
        public byte ABS() {
            byte low = Read(ProgramCounter);
            ProgramCounter++;
            byte hi = Read(ProgramCounter);
            ProgramCounter++;

            AbsoluteAddress = ToU16Bit((hi << 8) | low);

            return 0;
        }

        //Absolute X offset
        public byte ABX()
        {
            byte low = Read(ProgramCounter);
            ProgramCounter++;
            byte hi = Read(ProgramCounter);
            ProgramCounter++;

            AbsoluteAddress = ToU16Bit((hi << 8) | low);
            AbsoluteAddress += XRegister;

            if ((AbsoluteAddress & 0xFF00) != (hi << 8))
            {
                return 1;
            }
            else
            {
                return 0;
            }

        }

        //Absolute Y offset
        public byte ABY()
        {
            byte low = Read(ProgramCounter);
            ProgramCounter++;
            byte hi = Read(ProgramCounter);
            ProgramCounter++;

            AbsoluteAddress = ToU16Bit((hi << 8) | low);
            AbsoluteAddress += YRegister;

            if ((AbsoluteAddress & 0xFF00) != (hi << 8))
            {
                return 1;
            }
            else
            {
                return 0;
            }

        }

        //Indirect
        public byte IND()
        {
            byte low = Read(ProgramCounter);
            ProgramCounter++;
            byte hi = Read(ProgramCounter);
            ProgramCounter++;

            var pointer = ToU16Bit((hi << 8) | low);

            AbsoluteAddress = ToU16Bit(Read(ToU16Bit((pointer + 1) << 8)) | Read(ToU16Bit(pointer + 0)));

            return 0;
        }

        //Indirect X offset
        public byte IZX()
        {
            byte t = Read(ProgramCounter);
            ProgramCounter++;

            var low = Read(ToU16Bit((t + XRegister) & 0x00FF));
            var high = Read(ToU16Bit((t + XRegister + 1) & 0x00FF));

            AbsoluteAddress = ToU16Bit((high << 8) | low);

            return 0;
        }

        //Indirect Y offset
        public byte IZY()
        {
            byte t = Read(ProgramCounter);
            ProgramCounter++;

            var low = Read(ToU16Bit(t & 0x00FF));
            var high = Read(ToU16Bit((t + 1) & 0x00FF));

            AbsoluteAddress = ToU16Bit((high << 8) | low);
            AbsoluteAddress += YRegister;

            if ((AbsoluteAddress & 0xFF00) != (high << 8))
            {
                return 1;
            }
            else
            {
                return 0;
            }

        }

        //Relative
        public byte REL()
        {
            RelativeAddress = Read(ProgramCounter);
            ProgramCounter++;
            //not sure if this is right
            if (IsBitSet(ToByte(RelativeAddress), 0))
            {
                RelativeAddress |= 0xFF00;
            }
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
                CurrentOpCode = Read(ProgramCounter);
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
            AccumulatorRegister = 0;
            XRegister = 0;
            YRegister = 0;
            StackPointer = 0xFD;
            StatusRegister = ToByte(0x00 | (int)FLAGS6502.Carry);

            AbsoluteAddress = 0xFFFC;

            var low = Read(ToU16Bit(AbsoluteAddress + 0));
            var hi = Read(ToU16Bit(AbsoluteAddress + 1));

            ProgramCounter = ToU16Bit((hi << 8) | low);

            AbsoluteAddress = 0x0000;
            RelativeAddress = 0x0000;
            Fetched = 0x00;

            Cycles = 8;
        }

        public void InterruptRequest()
        {
            if(GetFlag(FLAGS6502.DisableInterrupts) == 0)
            {
                Write(ToU16Bit(0x0100 + StackPointer), ToByte((ProgramCounter >> 8) & 0x00FF));
                StackPointer--;
                Write(ToU16Bit(0x0100 + StackPointer), ToByte(ProgramCounter & 0x00FF));
                StackPointer--;

                SetFlag(FLAGS6502.Break, false);
                SetFlag(FLAGS6502.Unused, true);
                SetFlag(FLAGS6502.DisableInterrupts, true);
                Write(ToU16Bit(0x0100 + StackPointer), StatusRegister);
                StackPointer--;

                AbsoluteAddress = 0xFFFE;
                var low = Read(ToU16Bit(AbsoluteAddress + 0));
                var hi = Read(ToU16Bit(AbsoluteAddress + 1));

                ProgramCounter = ToU16Bit((hi << 8) | low);

                Cycles = 7;

            }
        }

        public void NonMaskableInterruptRequest()
        {
            Write(ToU16Bit(0x0100 + StackPointer), ToByte((ProgramCounter >> 8) & 0x00FF));
            StackPointer--;
            Write(ToU16Bit(0x0100 + StackPointer), ToByte(ProgramCounter & 0x00FF));
            StackPointer--;

            SetFlag(FLAGS6502.Break, false);
            SetFlag(FLAGS6502.Unused, true);
            SetFlag(FLAGS6502.DisableInterrupts, true);
            Write(ToU16Bit(0x0100 + StackPointer), StatusRegister);
            StackPointer--;

            AbsoluteAddress = 0xFFFE;
            var low = Read(ToU16Bit(AbsoluteAddress + 0));
            var hi = Read(ToU16Bit(AbsoluteAddress + 1));

            ProgramCounter = ToU16Bit((hi << 8) | low);

            Cycles = 8;
        }

        public byte Restore()
        {
            StackPointer++;
            StatusRegister = Read(ToU16Bit(0x0100 + StackPointer));
            StatusRegister &= ToByte((int)~FLAGS6502.Break);
            StatusRegister &= ToByte((int)~FLAGS6502.Unused);

            StackPointer++;
            ProgramCounter = Read(ToU16Bit(0x0100 + StackPointer));
            StackPointer++;
            ProgramCounter |= ToU16Bit(Read(ToU16Bit(0x0100 + StackPointer)) << 8);
            return 0;

        }

        //Instructions
        public byte Fetch()
        {
            //Not sure if this is right
            var AddressModeName = InstructionLookup[CurrentOpCode].AddressModeFunc.Method.Name;
            if (AddressModeName != "IMP")
                Fetched = Read(AbsoluteAddress);

            return Fetched;
        }



        public byte AND()
        {
            Fetch();
            AccumulatorRegister = ToByte(AccumulatorRegister & Fetched);
            SetFlag(FLAGS6502.Zero, AccumulatorRegister == 0x00);
            var negativeFlag = Convert.ToBoolean(AccumulatorRegister & 0x80);
            SetFlag(FLAGS6502.Negative, negativeFlag);
            return 1;
        }

        //Arithmetic shift left
        public byte ASL()
        {
            Fetch();
            var temp = Fetched << 1;
            SetFlag(FLAGS6502.Carry, (temp & 0xFF00) > 0);
            SetFlag(FLAGS6502.Zero, (temp & 0x00FF) == 0x00);
            SetFlag(FLAGS6502.Negative, ToByte(temp & 0x80) != 0);

            var addressModeName = InstructionLookup[CurrentOpCode].AddressModeFunc.Method.Name;
            if (addressModeName == "IMP")
                AccumulatorRegister = ToByte(temp & 0x00FF);
            else
                Write(AbsoluteAddress, ToByte(temp & 0x00FF));
            return 0;
        }

        // Instruction: Branch if Carry Clear
        // Function:    if(C == 0) pc = address 
        public Byte BCC()
        {
            if (GetFlag(FLAGS6502.Carry) == 0)
            {
                Cycles++;
                AbsoluteAddress = ToU16Bit(ProgramCounter + RelativeAddress);

                if ((AbsoluteAddress & 0xFF00) != (ProgramCounter & 0xFF00))
                    Cycles++;

                ProgramCounter = AbsoluteAddress;
            }
            return 0;
        }

        // Instruction: Branch if Carry Set
        // Function:    if(C == 1) pc = address
        public byte BCS()
        {
            if(GetFlag(FLAGS6502.Carry) == 1)
            {
                Cycles++;
                AbsoluteAddress = ToU16Bit(ProgramCounter + RelativeAddress);

                if ((AbsoluteAddress & 0xFF00) != (ProgramCounter & 0xFF00)) {
                    Cycles++;
                }

                ProgramCounter = AbsoluteAddress;
            }
            return 0;
        }

        // Instruction: Branch if Equal
        // Function:    if(Z == 1) pc = address
        public byte BEQ()
        {
            if (GetFlag(FLAGS6502.Zero) == 1)
            {
                Cycles++;
                AbsoluteAddress = ToU16Bit(ProgramCounter + RelativeAddress);

                if ((AbsoluteAddress & 0xFF00) != (ProgramCounter & 0xFF00))
                {
                    Cycles++;
                }

                ProgramCounter = AbsoluteAddress;
            }
            return 0;
        }

        public byte BIT()
        {
            Fetch();
            var temp = AccumulatorRegister & Fetched;
            SetFlag(FLAGS6502.Zero, (temp & 0x00FF) == 0x00);
            SetFlag(FLAGS6502.Negative, (Fetched & (1 << 7)) != 0);
            SetFlag(FLAGS6502.Overflow, (Fetched & (1 << 6)) != 0);
            return 0;
        }

        // Instruction: Branch if Negative
        // Function:    if(N == 1) pc = address
        public byte BMI()
        {
            if (GetFlag(FLAGS6502.Negative) == 1)
            {
                Cycles++;
                AbsoluteAddress = ToU16Bit(ProgramCounter + RelativeAddress);

                if ((AbsoluteAddress & 0xFF00) != (ProgramCounter & 0xFF00))
                {
                    Cycles++;
                }

                ProgramCounter = AbsoluteAddress;
            }
            return 0;
        }

        // Instruction: Branch if Not Equal
        // Function:    if(Z == 0) pc = address
        public byte BNE()
        {
            if (GetFlag(FLAGS6502.Zero) == 0)
            {
                Cycles++;
                AbsoluteAddress = ToU16Bit(ProgramCounter + RelativeAddress);

                if ((AbsoluteAddress & 0xFF00) != (ProgramCounter & 0xFF00))
                {
                    Cycles++;
                }

                ProgramCounter = AbsoluteAddress;
            }
            return 0;
        }

        // Instruction: Branch if Positive
        // Function:    if(N == 0) pc = address
        public byte BPL()
        {
            if (GetFlag(FLAGS6502.Negative) == 0)
            {
                Cycles++;
                AbsoluteAddress = ToU16Bit(ProgramCounter + RelativeAddress);

                if ((AbsoluteAddress & 0xFF00) != (ProgramCounter & 0xFF00))
                {
                    Cycles++;
                }

                ProgramCounter = AbsoluteAddress;
            }
            return 0;
        }

        // Instruction: Break
        // Function:    Program Sourced Interrupt
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
            ProgramCounter = ToU16Bit(Read(0xFFFE) | (Read(0xFFFF) << 8));

            return 0;
        }

        // Instruction: Branch if Overflow Clear
        // Function:    if(V == 0) pc = address
        public byte BVC()
        {
            if (GetFlag(FLAGS6502.Overflow) == 0)
            {
                Cycles++;
                AbsoluteAddress = ToU16Bit(ProgramCounter + RelativeAddress);

                if ((AbsoluteAddress & 0xFF00) != (ProgramCounter & 0xFF00))
                {
                    Cycles++;
                }

                ProgramCounter = AbsoluteAddress;
            }
            return 0;
        }

        // Instruction: Branch if Overflow Set
        // Function:    if(V == 1) pc = address
        public byte BVS()
        {
            if (GetFlag(FLAGS6502.Overflow) == 1)
            {
                Cycles++;
                AbsoluteAddress = ToU16Bit(ProgramCounter + RelativeAddress);

                if ((AbsoluteAddress & 0xFF00) != (ProgramCounter & 0xFF00))
                {
                    Cycles++;
                }

                ProgramCounter = AbsoluteAddress;
            }
            return 0;
        }

        // Instruction: Clear Carry Flag
        // Function:    C = 0
        public byte CLC()
        {
            SetFlag(FLAGS6502.Carry, false);
            return 0;
        }


        // Instruction: Clear Decimal Flag
        // Function:    D = 0
        public byte CLD()
        {
            SetFlag(FLAGS6502.DecmalMode, false);
            return 0;
        }


        // Instruction: Disable Interrupts / Clear Interrupt Flag
        // Function:    I = 0
        public byte CLI()
        {
            SetFlag(FLAGS6502.DisableInterrupts, false);
            return 0;
        }


        // Instruction: Clear Overflow Flag
        // Function:    V = 0
        public byte CLV()
        {
            SetFlag(FLAGS6502.Overflow, false);
            return 0;
        }

        // Instruction: Compare Accumulator
        // Function:    C <- A >= M      Z <- (A - M) == 0
        // Flags Out:   N, C, Z
        public byte CMP()
        {
            Fetch();
            var temp = AccumulatorRegister - Fetched;
            SetFlag(FLAGS6502.Carry, AccumulatorRegister >= Fetched);
            SetFlag(FLAGS6502.Zero, (temp & 0x00FF) == 0x0000);
            SetFlag(FLAGS6502.Negative, (temp & 0x0080) != 0);
            return 1;
        }

        // Instruction: Compare X Register
        // Function:    C <- X >= M      Z <- (X - M) == 0
        // Flags Out:   N, C, Z
        public byte CPX()
        {
            Fetch();
            var temp = XRegister - Fetched;
            SetFlag(FLAGS6502.Carry, XRegister >= Fetched);
            SetFlag(FLAGS6502.Zero, (temp & 0x00FF) == 0x0000);
            SetFlag(FLAGS6502.Negative, (temp & 0x0080) != 0);
            return 0;
        }

        // Instruction: Compare Y Register
        // Function:    C <- X >= M      Z <- (X - M) == 0
        // Flags Out:   N, C, Z
        public byte CPY()
        {
            Fetch();
            var temp = YRegister - Fetched;
            SetFlag(FLAGS6502.Carry, YRegister >= Fetched);
            SetFlag(FLAGS6502.Zero, (temp & 0x00FF) == 0x0000);
            SetFlag(FLAGS6502.Negative, (temp & 0x0080) != 0);
            return 0;
        }


        public byte ADC()
        {
            Fetch();
            var temp = AccumulatorRegister + Fetched + GetFlag(FLAGS6502.Carry);

            SetFlag(FLAGS6502.Carry, temp > 255);
            SetFlag(FLAGS6502.Zero, (temp & 0x00FF) == 0);
            SetFlag(FLAGS6502.Negative, Convert.ToBoolean(temp & 0x80));
            //Set overflow
            AccumulatorRegister = ToByte(temp & 0x00FF);
            return 1;
        }

        public byte SBC()
        {
            var invertedFetched = Fetched ^ 0x00FF;

            var temp = AccumulatorRegister + invertedFetched + GetFlag(FLAGS6502.Carry);

            SetFlag(FLAGS6502.Carry, temp > 255);
            SetFlag(FLAGS6502.Zero, (temp & 0x00FF) == 0);
            SetFlag(FLAGS6502.Negative, Convert.ToBoolean(temp & 0x80));
            //Set overflow
            //(~((uint16_t)a ^ (uint16_t)fetched) & ((uint16_t)a ^ (uint16_t)temp)) & 0x0080;
            var doSetOverFlow = ((~(AccumulatorRegister ^ Fetched) & (AccumulatorRegister ^ temp)) & 0x0080) != 0;
            SetFlag(FLAGS6502.Overflow, doSetOverFlow);
            AccumulatorRegister = ToByte(temp & 0x00FF);
            return 1;
        }

        public byte PHA()
        {
            Write(ToU16Bit(0x0100 + StackPointer), AccumulatorRegister);
            StackPointer--;
            return 0;
        }

        public byte PLA()
        {
            StackPointer++;
            AccumulatorRegister = Read(ToU16Bit(0x0100 + StackPointer));
            SetFlag(FLAGS6502.Zero, AccumulatorRegister == 0x00);
            SetFlag(FLAGS6502.Negative, Convert.ToBoolean(AccumulatorRegister & 0x80));
            return 0;
        }

        private void Write(UInt16 a, byte d)
        {
            Bus.write(a, d);
        }

        private byte GetFlag(FLAGS6502 flag)
        {
            var flagValue = ((StatusRegister & ToByte((int)flag)) > 0) ? 1: 0;
            return ToByte(flagValue);
        }

        private void SetFlag(FLAGS6502 flag, bool value)
        {
            if (value == true)
            {
                StatusRegister |= ToByte((int)flag);
            }
            else
            {
                StatusRegister &= ToByte((int)~flag);
            }
        }


        private UInt16 ToU16Bit(int number)
        {
            return Convert.ToUInt16(number);
        }

        private byte ToByte(int number)
        {
            return Convert.ToByte(number);
        }

        private bool IsBitSet(byte b, int pos)
        {
            return (b & (1 << pos)) != 0;
        }

    }
}
