using System;
using nethackrf;
using System.IO;
using System.Numerics;
namespace WFM_RX_NETCORE_example
{
    class Program
    {
        static void Main(string[] args)
        {

            var devices = NetHackrf.HackrfDeviceList(); // get list of all connected hackrf transceivers
            if (devices.Length == 0) // if no hackrfs discovered
            {
                System.Console.WriteLine("No hackrf devices were found");
                System.Console.WriteLine("Press any key to close application");
                System.Console.ReadKey();
                return;
            }
            System.Console.WriteLine("Press any key to stop the application...");
            NetHackrf device = devices[0].OpenDevice(); // connecting to the first transceiver in the list
            device.CarrierFrequencyMHz = 104.7;
            device.SampleFrequencyMHz = 2.016; // 48kHz * 42
            device.LNAGainDb = 40; // setting amlpifiers gain values
            device.VGAGainDb = 40;
            device.FilterBandwidthMHz = 0.2;

            FileStream output = new FileStream("test.wav", FileMode.OpenOrCreate); // opening wave file
            Stream stream = device.StartRX();

            byte[] buffer = new byte[1000000];

            bool started = false;

            AddWaveHeader(output, 0); // adding wave header with zero file length placeholders (we will change them later)

            int file_length = 0;

            try {
                while (System.Console.KeyAvailable == false)
                {
                    stream.Read(buffer, 0, buffer.Length); // reading interpolated IQ data from stream
                    System.Console.WriteLine("demodulating...");
                    var IQ_data = ConvertToIQ(buffer); // converting interpolated IQ data to complex array
                    IQ_data = LPF1(IQ_data); // low pass filter to cutoff other frequencies
                    var demod = DemodFMsamples(IQ_data, 4000); // FM demodulator
                    demod = LPF2(demod); // low pass filter to cutoff pilot tone, stereo fm subcarrier and RDS
                    demod = Decimate(demod, 42); // changing sample frequency to 48kHz
                    var bytes = ConvertToBuffer(demod); // converting array of double to array of bytes
                    file_length += bytes.Length;
                    output.Write(bytes); // writing sound data to the file
                }

                stream.Close();
                output.Close(); // closing stream and file

                output = new FileStream("test.wav", FileMode.Open); // changing length values in the header to correct ones
                AddWaveHeader(output, file_length);
                output.Close();
            }
            catch (Exception)
            {
                device.Reset(); // reset hackRF if something goes wrong
            }
        }

        static Complex[] ConvertToIQ(byte[] buffer)
        {
            Complex[] ret = new Complex[buffer.Length / 2];
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = new Complex((sbyte)(buffer[2 * i]), (sbyte)(buffer[2 * i + 1]));
            }
            return ret;
        }


        static double[] coefs = {-0.006052 ,    -0.005539 , -0.007277 , -0.008615 , -0.009164 , -0.008511 , -0.006271 , -0.002146 , 0.004031 ,  0.012244 ,  0.022290 ,  0.033752 ,  0.046049 ,  0.058433 ,  0.070094 ,  0.080216 ,  0.088051 ,  0.093009 ,  0.094705 ,  0.093009 ,  0.088051 ,  0.080216 ,  0.070094 ,  0.058433 ,  0.046049 ,  0.033752 ,  0.022290 ,  0.012244 ,  0.004031 ,  -0.002146 , -0.006271 , -0.008511 , -0.009164 , -0.008615 , -0.007277 , -0.005539 , -0.006052};
        static Complex[] prev_val = new Complex[coefs.Length];
        static Complex[] LPF1(Complex[] data ) // FIR filter (36 order, 40kHz passband edge, 150kHz stopband edge, 40dB stopband att)
        {
            for ( int  i = 0; i < data.Length; i++)
            {
                Array.Copy(prev_val, 1, prev_val, 0, prev_val.Length-1);
                prev_val[prev_val.Length - 1] = data[i];
                Complex output = Complex.Zero;
                for ( int j = 0; j < prev_val.Length; j++)
                {
                    output += prev_val[j] * coefs[j];
                }
                data[i] = output;
            }
            return data;
        }


        static double[] coefs2 = { 0.0, 0.001741 ,  -0.015102 , 0.007302 ,  0.041937 ,  -0.057247 , -0.070744 , 0.298657 ,  0.583333 ,  0.298657 ,  -0.070744 , -0.057247 , 0.041937 ,  0.007302 ,  -0.015102 , 0.001741 };
        static double[] prev_val2 = new double[16];
        static double[] LPF2(double[] data) // FIR filter (16 order, 10kHz passband edge, 17kHz stopband edge, 50dB stopband att)
        {
            for (int i = 0; i < data.Length; i++)
            {
                Array.Copy(prev_val2, 1, prev_val2, 0, 15);
                prev_val2[15] = data[i];
                double output = 0;
                for (int j = 0; j < 16; j++)
                {
                    output += prev_val2[j] * coefs2[j];
                }
                data[i] = output;
            }
            return data;
        }

        static void AddWaveHeader(Stream s, Int32 size) // wave header writer. Described at http://soundfile.sapp.org/doc/WaveFormat/
        {
            byte[] header = {   0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x80, 0x57, 0x41, 0x56, 0x45, // RIFF chunk descriptor
                                0x66, 0x6d, 0x74, 0x20, 0x10, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, // fmt sub-chunk PCM mono 
                                0x80, 0xBB, 0x00, 0x00, 0x00, 0x77, 0x01, 0x00, 0x02, 0x00, 0x10, 0x00, // 48000 sample rate, 96000 byte rate, 16 bits per sample
                                0x64, 0x61, 0x74, 0x61, 0x00, 0x00, 0x00, 0x80}; // DATA
            Array.Copy(BitConverter.GetBytes(size + 36), 0, header, 4, 4);
            Array.Copy(BitConverter.GetBytes(size), 0, header, 40, 4);
            s.Write(header, 0, 44);
        }

        static double prev_I = 0;
        static double prev_Q = 0;
        static double[] DemodFMsamples( Complex[] IQ, double Fs)
        {
            double[] ret = new double[IQ.Length];
            double I;
            double Q;
            for ( int i = 0; i < ret.Length; i ++ )
            {
                I = IQ[i].Real;
                Q = IQ[i].Imaginary;
                double dI = (I - prev_I) * Fs;
                double dQ = (Q - prev_Q) * Fs;

                ret[i] = (dI * Q - dQ * I) / (I * I + Q * Q); // https://ru.dsplib.org/content/signal_fm_demod/img_html/fmdemod_html_46eb685f.gif

                prev_I = I;
                prev_Q = Q;
            }
            return ret;
        }

        static double[] Decimate ( double[] data, int K)
        {
            double[] ret = new double[data.Length / K];
            for ( int i = 0; i < ret.Length; i++)
            {
                ret[i] = data[K*i];
            }
            return ret;
        }

        static byte[] ConvertToBuffer( double[] data)
        {
            byte[] buffer = new byte[data.Length * 2];
            for (int i = 0; i < data.Length; i++)
            {
                Int16 sample = (short)(data[i]);
                var bytes = BitConverter.GetBytes(sample);
                buffer[i * 2] = bytes[0];
                buffer[i * 2 + 1] = bytes[1];
            }
            return buffer;
        }
    }
}
