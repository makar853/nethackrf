using System;
using System.Runtime.InteropServices;

namespace nethackrf
{
    public class NetHackrfLow : NetHackrf
    {
        unsafe internal NetHackrfLow(libhackrf.hackrf_device* device) : base(device)
        {

        }
        public enum HackrfChip
        {
            max2837, // direct-conversion zero-IF RF transceiver
            si5351c, // clock generator
            rffc5071 // rffc5071/rffc5072 frequency generator/RF mixer
        }
        public unsafe void WriteReg(HackrfChip chip, UInt16 register, UInt16 value)
        {
            libhackrf.hackrf_error error = libhackrf.hackrf_error.HACKRF_ERROR_OTHER;
            switch (chip)
            {
                case HackrfChip.max2837:
                    error = libhackrf.hackrf_max2837_write(device, (byte)register, value);
                    break;
                case HackrfChip.si5351c:
                    error = libhackrf.hackrf_si5351c_write(device, register, value);
                    break;
                case HackrfChip.rffc5071:
                    error = libhackrf.hackrf_rffc5071_write(device, (byte)register, value);
                    break;
            }
            if (error != libhackrf.hackrf_error.HACKRF_SUCCESS) throw new Exception(error.ToString());
        } // writes register of hackrf chip
        public unsafe UInt16 ReadReg(HackrfChip chip, UInt16 register)
        {
            libhackrf.hackrf_error error = libhackrf.hackrf_error.HACKRF_ERROR_OTHER;
            UInt16* valuePtr = (UInt16*)Marshal.AllocHGlobal(2);
            switch (chip)
            {
                case HackrfChip.max2837:
                    error = libhackrf.hackrf_max2837_read(device, (byte)register, valuePtr);
                    break;
                case HackrfChip.si5351c:
                    error = libhackrf.hackrf_si5351c_read(device, register, valuePtr);
                    break;
                case HackrfChip.rffc5071:
                    error = libhackrf.hackrf_rffc5071_read(device, (byte)register, valuePtr);
                    break;
            }
            UInt16 ret = *valuePtr;
            Marshal.FreeHGlobal((IntPtr)valuePtr);
            if (error != libhackrf.hackrf_error.HACKRF_SUCCESS) throw new Exception(error.ToString());
            return ret;
        } // reads register of hackrf chip

    } // this class defines low-level programming methods
}