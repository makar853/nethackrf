#pragma warning disable 0649

using System;
using System.Runtime.InteropServices;

namespace nethackrf
{

    internal class libhackrf // functions and types from hackrf.h
    {
        public const string dllname = @"hackrf.dll";
        public enum hackrf_error
        {
            HACKRF_SUCCESS = 0,
            HACKRF_TRUE = 1,
            HACKRF_ERROR_INVALID_PARAM = -2,
            HACKRF_ERROR_NOT_FOUND = -5,
            HACKRF_ERROR_BUSY = -6,
            HACKRF_ERROR_NO_MEM = -11,
            HACKRF_ERROR_LIBUSB = -1000,
            HACKRF_ERROR_THREAD = -1001,
            HACKRF_ERROR_STREAMING_THREAD_ERR = -1002,
            HACKRF_ERROR_STREAMING_STOPPED = -1003,
            HACKRF_ERROR_STREAMING_EXIT_CALLED = -1004,
            HACKRF_ERROR_USB_API_VERSION = -1005,
            HACKRF_ERROR_NOT_LAST_DEVICE = -2000,
            HACKRF_ERROR_OTHER = -9999,
        };
        public enum hackrf_usb_board_id
        {
            USB_BOARD_ID_JAWBREAKER = 0x604B,
            USB_BOARD_ID_HACKRF_ONE = 0x6089,
            USB_BOARD_ID_RAD1O = 0xCC15,
            USB_BOARD_ID_INVALID = 0xFFFF,
        };
        public struct hackrf_device
        {

        }
        unsafe public struct hackrf_transfer
        {
            public hackrf_device* device;
            public byte* buffer;
            public int buffer_length;
            public int valid_length;
            public void* rx_ctx;
            public void* tx_ctx;
        };
        unsafe public struct hackrf_device_list_t
        {
            public byte** serial_numbers;
            public hackrf_usb_board_id* usb_board_ids;
            public int* usb_device_index;
            public int devicecount;

            public void** usb_devices;
            public int usb_devicecount;
        };
        [DllImport(dllname)]
        public static extern hackrf_error hackrf_init();
        [DllImport(dllname)]
        public static extern hackrf_error hackrf_exit();
        [DllImport(dllname)]
        unsafe public static extern byte* hackrf_library_version();
        [DllImport(dllname)]
        unsafe public static extern byte* hackrf_library_release();
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_device_list_open(hackrf_device_list_t* list, int idx, hackrf_device** device);
        [DllImport(dllname)]
        unsafe public static extern void hackrf_device_list_free(hackrf_device_list_t* list);
        [DllImport(dllname)]
        unsafe public static extern hackrf_device_list_t* hackrf_device_list();
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_open_by_serial(byte* desired_serial_number, hackrf_device** device);
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_close(hackrf_device* device);
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_set_baseband_filter_bandwidth(hackrf_device* device, UInt32 bandwidth_hz);
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_set_freq(hackrf_device* device, UInt64 freq_hz);
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_set_sample_rate(hackrf_device* device, double freq_hz);
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_set_amp_enable(hackrf_device* device, byte value);
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_set_lna_gain(hackrf_device* device, UInt32 value);
        [DllImport(dllname)]
        /* range 0-62 step 2db, BB gain in osmosdr */
        unsafe public static extern hackrf_error hackrf_set_vga_gain(hackrf_device* device, UInt32 value);
        [DllImport(dllname)]
        /* range 0-47 step 1db */
        unsafe public static extern hackrf_error hackrf_set_txvga_gain(hackrf_device* device, UInt32 value);
        [DllImport(dllname)]
        /* antenna port power control */
        unsafe public static extern hackrf_error hackrf_set_antenna_enable(hackrf_device* device, byte value);
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_board_id_read(hackrf_device* device, byte* value);
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_version_string_read(hackrf_device* device, byte* version, byte length);
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_usb_api_version_read(hackrf_device* device, UInt16* version);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        unsafe public delegate int hackrf_delegate(hackrf_transfer* transfer);

        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_start_rx(hackrf_device* device, IntPtr callback, void* rx_ctx);
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_stop_rx(hackrf_device* device);
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_start_tx(hackrf_device* device, IntPtr callback, void* tx_ctx);
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_stop_tx(hackrf_device* device);
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_is_streaming(hackrf_device* device);
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_reset(hackrf_device* device);
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_set_clkout_enable(hackrf_device* device, byte value);

        // low-level programming functions
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_max2837_read(hackrf_device* device, byte register_number, UInt16* value);
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_max2837_write(hackrf_device* device, byte register_number, UInt16 value);
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_si5351c_read(hackrf_device* device, UInt16 register_number, UInt16* value);
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_si5351c_write(hackrf_device* device, UInt16 register_number, UInt16 value);
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_rffc5071_read(hackrf_device* device, byte register_number, UInt16* value);
        [DllImport(dllname)]
        unsafe public static extern hackrf_error hackrf_rffc5071_write(hackrf_device* device, byte register_number, UInt16 value);
    }
}
