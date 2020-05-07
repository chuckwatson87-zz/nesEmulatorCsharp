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

            AbsoluteAddress = ToU16Bit(Read((pointer + 1) << 8) | Read(pointer + 0));

            return 0;
        }

        //Indirect X offset
        public byte IZX()
        {
            byte t = Read(ProgramCounter);
            ProgramCounter++;

            var low = Read((t + XRegister) & 0x00FF);
            var high = Read((t + XRegister + 1) & 0x00FF);

            AbsoluteAddress = ToU16Bit((high << 8) | low);

            return 0;
        }

        //Indirect Y offset
        public byte IZY()
        {
            byte t = Read(ProgramCounter);
            ProgramCounter++;

            var low = Read(t & 0x00FF);
            var high = Read((t + 1) & 0x00FF);

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
            if ((RelativeAddress & 0) != 0)
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

            var low = Read(AbsoluteAddress + 0);
            var hi = Read(AbsoluteAddress + 1);

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
                Write(0x0100 + StackPointer, (ProgramCounter >> 8) & 0x00FF);
                StackPointer--;
                Write(0x0100 + StackPointer, ProgramCounter & 0x00FF);
                StackPointer--;

                SetFlag(FLAGS6502.Break, 0);
                SetFlag(FLAGS6502.Unused, 1);
                SetFlag(FLAGS6502.DisableInterrupts, 1);
                Write(0x0100 + StackPointer, StatusRegister);
                StackPointer--;

                AbsoluteAddress = 0xFFFE;
                var low = Read(AbsoluteAddress + 0);
                var hi = Read(AbsoluteAddress + 1);

                ProgramCounter = ToU16Bit((hi << 8) | low);

                Cycles = 7;

            }
        }

        public void NonMaskableInterruptRequest()
        {
            Write(0x0100 + StackPointer, (ProgramCounter >> 8) & 0x00FF);
            StackPointer--;
            Write(0x0100 + StackPointer, ProgramCounter & 0x00FF);
            StackPointer--;

            SetFlag(FLAGS6502.Break, 0);
            SetFlag(FLAGS6502.Unused, 1);
            SetFlag(FLAGS6502.DisableInterrupts, 1);
            Write(0x0100 + StackPointer, StatusRegister);
            StackPointer--;

            AbsoluteAddress = 0xFFFE;
            var low = Read(AbsoluteAddress + 0);
            var hi = Read(AbsoluteAddress + 1);

            ProgramCounter = ToU16Bit((hi << 8) | low);

            Cycles = 8;
        }

        public byte Restore()
        {
            StackPointer++;
            StatusRegister = Read(0x0100 + StackPointer);
            StatusRegister &= ToByte((int)~FLAGS6502.Break);
            StatusRegister &= ToByte((int)~FLAGS6502.Unused);

            StackPointer++;
            ProgramCounter = Read(0x0100 + StackPointer);
            StackPointer++;
            ProgramCounter |= ToU16Bit(Read(0x0100 + StackPointer) << 8);
            return 0;

        }

        //Instructions
        public byte Fetch()
        {
            //Not sure if this is right
            if(!IsAddressMode())
                Fetched = Read(AbsoluteAddress);

            return Fetched;
        }



        public byte AND()
        {
            Fetch();
            AccumulatorRegister = ToByte(AccumulatorRegister & Fetched);
            SetFlag(FLAGS6502.Zero, AccumulatorRegister == 0x00);
            SetFlag(FLAGS6502.Negative, AccumulatorRegister & 0x80);
            return 1;
        }

        //Arithmetic shift left
        public byte ASL()
        {
            Fetch();
            var temp = Fetched << 1;
            SetFlag(FLAGS6502.Carry, (temp & 0xFF00) > 0);
            SetFlag(FLAGS6502.Zero, (temp & 0x00FF) == 0x00);
            SetFlag(FLAGS6502.Negative, temp & 0x80);

            if(IsAddressMode())
                AccumulatorRegister = ToByte(temp & 0x00FF);
            else
                Write(AbsoluteAddress, temp & 0x00FF);
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
            SetFlag(FLAGS6502.Negative, Fetched & (1 << 7));
            SetFlag(FLAGS6502.Overflow, Fetched & (1 << 6));
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
            SetFlag(FLAGS6502.DisableInterrupts, 1);

            Write(0x0100 + StackPointer, (ProgramCounter >> 8) & 0x00FF);
            StackPointer--;
            Write(0x0100 + StackPointer, ProgramCounter & 0x00FF);
            StackPointer--;

            SetFlag(FLAGS6502.Break, 1);
            Write(0x0100 + StackPointer, StatusRegister);
            StackPointer--;
            SetFlag(FLAGS6502.Break, 0);

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
            SetFlag(FLAGS6502.Carry, 0);
            return 0;
        }


        // Instruction: Clear Decimal Flag
        // Function:    D = 0
        public byte CLD()
        {
            SetFlag(FLAGS6502.DecmalMode, 0);
            return 0;
        }


        // Instruction: Disable Interrupts / Clear Interrupt Flag
        // Function:    I = 0
        public byte CLI()
        {
            SetFlag(FLAGS6502.DisableInterrupts, 0);
            return 0;
        }


        // Instruction: Clear Overflow Flag
        // Function:    V = 0
        public byte CLV()
        {
            SetFlag(FLAGS6502.Overflow, 0);
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
            SetFlag(FLAGS6502.Negative, temp & 0x0080);
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
            SetFlag(FLAGS6502.Negative, temp & 0x0080);
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
            SetFlag(FLAGS6502.Negative, temp & 0x0080);
            return 0;
        }

        // Instruction: Decrement Value at Memory Location
        // Function:    M = M - 1
        // Flags Out:   N, Z
        public byte DEC()
        {
            Fetch();
            var temp = Fetched - 1;
            Write(AbsoluteAddress, temp & 0x00FF);
            SetFlag(FLAGS6502.Zero, (temp & 0x00FF) == 0x0000);
            SetFlag(FLAGS6502.Negative, temp & 0x0080);
            return 0;
        }

        // Instruction: Decrement X Register
        // Function:    X = X - 1
        // Flags Out:   N, Z
        public byte DEX()
        {
            XRegister--;
            SetFlag(FLAGS6502.Zero, XRegister == 0x00);
            SetFlag(FLAGS6502.Negative, XRegister & 0x80);
            return 0;
        }

        // Instruction: Decrement Y Register
        // Function:    Y = Y - 1
        // Flags Out:   N, Z
        public byte DEY()
        {
            YRegister--;
            SetFlag(FLAGS6502.Zero, YRegister == 0x00);
            SetFlag(FLAGS6502.Negative, YRegister & 0x80);
            return 0;
        }

        // Instruction: Bitwise Logic XOR
        // Function:    A = A xor M
        // Flags Out:   N, Z
        public byte EOR()
        {
            Fetch();
            AccumulatorRegister = ToByte(AccumulatorRegister ^ Fetched);
            SetFlag(FLAGS6502.Zero, AccumulatorRegister == 0x00);
            SetFlag(FLAGS6502.Negative, AccumulatorRegister & 0x80);
            return 1;
        }

        // Instruction: Increment Value at Memory Location
        // Function:    M = M + 1
        // Flags Out:   N, Z
        public byte INC()
        {
            Fetch();
            var temp = Fetched + 1;
            Write(AbsoluteAddress, temp & 0x00FF);
            SetFlag(FLAGS6502.Zero, (temp & 0x00FF) == 0x0000);
            SetFlag(FLAGS6502.Negative, temp & 0x0080);
            return 0;
        }

        // Instruction: Increment X Register
        // Function:    X = X + 1
        // Flags Out:   N, Z
        public byte INX()
        {
            XRegister++;
            SetFlag(FLAGS6502.Zero, XRegister == 0x00);
            SetFlag(FLAGS6502.Negative, XRegister & 0x80);
            return 0;
        }

        // Instruction: Increment Y Register
        // Function:    Y = Y + 1
        // Flags Out:   N, Z
        public byte INY()
        {
            YRegister++;
            SetFlag(FLAGS6502.Zero, YRegister == 0x00);
            SetFlag(FLAGS6502.Negative, YRegister & 0x80);
            return 0;
        }

        // Instruction: Jump To Location
        // Function:    pc = address
        public byte JMP()
        {
            ProgramCounter = AbsoluteAddress;
            return 0;
        }

        // Instruction: Jump To Sub-Routine
        // Function:    Push current pc to stack, pc = address
        public byte JSR()
        {
            ProgramCounter--;

            Write(0x0100 + StackPointer, (ProgramCounter >> 8) & 0x00FF);
            StackPointer--;
            Write(0x0100 + StackPointer, ProgramCounter & 0x00FF);
            StackPointer--;

            ProgramCounter = AbsoluteAddress;
            return 0;
        }

        // Instruction: Load The Accumulator
        // Function:    A = M
        // Flags Out:   N, Z
        public byte LDA()
        {
            Fetch();
            AccumulatorRegister = Fetched;
            SetFlag(FLAGS6502.Zero, AccumulatorRegister == 0x00);
            SetFlag(FLAGS6502.Negative, AccumulatorRegister & 0x80);
            return 1;
        }

        // Instruction: Load The X Register
        // Function:    X = M
        // Flags Out:   N, Z
        public byte LDX()
        {
            Fetch();
            XRegister = Fetched;
            SetFlag(FLAGS6502.Zero, XRegister == 0x00);
            SetFlag(FLAGS6502.Negative, XRegister & 0x80);
            return 1;
        }

        // Instruction: Load The Y Register
        // Function:    Y = M
        // Flags Out:   N, Z
        public byte LDY()
        {
            Fetch();
            YRegister = Fetched;
            SetFlag(FLAGS6502.Zero, YRegister == 0x00);
            SetFlag(FLAGS6502.Negative, (YRegister & 0x80) != 0);
            return 1;
        }

        public byte LSR()
        {
            Fetch();
            SetFlag(FLAGS6502.Carry, Fetched & 0x0001);
            var temp = Fetched >> 1;
            SetFlag(FLAGS6502.Zero, (temp & 0x00FF) == 0x0000);
            SetFlag(FLAGS6502.Negative, temp & 0x0080);

            if(IsAddressMode())
                AccumulatorRegister = ToByte(temp & 0x00FF);
            else
                Write(AbsoluteAddress, temp & 0x00FF);
            return 0;
        }

        public byte NOP()
        {
            // Sadly not all NOPs are equal, Ive added a few here
            // based on https://wiki.nesdev.com/w/index.php/CPU_unofficial_opcodes
            // and will add more based on game compatibility, and ultimately
            // I'd like to cover all illegal opcodes too
            switch (CurrentOpCode)
            {
                case 0x1C:
                case 0x3C:
                case 0x5C:
                case 0x7C:
                case 0xDC:
                case 0xFC:
                    return 1;
                default:
                    return 0;
            }
        }

        // Instruction: Bitwise Logic OR
        // Function:    A = A | M
        // Flags Out:   N, Z
        public byte ORA()
        {
            Fetch();
            AccumulatorRegister = ToByte(AccumulatorRegister | Fetched);
            SetFlag(FLAGS6502.Zero, AccumulatorRegister == 0x00);
            SetFlag(FLAGS6502.Negative, AccumulatorRegister & 0x80);
            return 1;
        }


        public byte ADC()
        {
            Fetch();
            var temp = AccumulatorRegister + Fetched + GetFlag(FLAGS6502.Carry);

            SetFlag(FLAGS6502.Carry, temp > 255);
            SetFlag(FLAGS6502.Zero, (temp & 0x00FF) == 0);
            SetFlag(FLAGS6502.Negative, temp & 0x80);
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
            SetFlag(FLAGS6502.Negative, temp & 0x80);
            //Set overflow
            //(~((uint16_t)a ^ (uint16_t)fetched) & ((uint16_t)a ^ (uint16_t)temp)) & 0x0080;
            var doSetOverFlow = ((~(AccumulatorRegister ^ Fetched) & (AccumulatorRegister ^ temp)) & 0x0080) != 0;
            SetFlag(FLAGS6502.Overflow, doSetOverFlow);
            AccumulatorRegister = ToByte(temp & 0x00FF);
            return 1;
        }

        // Instruction: Push Accumulator to Stack
        // Function:    A -> stack
        public byte PHA()
        {
            Write(0x0100 + StackPointer, AccumulatorRegister);
            StackPointer--;
            return 0;
        }

        // Instruction: Push Status Register to Stack
        // Function:    status -> stack
        // Note:        Break flag is set to 1 before push
        public byte PHP()
        {
            Write(0x0100 + StackPointer, StatusRegister | ToByte((int)FLAGS6502.Break) | ToByte((int)FLAGS6502.Unused));
            SetFlag(FLAGS6502.Break, 0);
            SetFlag(FLAGS6502.Unused, 0);
            StackPointer--;
            return 0;
        }

        // Instruction: Pop Accumulator off Stack
        // Function:    A <- stack
        // Flags Out:   N, Z
        public byte PLA()
        {
            StackPointer++;
            AccumulatorRegister = Read(0x0100 + StackPointer);
            SetFlag(FLAGS6502.Zero, AccumulatorRegister == 0x00);
            SetFlag(FLAGS6502.Negative, AccumulatorRegister & 0x80);
            return 0;
        }

        // Instruction: Pop Status Register off Stack
        // Function:    Status <- stack
        public byte PLP()
        {
            StackPointer++;
            StatusRegister = Read(0x0100 + StackPointer);
            SetFlag(FLAGS6502.Unused, 1);
            return 0;
        }

        public byte ROL()
        {
            Fetch();
            var temp = ToU16Bit(Fetched << 1) | GetFlag(FLAGS6502.Carry);
            SetFlag(FLAGS6502.Carry, temp & 0xFF00);
            SetFlag(FLAGS6502.Zero, (temp & 0x00FF) == 0x0000);
            SetFlag(FLAGS6502.Negative, temp & 0x0080);

            if(IsAddressMode())
                AccumulatorRegister = ToByte(temp & 0x00FF);
            else
                Write(AbsoluteAddress, ToByte(temp & 0x00FF));
            return 0;
        }

        public byte ROR()
        {
            Fetch();
            var temp = ToU16Bit(GetFlag(FLAGS6502.Carry) << 7) | (Fetched >> 1);
            SetFlag(FLAGS6502.Carry, Fetched & 0x01);
            SetFlag(FLAGS6502.Zero, (temp & 0x00FF) == 0x00);
            SetFlag(FLAGS6502.Negative, temp & 0x0080);

            if (IsAddressMode())
                AccumulatorRegister = ToByte(temp & 0x00FF);
            else
                Write(AbsoluteAddress, temp & 0x00FF);
            return 0;
        }

        public byte RTI()
        {
            StackPointer++;
            StatusRegister = Read(0x0100 + StackPointer);
            StatusRegister &= ToByte(~(int)FLAGS6502.Break);
            StatusRegister &= ToByte(~(int)FLAGS6502.Unused);

            StackPointer++;
            ProgramCounter = ToU16Bit(Read(0x0100 + StackPointer));
            StackPointer++;
            ProgramCounter |= ToU16Bit(Read(0x0100 + StackPointer) << 8);
            return 0;
        }

        public byte RTS()
        {
            StackPointer++;
            ProgramCounter = ToU16Bit(Read(0x0100 + StackPointer));
            StackPointer++;
            ProgramCounter |= ToU16Bit(Read(0x0100 + StackPointer) << 8);

            ProgramCounter++;
            return 0;
        }

        // Instruction: Set Carry Flag
        // Function:    C = 1
        public byte SEC()
        {
            SetFlag(FLAGS6502.Carry, true);
            return 0;
        }


        // Instruction: Set Decimal Flag
        // Function:    D = 1
        public byte SED()
        {
            SetFlag(FLAGS6502.DecmalMode, true);
            return 0;
        }

        // Instruction: Set Interrupt Flag / Enable Interrupts
        // Function:    I = 1
        public byte SEI()
        {
            SetFlag(FLAGS6502.DisableInterrupts, true);
            return 0;
        }

        // Instruction: Store Accumulator at Address
        // Function:    M = A
        public byte STA()
        {
            Write(AbsoluteAddress, AccumulatorRegister);
            return 0;
        }

        // Instruction: Store X Register at Address
        // Function:    M = X
        public byte STX()
        {
            Write(AbsoluteAddress, XRegister);
            return 0;
        }

        // Instruction: Store Y Register at Address
        // Function:    M = Y
        public byte STY()
        {
            Write(AbsoluteAddress, YRegister);
            return 0;
        }

        // Instruction: Transfer Accumulator to X Register
        // Function:    X = A
        // Flags Out:   N, Z
        public byte TAX()
        {
            XRegister = AccumulatorRegister;
            SetFlag(FLAGS6502.Zero, XRegister == 0x00);
            SetFlag(FLAGS6502.Negative, XRegister & 0x80);
            return 0;
        }

        // Instruction: Transfer Accumulator to Y Register
        // Function:    Y = A
        // Flags Out:   N, Z
        public byte TAY()
        {
            YRegister = AccumulatorRegister;
            SetFlag(FLAGS6502.Zero, YRegister == 0x00);
            SetFlag(FLAGS6502.Negative, YRegister & 0x80);
            return 0;
        }

        // Instruction: Transfer Stack Pointer to X Register
        // Function:    X = stack pointer
        // Flags Out:   N, Z
        public byte TSX()
        {
            XRegister = StackPointer;
            SetFlag(FLAGS6502.Zero, XRegister == 0x00);
            SetFlag(FLAGS6502.Negative, XRegister & 0x80);
            return 0;
        }

        // Instruction: Transfer X Register to Accumulator
        // Function:    A = X
        // Flags Out:   N, Z
        public byte TXA()
        {
            AccumulatorRegister = XRegister;
            SetFlag(FLAGS6502.Zero, XRegister == 0x00);
            SetFlag(FLAGS6502.Negative, XRegister & 0x80);
            return 0;
        }

        // Instruction: Transfer X Register to Stack Pointer
        // Function:    stack pointer = X
        public byte TXS()
        {
            StackPointer = XRegister;
            return 0;
        }

        // Instruction: Transfer Y Register to Accumulator
        // Function:    A = Y
        // Flags Out:   N, Z
        public byte TYA()
        {
            AccumulatorRegister = YRegister;
            SetFlag(FLAGS6502.Zero, XRegister == 0x00);
            SetFlag(FLAGS6502.Negative, XRegister & 0x80);
            return 0;
        }


        private void Write(int address, int data)
        {
            Bus.write(ToU16Bit(address), ToByte(data));
        }

        private byte Read(int address)
        {
            return Bus.Read(ToU16Bit(address));
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

        private void SetFlag(FLAGS6502 flag, int value)
        {
            SetFlag(flag, value != 0);
        }


        private UInt16 ToU16Bit(int number)
        {
            return Convert.ToUInt16(number);
        }

        private byte ToByte(int number)
        {
            return Convert.ToByte(number);
        }

        private bool IsAddressMode(string addressMode = "IMP")
        {
            return InstructionLookup[CurrentOpCode].AddressModeFunc.Method.Name == addressMode;
        }

        // This is the disassembly function. Its workings are not required for emulation.
        // It is merely a convenience function to turn the binary instruction code into
        // human readable form. Its included as part of the emulator because it can take
        // advantage of many of the CPUs internal operations to do this.
        Dictionary<UInt16, string>disassemble(UInt16 nStart, UInt16 nStop)
        {
            UInt32 addr = nStart;
            byte value = 0x00, lo = 0x00, hi = 0x00;
            Dictionary<UInt16, string> mapLines;
            UInt16 line_addr = 0;

            // Starting at the specified address we read an instruction
            // byte, which in turn yields information from the lookup table
            // as to how many additional bytes we need to read and what the
            // addressing mode is. I need this info to assemble human readable
            // syntax, which is different depending upon the addressing mode

            // As the instruction is decoded, a std::string is assembled
            // with the readable output
            while (addr <= Convert.ToInt32(nStop))
            {
                line_addr = Convert.ToUInt16(addr);

                // Prefix line with instruction address
                var sInst = "$" + addr.ToString("X").PadLeft(0, '4') + ": ";

                // Read instruction, and get its readable name
                var opcode = Bus.Read(Convert.ToUInt16(addr), true); addr++;
                sInst += InstructionLookup[opcode].Name + " ";

                // Get oprands from desired locations, and form the
                // instruction based upon its addressing mode. These
                // routines mimmick the actual fetch routine of the
                // 6502 in order to get accurate data as part of the
                // instruction
                if (IsAddressMode())
                {
                    sInst += " {IMP}";
                }
                else if (IsAddressMode("IMM"))
                {
                    value = Bus.Read(addr, true); addr++;
                    sInst += "#$" + hex(value, 2) + " {IMM}";
                }
                else if (lookup[opcode].addrmode == &olc6502::ZP0)
                {
                    lo = bus->read(addr, true); addr++;
                    hi = 0x00;
                    sInst += "$" + hex(lo, 2) + " {ZP0}";
                }
                else if (lookup[opcode].addrmode == &olc6502::ZPX)
                {
                    lo = bus->read(addr, true); addr++;
                    hi = 0x00;
                    sInst += "$" + hex(lo, 2) + ", X {ZPX}";
                }
                else if (lookup[opcode].addrmode == &olc6502::ZPY)
                {
                    lo = bus->read(addr, true); addr++;
                    hi = 0x00;
                    sInst += "$" + hex(lo, 2) + ", Y {ZPY}";
                }
                else if (lookup[opcode].addrmode == &olc6502::IZX)
                {
                    lo = bus->read(addr, true); addr++;
                    hi = 0x00;
                    sInst += "($" + hex(lo, 2) + ", X) {IZX}";
                }
                else if (lookup[opcode].addrmode == &olc6502::IZY)
                {
                    lo = bus->read(addr, true); addr++;
                    hi = 0x00;
                    sInst += "($" + hex(lo, 2) + "), Y {IZY}";
                }
                else if (lookup[opcode].addrmode == &olc6502::ABS)
                {
                    lo = bus->read(addr, true); addr++;
                    hi = bus->read(addr, true); addr++;
                    sInst += "$" + hex((uint16_t)(hi << 8) | lo, 4) + " {ABS}";
                }
                else if (lookup[opcode].addrmode == &olc6502::ABX)
                {
                    lo = bus->read(addr, true); addr++;
                    hi = bus->read(addr, true); addr++;
                    sInst += "$" + hex((uint16_t)(hi << 8) | lo, 4) + ", X {ABX}";
                }
                else if (lookup[opcode].addrmode == &olc6502::ABY)
                {
                    lo = bus->read(addr, true); addr++;
                    hi = bus->read(addr, true); addr++;
                    sInst += "$" + hex((uint16_t)(hi << 8) | lo, 4) + ", Y {ABY}";
                }
                else if (lookup[opcode].addrmode == &olc6502::IND)
                {
                    lo = bus->read(addr, true); addr++;
                    hi = bus->read(addr, true); addr++;
                    sInst += "($" + hex((uint16_t)(hi << 8) | lo, 4) + ") {IND}";
                }
                else if (lookup[opcode].addrmode == &olc6502::REL)
                {
                    value = bus->read(addr, true); addr++;
                    sInst += "$" + hex(value, 2) + " [$" + hex(addr + value, 4) + "] {REL}";
                }

                // Add the formed string to a std::map, using the instruction's
                // address as the key. This makes it convenient to look for later
                // as the instructions are variable in length, so a straight up
                // incremental index is not sufficient.
                mapLines[line_addr] = sInst;
            }

            return mapLines;
        }

    }
}
