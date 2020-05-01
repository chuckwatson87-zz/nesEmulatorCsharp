using System;
namespace nesEmulatorCsharp
{
    public class Bus
    {
        public Olc6502 cpu;

        //Fake ram
        public byte[] RAM = new byte[64 * 1024];

        public Bus()
        {
            //clear RAM contents,
            for (int i = 0; i < RAM.Length; i++)
            {
                RAM[i] = 0x00;
            }
            cpu.ConnectBus(this);
        }

        public void write(UInt16 addr, byte data)
        {
            if (addr >= 0x0000 && addr <= 0xFFFF)
                RAM[addr] = data;
        }

        public byte read(UInt16 addr, bool readoOly = false)
        {
            if (addr >= 0x0000 && addr <= 0xFFFF)
                return RAM[addr];

            return 0x00;
        }
    }

}
