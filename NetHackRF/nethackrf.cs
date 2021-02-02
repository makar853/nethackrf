using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Security;
using System.Runtime.InteropServices;

namespace nethackrf
{

    public class NetHackrf : IDisposable // hackrf device class
    {
        private class StaticFinalyzer // crutch to make static destructor
        {
            ~StaticFinalyzer() // this destructor will run at programs end to free hackrf.dll
            {
                libhackrf.hackrf_exit();
            }
        }
        private static StaticFinalyzer finalyzer = new StaticFinalyzer();
        static NetHackrf() // static constructor for initializing hackrf.dll 
        {
            CheckHackrfError(libhackrf.hackrf_init());
        }
        public enum hackrf_board
        {
            USB_BOARD_ID_JAWBREAKER = 0x604B,
            USB_BOARD_ID_HACKRF_ONE = 0x6089,
            USB_BOARD_ID_RAD1O = 0xCC15,
            USB_BOARD_ID_INVALID = 0xFFFF,
        };
        public enum transceiver_mode_t
        {
            OFF = 0,
            RX = 1,
            TX = 2,
            SS = 3,
            CPLD_UPDATE = 4
        };

        // NetHackrf class fields
        internal unsafe libhackrf.hackrf_device* device;
        internal transceiver_mode_t mode;
        internal bool TxStarted;
        public hackrf_sweep_info sweepInfo;
        private bool disposed;
        private HackRFStream stream;
        public class hackrf_sweep_info // structure for sweep options
        {
            public List<Tuple<double, double>> rangesMHz = new List<Tuple<double, double>>();
            public UInt32 StepSamples { get => stepSamples; 
                set {
                    UInt32 blocks = value / libhackrf.SAMPLES_PER_BLOCK;
                    if (blocks == 0) blocks = 1;
                    stepSamples = blocks * libhackrf.SAMPLES_PER_BLOCK;
                }
            }// samples per step should be a multiple of 16384
            internal UInt32 stepSamples;
            public double stepMHz = 0;
            public double offsetMHz = 0;
            public bool interpolating = false;
            public void ResetRanges()
            {
                rangesMHz = new List<Tuple<double, double>>();
            }
            public void AddRange( double StartFrequencyMHz, double StopFrequencyMHz)
            {
                rangesMHz.Add(new Tuple<double, double>(StartFrequencyMHz, StopFrequencyMHz));
            }
        }
        public class hackrf_device_info // this class is needed for devices enumeration
        {
            unsafe public NetHackrf OpenDevice()
            {
                libhackrf.hackrf_device* device = null;
                byte[] serial = System.Text.Encoding.ASCII.GetBytes(serial_number);
                fixed (byte* serptr = serial)
                {
                    CheckHackrfError(libhackrf.hackrf_open_by_serial(serptr, &device));
                }
                return new NetHackrf(device);
            }
            public string serial_number;
            public hackrf_board usb_board_id;
            public int usb_device_index;
        };
        unsafe internal NetHackrf(libhackrf.hackrf_device* device) // NetHackrf class constructor. hackrf_device_info.OpenDevice() is needed to create NetHackrf object
        {
            this.device = device;
            mode = transceiver_mode_t.OFF;
            TxStarted = false;
            disposed = false;
            sweepInfo = new hackrf_sweep_info();
        }
        ~NetHackrf() // NetHackrf class destructor
        {
            Dispose();
        }
        unsafe public void Dispose() // NetHackrf.Dispose() or HackRFStream.Dispose() is needed to properly stop hackrf from transmitting/receiving.
        {
            if (disposed == false)
            {
                if (mode != transceiver_mode_t.OFF)
                {
                    stream.Dispose();
                }
                CheckHackrfError(libhackrf.hackrf_close(device));
                disposed = true;
            }
        } 
        unsafe private static string PtrToStr(byte* data) // transforms C string pointer to .net string
        {
            byte* bytes = data;
            int counter = 0;
            System.Text.StringBuilder ret = new System.Text.StringBuilder();
            while (*bytes != 0)
            {
                counter++;
                bytes++;
            }
            return System.Text.Encoding.ASCII.GetString(data, counter);
        }
        unsafe public static string HackrfLibraryVersion() // gets hackrf.dll version
        {
            return PtrToStr(libhackrf.hackrf_library_version());
        }
        unsafe public static string HackrfLibraryRelease() // gets hackrf.dll release name
        {
            return PtrToStr(libhackrf.hackrf_library_release());
        }
        unsafe public static hackrf_device_info[] HackrfDeviceList() // Enumerates connected hackrf devices
        {
            libhackrf.hackrf_device_list_t* ptr = libhackrf.hackrf_device_list();
            if (ptr == null) return new hackrf_device_info[0];
            libhackrf.hackrf_device_list_t devs = *ptr;
            hackrf_device_info[] ret = new hackrf_device_info[devs.devicecount];
            for (int i = 0; i < devs.devicecount; i++)
            {
                hackrf_device_info dev = new hackrf_device_info {
                    serial_number = PtrToStr(devs.serial_numbers[i]),
                    usb_board_id = (hackrf_board)devs.usb_board_ids[i],
                    usb_device_index = devs.usb_device_index[i]
                };
                ret[i] = dev;
            }
            return ret;
        }
        internal static void CheckHackrfError(libhackrf.hackrf_error error) // throws exceptions when hackRF error occurs
        {
            if (error != libhackrf.hackrf_error.HACKRF_SUCCESS)
            {
                string method_name = new System.Diagnostics.StackTrace().GetFrame(1).GetMethod().Name;
                throw new Exception($"Error \"{error}\" have occured in {method_name}.");
            }
        } 
        const Int32 api_version_length = 64;
        // most of hackrf functions are made as properties
        unsafe public UInt16 UsbApiVersion // returns hackrf usb api version
        {
            get
            {
                UInt16 version;
                libhackrf.hackrf_usb_api_version_read(device, &version);
                return version;
            }
        } 
        private void CheckVersion()
        {
            if (UsbApiVersion < 0x0102)
            {
                throw new Exception($"Current USB API version is too old (0x{UsbApiVersion:X}). Minimal version is 0x1002.");
            }
        }
        unsafe public string HackrfVersion // returns hackrf firmware version
        {
            get
            {
                byte[] version = new byte[api_version_length];
                fixed (byte* fixed_ver = version)
                {
                    libhackrf.hackrf_version_string_read(device, fixed_ver, api_version_length);
                }
                return System.Text.Encoding.ASCII.GetString(version);
            }
        } 
        unsafe public double FilterBandwidthMHz // configures internal filter
        {
            set {
                CheckHackrfError(libhackrf.hackrf_set_baseband_filter_bandwidth(device, (UInt32)(value * 1000000f)));
            }
        } 
        unsafe public double CarrierFrequencyMHz // sets carrier frequency in MHz
        {
            set
            {
                CheckHackrfError(libhackrf.hackrf_set_freq(device, (UInt64)(value * 1000000f)));
            }
        } 
        unsafe public double SampleFrequencyMHz // sets sampling frequency in MHz
        {
            set
            {
                CheckHackrfError(libhackrf.hackrf_set_sample_rate(device, value * 1000000f));
            }
        } 
        unsafe public bool AntPower // sets biasT power on/off
        {
            set
            {
                CheckHackrfError(libhackrf.hackrf_set_antenna_enable(device, value ? (byte)1 : (byte)0));
            }
        }
        unsafe public bool ClkOut // sets clkout on/off
        {
            set
            {
                CheckHackrfError(libhackrf.hackrf_set_clkout_enable(device, value ? (byte)1 : (byte)0));
            }
        }
        unsafe public double LNAGainDb // adjusts LNA gain in range from 0 dB to 40 dB with 8 dB steps
        {
            set
            {
                int val = (int)value;
                val = (val / 8)*8;
                if (val < 0) val = 0;
                if (val > 40) val = 40;
                CheckHackrfError(libhackrf.hackrf_set_lna_gain(device, (UInt32)val));
            }
        } 
        unsafe public double VGAGainDb // adjusts VGA gain in range from 0 dB to 62 dB with 2 dB steps
        {
            set
            {
                int val = (int)value;
                val = (val / 2) * 2;
                if (val < 0) val = 0;
                if (val > 62) val = 62;
                CheckHackrfError(libhackrf.hackrf_set_vga_gain(device, (UInt32)val));
            }
        } 
        unsafe public double TXVGAGainDb // adjusts transmitter's VGA gain in range from 0 dB to 47 dB
        {
            set
            {
                int val = (int)value;
                if (val < 0) val = 0;
                if (val > 47) val = 47;
                CheckHackrfError(libhackrf.hackrf_set_txvga_gain(device, (UInt32)val));
            }
        } 
        unsafe public bool AMPEnable  // sets external amp on/off
        {
            set
            {
                byte val = value? (byte)1 : (byte)0;
                CheckHackrfError(libhackrf.hackrf_set_amp_enable(device, val));
            }
        }
        unsafe public HackRFStream StartRX() // creates hackrfstream to read interleaved IQ data and starts hackrf receiver
        {
            if (mode == transceiver_mode_t.OFF)
            {
                mode = transceiver_mode_t.RX;
                stream = new HackRFStream(this);
                stream.callback = stream.StreamCallback; // delegate is needed to get cdecl pointer to StreamCallback method
                libhackrf.hackrf_start_rx(device, Marshal.GetFunctionPointerForDelegate<libhackrf.hackrf_delegate>(stream.callback), null);
                return stream;
            } else
            {
                throw new Exception("Device is already streaming. Firstly close existing stream.");
            }
        }
        unsafe private void InitSweep()
        {
            UInt16[] freqs = new UInt16[sweepInfo.rangesMHz.Count * 2];
            for ( int i = 0; i < sweepInfo.rangesMHz.Count; i++)
            {
                freqs[i * 2] = (UInt16)(sweepInfo.rangesMHz[i].Item1);
                freqs[i * 2 + 1] = (UInt16)(sweepInfo.rangesMHz[i].Item2);
            }
            fixed ( UInt16* ptr = freqs)
            {
                CheckHackrfError(libhackrf.hackrf_init_sweep(device, ptr, (UInt32)(sweepInfo.rangesMHz.Count), sweepInfo.stepSamples * 2, (UInt32)(sweepInfo.stepMHz * 1000000), (UInt32)(sweepInfo.offsetMHz * 1000000), sweepInfo.interpolating ? 1u : 0u));
            }
         }
        unsafe public HackRFStream StartSweepRX() // creates hackrfstream to receive sweep data
        {
            CheckVersion();
            InitSweep(); // init sweep mode
            if (mode == transceiver_mode_t.OFF)
            {
                mode = transceiver_mode_t.RX;
                stream = new HackRFStream(this);
                stream.callback = stream.StreamCallback; // delegate is needed to get cdecl pointer to StreamCallback method
                CheckHackrfError(libhackrf.hackrf_start_rx_sweep(device, Marshal.GetFunctionPointerForDelegate<libhackrf.hackrf_delegate>(stream.callback), null));
                return stream;
            }
            else
            {
                throw new Exception("Device is already streaming. Firstly close existing stream.");
            }
        }
        unsafe public HackRFStream StartTX() // creates hackrfstream to read interleaved IQ data. hackrf transmitter won't startup until first write operation occures
        {
            if (mode == transceiver_mode_t.OFF)
            {
                mode = transceiver_mode_t.TX;
                stream = new HackRFStream(this);
                stream.callback = stream.StreamCallback; // delegate is needed to get cdecl pointer to StreamCallback method
                return stream; // start_tx(...) method is not called because there's no data to transmit yet
            }
            else
            {
                throw new Exception("Device is already streaming. Firstly close existing stream.");
            }
        }
        unsafe public bool IsStreaming // returns true if hackrf is currently receiving/transmitting
        {
            get => libhackrf.hackrf_is_streaming(device) == libhackrf.hackrf_error.HACKRF_TRUE;
        } 
    }
}

