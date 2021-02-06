using System;
using System.Collections.Generic;
using System.IO;
using nethackrf;

namespace main_test
{
    public class program
    {
        static public void Main(string[] args)
        {
            System.Console.WriteLine(NetHackrf.HackrfLibraryVersion());
            System.Console.WriteLine(NetHackrf.HackrfLibraryRelease());
            var devices = NetHackrfLow.HackrfDeviceList();
            foreach (var i in devices)
            {
                System.Console.WriteLine($"serial=\"{i.serial_number}\", boardId=\"{i.usb_board_id}\", index={i.usb_device_index}");
            }
            if (devices.Length == 0)
            {
                System.Console.WriteLine("No hackrf devices were found");
                System.Console.WriteLine("Press any key to close application");
                System.Console.ReadKey();
                return;
            }
            NetHackrfLow device = devices[0].OpenDevice();
            System.Console.WriteLine(device.HackrfVersion);
            System.Console.WriteLine(device.UsbApiVersion);
            device.CarrierFrequencyMHz = 444.0;
            device.SampleFrequencyMHz = 2;
            device.FilterBandwidthMHz = 0.5;
            device.TXVGAGainDb = 20;
            byte[] buf = new byte[20000000];
            double phase = 0;
            for (int i = 0; i < buf.Length; i += 2)
            {
                double signal = 50.0 * Math.Sin((double)i / 4000.0 * Math.PI * 2.0);
                phase += signal * 0.0001;
                buf[i] = (byte)((sbyte)(Math.Sin(phase) * 120.0));
                buf[i + 1] = (byte)((sbyte)(Math.Cos(phase) * 120.0));
            }
            var writer = device.StartTX();
            for (int i = 0; i < 10; i++)
            {
                writer.Write(buf, 0, 2000000);
                System.Console.WriteLine($"Writing block #{i}...");
            }
            writer.Dispose();
            System.Console.WriteLine("Press any key to close application");
            System.Console.ReadKey();
        }
    }
}
