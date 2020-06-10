using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace nethackrf
{
    public class HackRFStream : Stream
    {
        internal HackRFStream(NetHackrf parent)
        {
            device = parent;
            max_length = 10240000;
            stream_buffer = new byte[max_length];
            buffer_semaphore = new Semaphore(1, 1);
        } // private constructor. Can be called through NetHackrf.StartRX() or NetHackrf.StartTX() methods
        NetHackrf device;
        Semaphore buffer_semaphore; // semaphore is used to precent simultaneous access to steam_buffer which is used both by callback and read/write methods

        byte[] stream_buffer;
        int read_pos = 0;
        int write_pos = 0;
        int max_length;
        bool disposed = false;
        internal libhackrf.hackrf_delegate callback; // callback delegate which pointer is used by libhackrf to exchange data with device
        Semaphore event_sem = new Semaphore(0, 1);
        private void set_event() // set_event and get_event are used to lower CPU usage while Write and Read methods are waiting for new portion of data
        {
            try
            {
                event_sem.Release();
            } catch (SemaphoreFullException)
            {

            }
        }
        private void get_event()
        {
            event_sem.WaitOne(5000);
        }
        unsafe internal int StreamCallback(libhackrf.hackrf_transfer* transfer) // hackrf.dll calls this method to exchange data with device
        {
            if (disposed || transfer == null) // this method should be immedeately ended if stream is closed
            {
                return 0;
            }
            if (device.mode == NetHackrf.transceiver_mode_t.RX)
            {
                buffer_semaphore.WaitOne();
                for (int i = 0; i < transfer->valid_length; i++)
                {
                    stream_buffer[write_pos] = transfer->buffer[i];
                    write_pos++;
                    if (write_pos >= stream_buffer.Length) write_pos = 0;
                    if (write_pos == read_pos) SetOverrunError();
                }
                buffer_semaphore.Release();
                set_event();
            } else if(device.mode == NetHackrf.transceiver_mode_t.TX)
            {
                buffer_semaphore.WaitOne();
                for (int i = 0; i < transfer->valid_length; i++)
                {
                    transfer->buffer[i] = stream_buffer[read_pos];
                    read_pos++;
                    if (read_pos >= stream_buffer.Length) read_pos = 0;
                    if (read_pos == write_pos)
                    {
                        SetOverrunError();
                        break;
                    }
                }
                buffer_semaphore.Release();
                set_event();
            }
            return 0;
        }
        public bool OverrunError
        {
            get; private set;
        }
        private void SetOverrunError() // creates warning of buffer overrun and stops device streaming if necessary
        {
            OverrunError = true;
            if (device.mode == NetHackrf.transceiver_mode_t.TX)
            {
                stop_tx_async();
            }
        }
        private async void stop_tx_async() // asynchronous method is used to stop hackrf streaming because hackrf_stop_tx can't be called inside of callback function thread
        {
            await Task.Run(() =>
            {
                unsafe
                {
                    buffer_semaphore.WaitOne();
                    device.TxStarted = false;
                    libhackrf.hackrf_stop_tx(device.device);
                    read_pos = 0;
                    write_pos = 0;
                    set_event();
                    buffer_semaphore.Release();
                }
            });
        }
        public override bool CanRead { get => device.mode == NetHackrf.transceiver_mode_t.RX; }
        public override bool CanWrite { get => device.mode == NetHackrf.transceiver_mode_t.TX; }

        public override bool CanSeek { get => false; }

        public override long Length { get {
                int ret = write_pos - read_pos;
                if (ret < 0) ret += stream_buffer.Length;
                return ret;
            } }

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            if (disposed) throw new EndOfStreamException();
            buffer_semaphore.WaitOne();
            stream_buffer = new byte[max_length];
            read_pos = 0;
            write_pos = 0;
            buffer_semaphore.Release();
        }

        public override int Read(byte[] buffer, int offset = 0, int count = -1) // implementation of Stream.Read(...) method
        {
            if (disposed) throw new EndOfStreamException();
            if (count < 0) count = buffer.Length;
            if ( device.mode == NetHackrf.transceiver_mode_t.RX)
            {
                if (count > stream_buffer.Length / 2) // if needed data length is bigger than half of buffer size then transfer should be devided into smaller parts
                {
                    int blocksize = stream_buffer.Length / 2;
                    int curpos = 0;
                    do
                    {
                        Read(buffer, curpos, blocksize);
                        count -= blocksize;
                        curpos += blocksize;
                        if ( count < blocksize)
                        {
                            blocksize = count;
                        }
                    } while (count > 0);
                    return 0;
                } else
                {
                    int avail;
                    do
                    {
                        if (!device.IsStreaming) throw new IOException("HackRF is not streaming data!");
                        avail = write_pos - read_pos;
                        if (avail < 0) avail += stream_buffer.Length;
                        if ( avail < count ) get_event(); // waiting for next callback to lower cpu usage
                    } while (avail < count); // waiting for required data amount in buffer
                    buffer_semaphore.WaitOne();
                    for (int i = 0; i < count; i++) // reading data from stream buffer
                    {
                        buffer[i + offset] = stream_buffer[read_pos];
                        read_pos++;
                        if (read_pos >= stream_buffer.Length) read_pos = 0;
                    }
                    buffer_semaphore.Release();
                }
                return 0;
            } else
            {
                throw new NotImplementedException();
            }
        }
        public byte[] Read(int count)
        {
            byte[] ret = new byte[count];
            Read(ret, 0, count);
            return ret;
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            if (disposed) throw new EndOfStreamException();
            if (value > int.MaxValue) throw new System.OutOfMemoryException();
            max_length = (int)value;
            Flush();
        }

        unsafe public override void Write(byte[] buffer, int offset = 0, int count = -1) // implementation of Stream.Write(...) method
        {
            if (disposed) throw new EndOfStreamException();
            if (count < 0) count = buffer.Length;
            if (device.mode == NetHackrf.transceiver_mode_t.TX)
            {
                if (count > stream_buffer.Length / 2) // if needed data length is bigger than half of buffer size then transfer should be devided into smaller parts
                {
                    int blocksize = stream_buffer.Length / 2;
                    int curpos = 0;
                    do
                    {
                        Write(buffer, curpos, blocksize);
                        count -= blocksize;
                        curpos += blocksize;
                        if (count < blocksize)
                        {
                            blocksize = count;
                        }
                    } while (count > 0);
                }
                else
                {
                    int avail;
                    do
                    {
                        if (!device.IsStreaming && device.TxStarted) throw new IOException("HackRF is not streaming data!");
                        avail = read_pos - write_pos;
                        if (avail <= 0) avail += stream_buffer.Length;
                        if (avail < count) get_event(); // waiting for next callback to lower cpu usage
                    } while (avail < count); // waiting for required data amount in buffer
                    buffer_semaphore.WaitOne();
                    for (int i = 0; i < count; i++) // writing data to stream buffer
                    {
                        stream_buffer[write_pos] = buffer[i + offset];
                        write_pos++;
                        if (write_pos >= stream_buffer.Length) write_pos = 0;
                    }
                    buffer_semaphore.Release();
                }
                if (device.TxStarted == false)
                {
                    libhackrf.hackrf_start_tx(device.device, Marshal.GetFunctionPointerForDelegate<libhackrf.hackrf_delegate>(callback), null);
                    device.TxStarted = true;
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        unsafe protected override void Dispose(bool disposing) // dispose method is used to stop device transmitter or receiver. Stream must be disposed before new stream creating attempt
        {
            if (disposed == false)
            {
                disposed = true;
                if (device.mode == NetHackrf.transceiver_mode_t.RX)
                {
                    device.mode = NetHackrf.transceiver_mode_t.OFF;
                    libhackrf.hackrf_stop_rx(device.device);
                }
                else if (device.mode == NetHackrf.transceiver_mode_t.TX)
                {
                    device.mode = NetHackrf.transceiver_mode_t.OFF;
                    if (device.TxStarted) libhackrf.hackrf_stop_tx(device.device);
                }
                device.TxStarted = false;
                base.Dispose(disposing);
            }
        }
    }
}